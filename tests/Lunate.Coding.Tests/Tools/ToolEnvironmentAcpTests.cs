using Lunate.Ai;
using Lunate.Protocols.Acp;

namespace Lunate.Coding.Tests;

/// <summary>
/// ACP's startup line for the tool-environment pass-through: the editor session resolves the same
/// settings as the interactive session and print mode, and states the admitted variables before it
/// can run a tool.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class ToolEnvironmentAcpTests
{
    [Fact]
    public void The_session_start_names_the_admitted_variables()
    {
        try
        {
            using var temp = new TempDirectory();
            ChatUsageProfileTestSupport.SaveProvider(temp);
            var notices = new List<UserNotice>();

            _ = AcpMode.CreateHarness(
                new AcpSessionContext(temp.Root, null, null),
                null,
                [],
                [],
                null,
                new FakeChatClientFactory(new ScriptedChatClient()),
                temp.File("sessions"),
                ChatUsageProfileTestSupport.Settings() with
                {
                    ToolEnvironment = new ToolEnvironmentSettings
                    {
                        Passthrough = ["MY_PROJECT_TOKEN"],
                    },
                },
                ModelCatalog.Load(temp.File("models.json")),
                authPath: temp.File("auth.json"),
                warn: notices.Add
            );

            UserNotice admission = Assert.Single(notices);
            Assert.Equal(NoticeLevel.Info, admission.Level);
            Assert.Contains("MY_PROJECT_TOKEN", admission.Message, StringComparison.Ordinal);
            Assert.Contains("tool processes", admission.Message, StringComparison.Ordinal);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }
}
