using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

/// <summary>A verifier name and implementation registered by an extension factory.</summary>
public sealed class WriteVerifierRegistration(string name, IWriteVerifier verifier)
{
    public string Name { get; } = name;

    public IWriteVerifier Verifier { get; } = verifier;
}
