using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// Verifies staged file text without receiving a file handle or any host write capability. The host
/// owns the staged text and decides whether it is applied.
/// </summary>
public interface IWriteVerifier
{
    /// <summary>Verifies the staged text; cancellation is supplied by the host's bounded call.</summary>
    ValueTask<WriteVerificationResult> VerifyAsync(
        WriteVerificationRequest request,
        CancellationToken cancellationToken
    );
}
