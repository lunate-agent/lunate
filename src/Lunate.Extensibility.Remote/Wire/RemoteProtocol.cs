using System.Text.Json;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility.Remote;

/// <summary>The wire protocol constants: version, capabilities and reserved method names.</summary>
public static class RemoteProtocol
{
    /// <summary>
    /// The protocol version this host speaks. Version 2 added <c>command/invoke</c>; version 3 adds
    /// the observable event projection (<c>event/&lt;kind&gt;</c> notifications, ADR-0040). A
    /// version-1 or version-2 child stays readable for hooks and services, and reports its declared
    /// commands as declared, not provided; a manifest that declares <c>events</c> refuses a child
    /// below <see cref="EventsVersion"/> at the handshake.
    /// </summary>
    public const int Version = 3;

    /// <summary>The first version that carries the observable event projection.</summary>
    public const int EventsVersion = 3;

    /// <summary>The oldest child version this host still reads (hooks and services only).</summary>
    public const int MinimumVersion = 1;

    /// <summary>The capabilities the host offers, echoed in <see cref="InitializeRequest"/>.</summary>
    public static IReadOnlyList<string> Capabilities { get; } =
    [
        "hook-requests",
        "hook-notifications",
        "service-call",
        "command-invoke",
        "event-notifications",
        "cancel",
    ];

    public const string InitializeMethod = "initialize";
    public const string ShutdownMethod = "shutdown";
    public const string ServiceCallMethod = "service-call";
    public const string CommandInvokeMethod = "command/invoke";
    public const string CancelMethod = "$/cancelRequest";

    /// <summary>The method prefix one observable event notification is sent under.</summary>
    public const string EventMethodPrefix = "event/";

    /// <summary>
    /// How many events one remote extension may have queued before the oldest is dropped: the same
    /// capacity the in-process subscription uses, so the wire drops at the same pressure.
    /// </summary>
    public const int EventQueueCapacity = 256;

    /// <summary>The JSON-RPC error code a peer answers a cancelled request with. A command invocation
    /// treats it as the <c>cancelled</c> disposition, never as a failure.</summary>
    public const int CancelledErrorCode = -32800;

    /// <summary>The child → host service request method for one service and method.</summary>
    public static string ServiceMethod(string service, string method) =>
        $"service/{service}/{method}";

    /// <summary>The host → child method one observable event notification is sent under.</summary>
    public static string EventMethod(string kind) => EventMethodPrefix + kind;
}

/// <summary>
/// The <c>initialize</c> request (host → child, the first call): the protocol version and
/// capabilities, the manifest echo, the workspace and the manifest's declared hooks, services and
/// event watch list. <see cref="Events"/> absent reads as an empty list, which is what a host that
/// speaks version 3 but has no watch list sends anyway.
/// </summary>
public sealed record InitializeRequest(
    int ProtocolVersion,
    IReadOnlyList<string> Capabilities,
    JsonElement Manifest,
    WorkspaceInfo Workspace,
    IReadOnlyList<string> Hooks,
    IReadOnlyList<string> Services
)
{
    /// <summary>
    /// The manifest's <c>events</c> watch list (<c>["*"]</c> for every kind) the host projects over
    /// the wire. The manifest is the filter: the host sends only these kinds, and only the ones the
    /// child accepts.
    /// </summary>
    public IReadOnlyList<string> Events { get; init; } = [];
}

/// <summary>
/// The child's <c>initialize</c> answer: the protocol version it speaks, the hooks and services it
/// implements, whether it serves command invocations, and the event kinds it accepts. A version the
/// host does not read (or a version above this host's) refuses the extension; an undeclared hook or
/// service refuses it; a declared one that is missing only warns. <see cref="Commands"/> absent
/// reads as false and <see cref="Events"/> absent as an empty list, so a version-1 or version-2
/// answer still deserialises.
/// </summary>
public sealed record InitializeResult(
    int ProtocolVersion,
    IReadOnlyList<string> Hooks,
    IReadOnlyList<string> Services
)
{
    /// <summary>
    /// True when the child can answer <c>command/invoke</c>. The host does not infer it from the
    /// protocol version: a child may speak version 2 and still serve hooks and services only, and
    /// its declared commands then read as declared, not provided.
    /// </summary>
    public bool Commands { get; init; }

    /// <summary>
    /// The event kinds the child accepts (or <c>["*"]</c> for every kind). Empty means the child
    /// accepts none: a manifest that declares <c>events</c> then gets a warning and sends nothing.
    /// Only a child speaking <see cref="RemoteProtocol.EventsVersion"/> or later can declare any.
    /// </summary>
    public IReadOnlyList<string> Events { get; init; } = [];
}
