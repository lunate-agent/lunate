namespace Lunate.Ai.Decisions;

internal interface IDecisionClient
{
    Task<DecisionResult> GetDecisionAsync(
        DecisionRequest request,
        DecisionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    object? GetService(Type serviceType);
}

internal class DelegatingDecisionClient(IDecisionClient innerClient) : IDecisionClient
{
    protected IDecisionClient InnerClient { get; } =
        innerClient ?? throw new ArgumentNullException(nameof(innerClient));

    public virtual Task<DecisionResult> GetDecisionAsync(
        DecisionRequest request,
        DecisionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => InnerClient.GetDecisionAsync(request, options, cancellationToken);

    public virtual object? GetService(Type serviceType) => InnerClient.GetService(serviceType);
}
