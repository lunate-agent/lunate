namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// Read-only handle to the core mutation queue: schedules a read-modify-write on a canonical
/// absolute path so concurrent mutations of one file apply one after another.
/// </summary>
public interface IMutationQueue
{
    Task<T> RunAsync<T>(
        string path,
        Func<CancellationToken, Task<T>> mutation,
        CancellationToken cancellationToken
    );
}
