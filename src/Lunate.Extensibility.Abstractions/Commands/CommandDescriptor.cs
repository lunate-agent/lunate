namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// One command a source offers. <see cref="Name"/> is the name as typed without the leading slash
/// (a skill is <c>skill:&lt;name&gt;</c>); <see cref="ArgumentHint"/> is optional display text.
/// </summary>
public sealed record CommandDescriptor(
    string Name,
    string Description,
    string? ArgumentHint,
    string SourceId,
    CommandKind Kind
);
