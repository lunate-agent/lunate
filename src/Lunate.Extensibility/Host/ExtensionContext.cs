using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility;

internal sealed class ExtensionContext : IExtensionContext
{
    private readonly Action<string, IHookHandler>? _register;
    private readonly Action<string, string, IBackgroundService>? _registerService;
    private readonly Action<string, IFileChangedHandler, string?>? _subscribeFileChanged;
    private readonly Action<string, IToolResultRenderer>? _registerToolResultRenderer;
    private readonly Action<string, IStatusWidget>? _registerStatusWidget;
    private readonly Action<string, McpServerRegistration>? _registerMcpServer;
    private readonly Action<string, ExtensionToolRegistration>? _registerTool;
    private readonly Action<string, WriteVerifierRegistration>? _registerWriteVerifier;
    private readonly Action<string, ExtensionModelProvider>? _registerModelProvider;
    private readonly Action<string, ExtensionModelMiddleware>? _registerModelMiddleware;
    private readonly Action<string, VirtualModelRegistration>? _registerVirtualModel;
    private readonly Action<string, ISkillSource>? _registerSkillSource;
    private readonly Action<string, ICommandSource>? _registerCommandSource;
    private readonly Action<string, string, Type, Func<string, object>>? _registerWorkspaceService;
    private readonly Func<
        string,
        string,
        Type,
        string,
        CancellationToken,
        WorkspaceServiceRegistry.WorkspaceServiceLease
    >? _acquireWorkspaceService;
    private readonly Func<string, IBackgroundService?>? _findService;
    private readonly Func<string, object?>? _findCoreService;
    private readonly IExtensionSettings _settings;
    private readonly IExtensionSecrets _secrets;
    private readonly ObservableEvents _events;
    private volatile bool _disposed;

    public ExtensionContext(
        string id,
        IExtensionSettings settings,
        IExtensionSecrets secrets,
        IExtensionLog log,
        Action<string, IHookHandler>? register = null,
        Action<string, string, IBackgroundService>? registerService = null,
        Action<string, IFileChangedHandler, string?>? subscribeFileChanged = null,
        Func<string, IBackgroundService?>? findService = null,
        Func<string, object?>? findCoreService = null,
        Action<string, IToolResultRenderer>? registerToolResultRenderer = null,
        Action<string, IStatusWidget>? registerStatusWidget = null,
        Action<string, McpServerRegistration>? registerMcpServer = null,
        Action<string, ExtensionToolRegistration>? registerTool = null,
        Action<string, WriteVerifierRegistration>? registerWriteVerifier = null,
        SessionEventStream? events = null,
        Action<string, ExtensionModelProvider>? registerModelProvider = null,
        Action<string, ExtensionModelMiddleware>? registerModelMiddleware = null,
        Action<string, VirtualModelRegistration>? registerVirtualModel = null,
        Action<string, ISkillSource>? registerSkillSource = null,
        Action<string, ICommandSource>? registerCommandSource = null,
        Action<string, string, Type, Func<string, object>>? registerWorkspaceService = null,
        Func<
            string,
            string,
            Type,
            string,
            CancellationToken,
            WorkspaceServiceRegistry.WorkspaceServiceLease
        >? acquireWorkspaceService = null
    )
    {
        Id = id;
        _settings = settings;
        _secrets = secrets;
        Log = log;
        _events = new ObservableEvents(events, id, log);
        _register = register;
        _registerService = registerService;
        _subscribeFileChanged = subscribeFileChanged;
        _registerToolResultRenderer = registerToolResultRenderer;
        _registerStatusWidget = registerStatusWidget;
        _registerMcpServer = registerMcpServer;
        _registerTool = registerTool;
        _registerWriteVerifier = registerWriteVerifier;
        _registerModelProvider = registerModelProvider;
        _registerModelMiddleware = registerModelMiddleware;
        _registerVirtualModel = registerVirtualModel;
        _registerSkillSource = registerSkillSource;
        _registerCommandSource = registerCommandSource;
        _registerWorkspaceService = registerWorkspaceService;
        _acquireWorkspaceService = acquireWorkspaceService;
        _findService = findService;
        _findCoreService = findCoreService;
    }

    public string Id { get; }

    public IExtensionSettings Settings
    {
        get
        {
            ThrowIfDisposed();
            return _settings;
        }
    }

    public IExtensionSecrets Secrets
    {
        get
        {
            ThrowIfDisposed();
            return _secrets;
        }
    }

    public IExtensionLog Log { get; }

    public IObservable<AgentEvent> Events
    {
        get
        {
            ThrowIfDisposed();
            return _events;
        }
    }

    /// <summary>
    /// Marks the context disposed: the extension was unloaded, so a host call from a handler that
    /// outlived it throws <see cref="ObjectDisposedException"/> instead of reaching a host that has
    /// already reported the extension gone (design D3, sign-off Q2). The id and the log stay
    /// usable — a diagnostic must never become the failure it describes.
    /// </summary>
    internal void MarkDisposed() => _disposed = true;

