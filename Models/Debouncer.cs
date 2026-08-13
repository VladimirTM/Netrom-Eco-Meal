namespace Netrom_Eco_Meal.Models;

// Cancels any pending call before scheduling a new one, so rapid triggers (keystrokes, quick clicks)
// collapse into a single invocation instead of racing on Blazor Server's one DbContext per circuit.
// The gate below also serializes the actual action() calls: cancelling only stops a pending delay, so
// without it two delayMs=0 triggers (e.g. rapid pagination clicks) could still run concurrently.
public class Debouncer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;

    public async Task DebounceAsync(Func<Task> action, int delayMs = 0)
    {
        var previous = _cts;
        var cts = new CancellationTokenSource();
        _cts = cts;
        previous?.Cancel();
        previous?.Dispose();

        if (delayMs > 0)
        {
            try
            {
                await Task.Delay(delayMs, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        if (cts.IsCancellationRequested)
            return;

        await _gate.WaitAsync();
        try
        {
            // A newer call may have superseded this one while we waited for the gate.
            if (cts.IsCancellationRequested)
                return;

            await action();
        }
        finally
        {
            _gate.Release();
        }
    }
}
