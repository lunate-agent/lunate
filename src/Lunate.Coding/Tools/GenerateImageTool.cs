using System.Globalization;
using System.Text.Json;
using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The image-generation tool: asks the resolved <c>usages.image</c> profile for one image, attaches
/// the bytes through the loop's tool-image path (the session sidecar and the typed image part), and
/// optionally writes the same bytes into the workspace under the write tool's path rules.
/// </summary>
internal sealed class GenerateImageTool : ITool
{
    internal const string ToolName = "generate_image";

    private const int PromptPreview = 200;

    private static readonly IReadOnlyList<string> Accepted = ["prompt", "path"];

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "prompt": {
                  "type": "string",
                  "description": "What the image should show, described in full"
                },
                "path": {
                  "type": "string",
                  "description": "Optional file path to also write the image to, relative to the workspace or absolute inside it"
                }
              },
              "required": ["prompt"]
            }
            """
        )
        .RootElement.Clone();

    private readonly Workspace _workspace;
    private readonly ImageGenerationBinding _binding;
    private readonly IFileMutationQueue _mutations;

    internal GenerateImageTool(
        Workspace workspace,
        ImageGenerationBinding binding,
        IFileMutationQueue? mutations = null
    )
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(binding);
        _workspace = workspace;
        _binding = binding;
        _mutations = mutations ?? FileMutationQueue.Shared;
    }

    public string Name => ToolName;

    public string Description =>
        "Generate an image from a text prompt with the configured image profile and attach it to "
        + "the conversation. With a path, the image is also written into the workspace there. "
        + "Images are PNG, JPEG, GIF or WebP, at most 5 MB and 4096 pixels per side.";

    public JsonElement ParametersSchema => Schema;

    /// <summary>
    /// The write risk: the call may write a workspace file, so it needs the same approval a
    /// <c>write</c> call needs. The approval model is per tool, so a call without a path is approved
    /// too; a generation is a paid, state-changing call and must not bypass the policy.
    /// </summary>
    public ToolRisk Risk => ToolRisk.Write;

    public async Task<ToolResult> ExecuteAsync(
        JsonElement args,
        ToolContext ctx,
        CancellationToken ct
    )
    {
        if (!ToolArguments.TryValidate(args, Accepted, out string? argumentError))
        {
            return Error(argumentError!);
        }

        if (
            !ToolArguments.TryString(args, "prompt", out string? prompt, out string? error)
            || !ToolArguments.TryString(args, "path", out string? path, out error)
        )
        {
            return Error(error!);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Error("prompt is required and must be a non-empty string");
        }

        // Resolve the target before the generation call: an unrunnable path must not cost a
        // provider request.
        ResolvedPath? target = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!_workspace.TryResolve(path, out ResolvedPath resolved, out string? pathError))
            {
                return Error(pathError);
            }

            if (Directory.Exists(resolved.AbsolutePath))
            {
                return Error(
                    $"{resolved.RelativePath} is a directory; give the path of an image file instead."
                );
            }

            target = resolved;
        }

        string trimmedPrompt = prompt.Trim();
        GeneratedImageResult generated;
        try
        {
            generated = await _binding.Client.GenerateAsync(trimmedPrompt, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Error(
                $"generate_image could not create an image for '{Preview(trimmedPrompt)}' with the "
                    + $"image profile '{_binding.ProfileName}' (model '{_binding.ModelId}'): "
                    + $"{exception.Message}. Check 'usages.image' and the provider credential, "
                    + "then retry."
            );
        }

        if (generated.Usage is { } usage)
        {
            AgentToolContextState.RecordUsageFor(
                ctx,
                usage,
                _binding.ModelId,
                new ModelUsageAttribution(
                    ImageGenerationUsage.Usage,
                    _binding.ProfileName,
                    _binding.ProviderName,
                    _binding.ProviderKind
                )
            );
        }

        if (generated.Data.Length > ImageLimits.MaxBytes)
        {
            return Error(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the generated image is {generated.Data.Length / 1048576d:F1} MB, over the "
                        + $"{ImageLimits.MaxBytes / 1048576d:F1} MB image limit; "
                        + $"{ImageLimits.DownscalingUnavailable}; pick a model or size that returns "
                        + $"a smaller {ImageLimits.Formats} image."
                )
            );
        }

        if (
            !ImageFile.TryReadDimensions(
                generated.Data.Span,
                out string mime,
                out int width,
                out int height
            )
        )
        {
            return Error(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the image profile '{_binding.ProfileName}' returned {generated.Data.Length} bytes that are not a {ImageLimits.Formats} image; pick a model that returns one of those formats."
                )
            );
        }

        if (width > ImageLimits.MaxSide || height > ImageLimits.MaxSide)
        {
            return Error(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the generated image is {width}x{height}, over the {ImageLimits.MaxSide}-pixel image side limit; {ImageLimits.DownscalingUnavailable}; pick a size that fits."
                )
            );
        }

        var attachment = new ImageAttachment(mime, width, height, generated.Data);
        string placeholder = string.Create(
            CultureInfo.InvariantCulture,
            $"[image {width}x{height}]"
        );

        if (target is null)
        {
            return new ToolResult(placeholder, IsError: false) { Images = [attachment] };
        }

        string? writeError = await WriteAsync(target, generated.Data, ctx, ct);
        if (writeError is not null)
        {
            return new ToolResult(
                $"{placeholder} could not be written to {target.RelativePath}: {writeError}. "
                    + "The image is still attached to the conversation; choose another path or "
                    + "omit path.",
                IsError: true
            )
            {
                Images = [attachment],
            };
        }

        return new ToolResult($"{placeholder} wrote {target.RelativePath}", IsError: false)
        {
            Images = [attachment],
        };
    }

    /// <summary>
    /// Writes the generated bytes under the same per-path lock the write tool uses, creating parent
    /// directories and reporting the mutation to the run's file-change sink. The returned text is
    /// the failure reason, or null on success.
    /// </summary>
    private Task<string?> WriteAsync(
        ResolvedPath target,
        ReadOnlyMemory<byte> data,
        ToolContext ctx,
        CancellationToken ct
    ) =>
        _mutations.RunAsync(
            target.AbsolutePath,
            async mutationCt =>
            {
                bool created = !File.Exists(target.AbsolutePath);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target.AbsolutePath)!);
                    await File.WriteAllBytesAsync(target.AbsolutePath, data, mutationCt);
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    return exception.Message;
                }

                ctx.FileChanges.Notify(
                    new FileChangeEvent(
                        target.AbsolutePath,
                        target.RelativePath,
                        ctx.WorkingDirectory,
                        ctx.RunId,
                        created ? FileChangeKind.Created : FileChangeKind.Modified
                    )
                );
                return null;
            },
            ct
        );

    private static string Preview(string prompt) =>
        prompt.Length <= PromptPreview ? prompt : prompt[..PromptPreview] + "…";

    private static ToolResult Error(string output) => new(output, IsError: true);
}
