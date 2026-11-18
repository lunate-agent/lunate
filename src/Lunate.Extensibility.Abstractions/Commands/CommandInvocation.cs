namespace Lunate.Extensibility.Abstractions;

/// <summary>What a source is asked to run: the bare command name and the argument remainder.</summary>
public sealed record CommandInvocation(string Name, string Argument);