    public void Register(IHookHandler handler)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(handler);
        _register?.Invoke(Id, handler);
    }

    public void RegisterService(string name, IBackgroundService service)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(service);
        _registerService?.Invoke(Id, name, service);
    }

    public void SubscribeFileChanged(IFileChangedHandler handler, string? pathPattern = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(handler);
        _subscribeFileChanged?.Invoke(Id, handler, pathPattern);
    }

    public void RegisterToolResultRenderer(IToolResultRenderer renderer)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(renderer);
        _registerToolResultRenderer?.Invoke(Id, renderer);
    }

    public void RegisterStatusWidget(IStatusWidget widget)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(widget);
        _registerStatusWidget?.Invoke(Id, widget);
    }

    public void RegisterTool(ExtensionToolRegistration registration)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(registration);
        _registerTool?.Invoke(Id, registration);
    }

    public void RegisterWriteVerifier(WriteVerifierRegistration registration)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(registration);
        _registerWriteVerifier?.Invoke(Id, registration);
    }

    public void RegisterMcpServer(McpServerRegistration registration)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(registration);
        _registerMcpServer?.Invoke(Id, registration);
    }

    public void RegisterModelProvider(ExtensionModelProvider provider)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(provider);
        _registerModelProvider?.Invoke(Id, provider);
    }

    public void RegisterModelMiddleware(ExtensionModelMiddleware middleware)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(middleware);
        _registerModelMiddleware?.Invoke(Id, middleware);
    }

    public void RegisterVirtualModel(VirtualModelRegistration model)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(model);
        _registerVirtualModel?.Invoke(Id, model);
    }

    public void RegisterSkillSource(ISkillSource source)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        _registerSkillSource?.Invoke(Id, source);
    }

    public void RegisterCommandSource(ICommandSource source)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        _registerCommandSource?.Invoke(Id, source);
    }

    public void RegisterWorkspaceService<TService>(
        string name,
        IWorkspaceServiceFactory<TService> factory
    )
        where TService : notnull
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);
        // The service's type identity travels with the registration: a later acquire that names a
        // different type is refused with a teaching message by the host registry.
        _registerWorkspaceService?.Invoke(Id, name, typeof(TService), root => factory.Create(root));
    }

    public ValueTask<IWorkspaceServiceLease<TService>> AcquireWorkspaceServiceAsync<TService>(
        string name,
        string canonicalWorktreeRoot,
        CancellationToken ct = default
    )
        where TService : notnull
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalWorktreeRoot);
        if (_acquireWorkspaceService is null)
        {
            throw new InvalidOperationException(
                $"extension '{Id}': workspace service '{name}' is not registered; the host does not hold this extension's registry."
            );
        }

        // The wait slices inside the registry are synchronous (the flagship loop already runs the
        // same shape on the caller's thread): the ValueTask hands the lease back without a hop.
        return ValueTask.FromResult<IWorkspaceServiceLease<TService>>(
            new WorkspaceServiceLeaseView<TService>(
                _acquireWorkspaceService(Id, name, typeof(TService), canonicalWorktreeRoot, ct)
            )
        );
    }

    /// <summary>
    /// The typed view of one root's lease: the host validated the service type at acquire, so the
    /// value cast is exact.
    /// </summary>
    private sealed class WorkspaceServiceLeaseView<TService>(
        WorkspaceServiceRegistry.WorkspaceServiceLease lease
    ) : IWorkspaceServiceLease<TService>
        where TService : notnull
    {
        public TService Value => (TService)lease.Value;

        public string WorktreeRoot => lease.WorktreeRoot;

        public void Dispose() => lease.Dispose();
    }

    public bool TryGetService(string name, out IBackgroundService? service)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        service = _findService?.Invoke(name);
        return service is not null;
    }

    public bool TryGetCoreService<T>(string name, out T? service)
        where T : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        service = _findCoreService?.Invoke(name) as T;
        return service is not null;
    }

    /// <summary>
    /// The one observable an extension's subscriptions go through: it forwards to the host's
    /// <see cref="SessionEventStream"/> under this extension's id, so completion, drops and the
    /// error isolation are the stream's. A host that wired no stream (a bare context in a test, or
    /// a host that does not carry run events) warns once and delivers nothing, the way a missing
    /// <c>core/file-bus</c> handle does.
    /// </summary>
    private sealed class ObservableEvents(
        SessionEventStream? stream,
        string extensionId,
        IExtensionLog log
    ) : IObservable<AgentEvent>
    {
        private int _warned;

        public IDisposable Subscribe(IObserver<AgentEvent> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            if (stream is not null)
            {
                return stream.Subscribe(extensionId, observer);
            }

            if (Interlocked.Exchange(ref _warned, 1) == 0)
            {
                log.Warn(
                    $"extension '{extensionId}' subscribed to the event stream, but this host wired no stream; the subscription will not receive events."
                );
            }

            return EmptySubscription.Instance;
        }
    }

    private sealed class EmptySubscription : IDisposable
    {
        public static readonly EmptySubscription Instance = new();

        public void Dispose() { }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                Id,
                $"extension '{Id}' was unloaded; its host context is disposed."
            );
        }
    }
}
