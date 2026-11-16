using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

/// <summary>A manifest declaration that binds a verifier name to filename patterns.</summary>
public sealed class WriteVerifierDeclaration(string name, IReadOnlyList<string> patterns)
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Patterns { get; } = patterns;
}
