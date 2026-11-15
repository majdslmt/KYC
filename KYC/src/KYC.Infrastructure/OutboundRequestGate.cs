namespace KYC.Infrastructure;

// One singleton per application process; bounds all outgoing requests across all endpoints.
public sealed class OutboundRequestGate
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Queue<DateTimeOffset> _sentAt = new();
    private readonly TimeProvider _clock;
    private readonly int _limit;
    private readonly TimeSpan _window;

    public OutboundRequestGate() : this(TimeProvider.System, 30, TimeSpan.FromMinutes(1)) { }

    /// <summary>
    /// Configurable clock and limits support deterministic tests without sleeping.
    /// Production defaults to 30 outbound requests per rolling 60 seconds.
    /// </summary>
    public OutboundRequestGate(TimeProvider clock, int limit, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        _clock = clock;
        _limit = limit;
        _window = window;
    }

    /// <summary>
    /// Grants a permit only when fewer than the configured number of requests
    /// have been recorded during the rolling window. Supports cancellation.
    /// </summary>
    public async Task WaitForPermitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan delay;
            await _mutex.WaitAsync(cancellationToken);
            try
            {
                var now = _clock.GetUtcNow();
                // Old timestamps no longer count toward the rolling limit.
                while (_sentAt.Count > 0 && now - _sentAt.Peek() >= _window)
                    _sentAt.Dequeue();
                // Record the permit while holding the mutex, preventing callers
                // from exceeding the quota during simultaneous requests.
                if (_sentAt.Count < _limit)
                {
                    _sentAt.Enqueue(now);
                    return;
                }
                delay = _window - (now - _sentAt.Peek());
            }
            finally
            {
                _mutex.Release();
            }
            // Do not hold the mutex while waiting: other requests may proceed.
            await Task.Delay(delay, _clock, cancellationToken);
        }
    }
}
