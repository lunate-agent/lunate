using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

/// <summary>The outcome of a write-verifier call.</summary>
public enum WriteVerificationStatus
{
    Verified,
    Unverified,
    Refused,
}
