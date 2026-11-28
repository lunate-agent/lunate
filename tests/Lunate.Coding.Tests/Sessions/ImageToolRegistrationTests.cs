using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>generate_image</c> tool's registration: offered only when an image binding resolves, so an
/// unset usage leaves the core set untouched and a resolved one adds exactly that tool.
/// </summary>
public sealed class ImageToolRegistrationTests
{
    [Fact]
    public void No_binding_registers_no_image_tool()
    {
        using var temp = new TempDirectory();

        ToolRegistry tools = HarnessFactory.CreateTools(
            new Workspace(temp.Root),
            imageGeneration: () => null
        );

        Assert.Null(tools.Find(GenerateImageTool.ToolName));
    }

    [Fact]
    public void A_resolved_binding_registers_the_tool_beside_the_core_set()
    {
        using var temp = new TempDirectory();

        ToolRegistry tools = HarnessFactory.CreateTools(
            new Workspace(temp.Root),
            imageGeneration: () =>
                new ImageGenerationBinding(
                    new StubImageClient(),
                    "image-profile",
                    "fake-provider",
                    "openai",
                    "gpt-image-1"
                )
        );

        ITool? tool = tools.Find(GenerateImageTool.ToolName);
        Assert.NotNull(tool);
        Assert.Equal(ToolRisk.Write, tool.Risk);
        Assert.Equal("generate_image", tool.Name);
    }

    private sealed class StubImageClient : IImageGenerationClient
    {
        public string ModelId => "gpt-image-1";

        public Task<GeneratedImageResult> GenerateAsync(
            string prompt,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new GeneratedImageResult(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png", null)
            );
    }
}
