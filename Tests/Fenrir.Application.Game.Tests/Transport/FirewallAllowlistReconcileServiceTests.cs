using Fenrir.Network.Dispatch.FloodProtection;

namespace Fenrir.Application.Game.Tests.Transport;

public sealed class FirewallAllowlistReconcileServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ReconcileOnceAsync_InvokesTheInjectedDelegate_ForwardingTheToken()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        CancellationToken observedToken = default;

        var service = new FirewallAllowlistReconcileService(ct =>
        {
            Interlocked.Increment(ref calls);
            observedToken = ct;
            return ValueTask.CompletedTask;
        });

        await service.ReconcileOnceAsync(cts.Token);

        Assert.Equal(1, calls);
        Assert.Equal(cts.Token, observedToken);
    }

    [Fact]
    public async Task RunAsync_RunsReconcileOnceAtStartup_BeforeAnyTick_ThenStopsCleanlyOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        var firstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new FirewallAllowlistReconcileService(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                firstCall.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMinutes(10));

        var runTask = service.RunAsync(cts.Token);

        await firstCall.Task.WaitAsync(TestTimeout);
        Assert.Equal(1, calls);

        await cts.CancelAsync();
        await runTask.WaitAsync(TestTimeout);

        Assert.True(runTask.IsCompletedSuccessfully);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_ReconcileThrows_LoopSurvivesAndKeepsReconciling()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        var reachedThird = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new FirewallAllowlistReconcileService(_ =>
        {
            var n = Interlocked.Increment(ref calls);

            if (n == 1)
                throw new InvalidOperationException("transient reconcile failure");

            if (n >= 3)
                reachedThird.TrySetResult();

            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(20));

        var runTask = service.RunAsync(cts.Token);

        await reachedThird.Task.WaitAsync(TestTimeout);
        Assert.True(calls >= 3);

        await cts.CancelAsync();
        await runTask.WaitAsync(TestTimeout);
        Assert.True(runTask.IsCompletedSuccessfully);
    }

    [Fact]
    public void DefaultInterval_IsTheCorrected120SecondCadence()
    {
        Assert.Equal(TimeSpan.FromSeconds(120), FirewallAllowlistReconcileService.DefaultInterval);

        var service = new FirewallAllowlistReconcileService(_ => ValueTask.CompletedTask);
        Assert.Equal(FirewallAllowlistReconcileService.DefaultInterval, service.Interval);
    }
}
