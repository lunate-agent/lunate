namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// One source of slash commands. The host asks every source what it offers once per composition
/// and invokes the source that owns a submitted name. Both calls are asynchronous and cancellable;
/// registration is registration only and must not start processes, sockets or timers.
/// </summary>
public interface ICommandSource
{
    /// <summary>The source's id; diagnostics name it next to the owning extension.</summary>
    string SourceId { get; }

    /// <summary>The commands this source currently offers, in display order.</summary>
    ValueTask<IReadOnlyList<CommandDescriptor>> DescribeAsync(CancellationToken cancellationToken);

    /// <summary>Runs one command this source owns.</summary>
    ValueTask<CommandResult> InvokeAsync(
        CommandInvocation invocation,
        CancellationToken cancellationToken
    );
}
