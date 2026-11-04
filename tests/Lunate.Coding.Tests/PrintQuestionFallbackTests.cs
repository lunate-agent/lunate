using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

[Collection("mcp-processes")]
public sealed class PrintQuestionFallbackTests
{
    [Fact]
    public async Task Ask_user_returns_the_no_user_result_without_question_output()
    {
        using var temp = new TempDirectory();
        var client = new ScriptedChatClient()
            .Enqueue(
                Scripts.Call(
                    "question-1",
                    "ask_user",
                    Scripts.Args(
                        (
                            "questions",
                            new[]
                            {
                                new
                                {
                                    header = "Plan",
                                    question = "Which plan should I use?",
                                    options = new[]
                                    {
                                        new
                                        {
                                            label = "Simple",
                                            description = "Fewer moving parts.",
                                        },
                                        new
                                        {
                                            label = "Flexible",
                                            description = "More room to grow.",
                                        },
                                    },
                                    multiSelect = false,
                                },
                            }
                        )
                    )
                ),
                Scripts.ToolCalls()
            )
            .Enqueue(Scripts.Text("I'll assume the simple plan and continue."), Scripts.Stop());
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Choose a plan.",
        };

        TextReader originalInput = Console.In;
        var input = new CountingTextReader();
        Console.SetIn(input);
        (int ExitCode, string Output, string Errors) run;
        try
        {
            run = await PrintModeTestSupport
                .RunAsync(options)
                .WaitAsync(WaitBudgets.InProcess, TestContext.Current.CancellationToken);
        }
        finally
        {
            Console.SetIn(originalInput);
        }

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("I'll assume the simple plan and continue.", run.Output);
        Assert.Empty(run.Errors);
        Assert.Equal(0, input.ReadCount);
        Assert.Contains(client.RequestOptions[0]!.Tools!, tool => tool.Name == "ask_user");
        FunctionResultContent result = Assert.Single(
            client
                .Requests[1]
                .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
        );
        Assert.Contains(
            "unanswered: no user present",
            result.Result?.ToString(),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "State an assumption and continue",
            result.Result?.ToString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Suggestions_in_print_mode_propose_no_question_decision()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("models.json"),
            """
            {"schemaVersion":2,"models":[
              {"id":"gpt-4o-mini","provider":"openai","contextWindow":64000,"supportsTools":true},
              {"id":"decision-model","provider":"openai","contextWindow":64000,"supportsTools":false}
            ]}
            """
        );
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {"schemaVersion":2,"model":"gpt-4o-mini",
             "profiles":{"decision":{"provider":"decision-provider","model":"decision-model"}},
             "decisions":{"profile":"decision","questions":"suggest"}}
            """
        );
        bool valid = ProviderEntry.TryCreate(
            "decision-provider",
            "openai",
            new Uri("https://provider.example.test/v1"),
            "auth:decision-provider",
            headers: null,
            region: null,
            out ProviderEntry? decisionProvider,
            out string? providerError
        );
        Assert.True(valid, providerError);
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(temp.File("auth.json")),
            [Assert.IsType<ProviderEntry>(decisionProvider)]
        );
        var client = new ScriptedChatClient()
            .Enqueue(
                Scripts.Call(
                    "question-1",
                    "ask_user",
                    Scripts.Args(
                        (
                            "questions",
                            new[]
                            {
                                new
                                {
                                    header = "Plan",
                                    question = "Which plan should I use?",
                                    options = new[]
                                    {
                                        new
                                        {
                                            label = "Simple",
                                            description = "Fewer moving parts.",
                                        },
                                        new
                                        {
                                            label = "Flexible",
                                            description = "More room to grow.",
                                        },
                                    },
                                    multiSelect = false,
                                },
                            }
                        )
                    )
                ),
                Scripts.ToolCalls()
            )
            .Enqueue(Scripts.Text("I'll assume the simple plan and continue."), Scripts.Stop());
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Choose a plan.",
        };

        (int ExitCode, string Output, string Errors) run = await PrintModeTestSupport
            .RunAsync(options)
            .WaitAsync(WaitBudgets.InProcess, TestContext.Current.CancellationToken);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("I'll assume the simple plan and continue.", run.Output);
        Assert.True(run.Errors.Length == 0, run.Errors);
        // No decision exchange is proposed: only the ask_user turn and the final answer.
        Assert.Equal(2, client.Requests.Count);
        FunctionResultContent result = Assert.Single(
            client
                .Requests[1]
                .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
        );
        Assert.Contains(
            "unanswered: no user present",
            result.Result?.ToString(),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "State an assumption and continue",
            result.Result?.ToString(),
            StringComparison.Ordinal
        );
    }

    private sealed class CountingTextReader : StringReader
    {
        private int readCount;

        internal CountingTextReader()
            : base(string.Empty) { }

        internal int ReadCount => Volatile.Read(ref readCount);

        public override int Read()
        {
            Interlocked.Increment(ref readCount);
            return base.Read();
        }

        public override int Read(char[] buffer, int index, int count)
        {
            Interlocked.Increment(ref readCount);
            return base.Read(buffer, index, count);
        }

        public override string? ReadLine()
        {
            Interlocked.Increment(ref readCount);
            return base.ReadLine();
        }
    }
}
