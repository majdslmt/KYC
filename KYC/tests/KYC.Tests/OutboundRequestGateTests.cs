using KYC.Infrastructure;

namespace KYC.Tests;

public sealed class OutboundRequestGateTests
{
    [Fact]
    public async Task First_request_receives_permit_immediately()
    {
        var gate = new OutboundRequestGate(TimeProvider.System, 1, TimeSpan.FromMinutes(1));
        await gate.WaitForPermitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Exhausted_gate_honors_cancellation()
    {
        var gate = new OutboundRequestGate(TimeProvider.System, 1, TimeSpan.FromMinutes(1));
        await gate.WaitForPermitAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.WaitForPermitAsync(cts.Token));
    }

    [Fact]
    public async Task Allows_another_request_after_window_expires()
    {
        var gate = new OutboundRequestGate(TimeProvider.System, 1, TimeSpan.FromMilliseconds(25));
        await gate.WaitForPermitAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await gate.WaitForPermitAsync(cts.Token);
    }
}
