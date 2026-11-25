using System.Globalization;
using System.Text;
using System.Text.Json;
using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The optional <c>semantic_search</c> tool: embeds the query with the resolved
/// <c>usages.embeddings</c> profile and returns the workspace regions whose embeddings are closest,
/// ranked and bounded. The index behind it is a rebuildable cache under the state root, so nothing
/// of this tool's state is committed or written into a session.
/// </summary>
internal sealed class SemanticSearchTool : ITool
{
    internal const string ToolName = "semantic_search";
    private const int DefaultMaxResults = 10;
    private const int MaxResultsCap = 50;
    private const int MaxOutputBytes = 8 * 1024;

    private static readonly IReadOnlyList<string> Accepted = ["query", "max_results"];

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "What to look for, in natural language or as a code phrase"
                },
                "max_results": {
                  "type": "integer",
                  "description": "Maximum ranked regions (default 10, max 50)"
                }
              },
              "required": ["query"]
            }
            """
        )
        .RootElement.Clone();

    private readonly EmbeddingsBinding _binding;
    private readonly SemanticIndex _index;

    internal SemanticSearchTool(Workspace workspace, EmbeddingsBinding binding, SemanticIndex? index = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(binding);
        _binding = binding;
        _index =
            index
            ?? new SemanticIndex(
                workspace.WorktreeRoot,
                SemanticIndex.CacheFile(workspace.WorktreeRoot, binding.ModelId),
                binding.ModelId
            );
    }

    public string Name => ToolName;

    public string Description =>
        "Search the workspace semantically: embed the query and return the best-matching file "
        + "regions ranked by similarity, as 'path:start-end' with a snippet. Use it when you know "
        + "what the code does but not the words it uses; use grep for an exact string or pattern.";

    public JsonElement ParametersSchema => Schema;

    public ToolRisk Risk => ToolRisk.ReadOnly;

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
            !ToolArguments.TryString(args, "query", out string? query, out string? error)
            || !ToolArguments.TryInteger(args, "max_results", out int? maxResults, out error)
        )
        {
            return Error(error!);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("query is required and must be a non-empty string");
        }

        var limit = maxResults ?? DefaultMaxResults;
        if (limit < 1)
        {
            return Error("max_results must be >= 1");
        }

        limit = Math.Min(limit, MaxResultsCap);

        var attribution = new ModelUsageAttribution(
            EmbeddingsUsage.Usage,
            _binding.ProfileName,
            _binding.ProviderName,
            _binding.ProviderKind
        );

        SemanticSearchResult result;
        try
        {
            result = await _index.SearchAsync(
                _binding.Client,
                query.Trim(),
                limit,
                usage =>
                    AgentToolContextState.RecordUsageFor(ctx, usage, _binding.ModelId, attribution),
                ct
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Error(
                $"semantic_search could not embed '{(query.Length <= 200 ? query.Trim() : Truncate(query.Trim(), 200) + "…")}' "
                    + $"with the embeddings profile '{_binding.ProfileName}' (model "
                    + $"'{_binding.ModelId}'): {exception.Message}. Check 'usages.embeddings' and the "
                    + "provider credential, then retry; 'grep' still works for an exact string."
            );
        }

        if (result.Hits.Count == 0)
        {
            return new ToolResult(
                $"No indexed files match '{query.Trim()}' (nothing readable to search under the "
                    + "workspace, or no similarity above the floor)",
                IsError: false
            );
        }

        return new ToolResult(Render(query.Trim(), result), IsError: false);
    }

    /// <summary>
    /// Renders the ranked hits: the query, one numbered line per region and the caps that actually
    /// cut the result. Output stops at the byte cap or before a hit that would cross it, so a cut
    /// never splits a UTF-8 sequence or a surrogate pair.
    /// </summary>
    internal static string Render(string query, SemanticSearchResult result)
    {
        var output = new StringBuilder();
        output.Append("query: ").Append(query).Append('\n');
        var rendered = 0;
        foreach (SemanticHit hit in result.Hits)
        {
            string snippet = hit.Snippet.Replace('\n', ' ');
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{rendered + 1}. {hit.Path}:{hit.StartLine}-{hit.EndLine}  {snippet}"
            );
            if (Encoding.UTF8.GetByteCount(line) + 1 > MaxOutputBytes)
            {
                output
                    .Append(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"[stopped at the {MaxOutputBytes} byte cap after {rendered} {(rendered == 1 ? "hit" : "hits")}]"
                        )
                    )
                    .Append('\n');
                return output.ToString().TrimEnd('\n');
            }

            output.Append(line).Append('\n');
            rendered++;
        }

        if (result.Truncated)
        {
            output
                .Append(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"[{rendered} shown; more matching regions exist, raise max_results for more]"
                    )
                )
                .Append('\n');
        }

        if (result.IndexCapped)
        {
            output
                .Append(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"[index capped at {SemanticIndex.MaxChunks} chunks]"
                    )
                )
                .Append('\n');
        }

        return output.ToString().TrimEnd('\n');
    }

    private static string Truncate(string value, int length) =>
        char.IsHighSurrogate(value[length - 1]) ? value[..(length - 1)] : value[..length];

    private static ToolResult Error(string output) => new(output, IsError: true);
}
