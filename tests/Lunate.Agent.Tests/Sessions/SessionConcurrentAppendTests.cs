using System.Globalization;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// One session, two appending threads: the harness loop appends the message a tool call produced
/// while the session's event consumer appends the usage that tool recorded. The file is
/// append-only JSONL, so both appends are one critical section — an entry id is minted from the
/// file's own sequence and its line lands whole (spec: the session file).
/// </summary>
/// <remarks>
/// Unsynchronized, both appends read the same end of the file and the later line is written on top
/// of the earlier one: the file keeps the second line's bytes where the first line's head was, so
/// the line in between no longer parses. macOS (dotnet 11) reproduces it in a few hundred appends;
/// the failing line is what made <c>verify (macos-latest, 2/2)</c> read a torn session (#1050).
/// </remarks>
public sealed class SessionConcurrentAppendTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Appends_from_two_threads_keep_every_line_whole()
    {
        using var temp = new TempDirectory();
        string path = temp.File("s.jsonl");
        Session session = Session.Create(path, "/work", new FixedTimeProvider(Start));

        const int Writers = 4;
        const int PerWriter = 100;
        string filler = new('x', 4096);
        using var gate = new Barrier(Writers);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        List<Thread> threads = [];
        for (var writer = 0; writer < Writers; writer++)
        {
            int index = writer;
            threads.Add(
                new Thread(() =>
                {
                    gate.SignalAndWait();
                    for (var turn = 0; turn < PerWriter; turn++)
                    {
                        try
                        {
                            session.AppendMessage(
                                new ChatMessage(
                                    ChatRole.User,
                                    string.Create(
                                        CultureInfo.InvariantCulture,
                                        $"w{index}-{turn}-{filler}"
                                    )
                                )
                            );
                        }
                        catch (Exception exception)
                        {
                            failures.Enqueue(exception);
                        }
                    }
                })
            );
        }

        foreach (Thread thread in threads)
        {
            thread.Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.Empty(failures);

        Session reloaded = Session.Load(path);
        Assert.Equal(
            Enumerable.Range(1, Writers * PerWriter).Select(index => $"e_{index:D2}"),
            reloaded.Entries.Select(entry => entry.Id)
        );

        string[] written = [.. reloaded.Entries.OfType<SessionMessageEntry>().Select(EntryText)];
        string[] expected =
        [
            .. from writer in Enumerable.Range(0, Writers)
            from turn in Enumerable.Range(0, PerWriter)
            select string.Create(CultureInfo.InvariantCulture, $"w{writer}-{turn}-{filler}"),
        ];
        Assert.Equal(
            [.. expected.Order(StringComparer.Ordinal)],
            [.. written.Order(StringComparer.Ordinal)]
        );
    }

    private static string EntryText(SessionMessageEntry entry) =>
        entry.Message.Text ?? string.Empty;
}
