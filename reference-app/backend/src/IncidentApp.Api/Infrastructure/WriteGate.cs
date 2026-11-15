namespace IncidentApp.Api.Infrastructure;

/// <summary>
/// Serialises all state-changing work. This guarantees that two simultaneous identical transitions cannot both succeed
/// (spec section 6, optimistic concurrency) and keeps the SQLite fallback free of writer contention.
/// </summary>
public sealed class WriteGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await work();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task RunAsync(Func<Task> work) => RunAsync(async () =>
    {
        await work();
        return 0;
    });
}
