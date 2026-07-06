using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Hosting.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribeBankTaxSweepFlushHostTests
{
    [Fact]
    public async Task FlushOnceAsync_WithNoZoneHavingADueSweep_ForwardsNothing()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1, 2]);
        var gateway = new RecordingGateway();
        var host = new TribeBankTaxSweepFlushHost(registry, gateway, NullLogger<TribeBankTaxSweepFlushHost>.Instance);

        await host.FlushOnceAsync(CancellationToken.None);

        Assert.Empty(gateway.Sweeps);
    }

    [Fact]
    public async Task FlushOnceAsync_ForwardsEachHostedZonesDueSweep_ToTheGateway()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1, 2]);
        registry[1].CreditNpcServiceTribeTax(0, 1000); // 10
        registry[2].CreditMonsterKillTribeTax(1, 1000); // 90

        registry[1].Tick(TimeSpan.FromMinutes(10));
        registry[2].Tick(TimeSpan.FromMinutes(10));

        var gateway = new RecordingGateway();
        var host = new TribeBankTaxSweepFlushHost(registry, gateway, NullLogger<TribeBankTaxSweepFlushHost>.Instance);

        await host.FlushOnceAsync(CancellationToken.None);

        Assert.Equal(2, gateway.Sweeps.Count);
        var zone1Sweep = Assert.Single(gateway.Sweeps, s => s.MapId == 1).Payload;
        var zone2Sweep = Assert.Single(gateway.Sweeps, s => s.MapId == 2).Payload;
        Assert.Equal(10, zone1Sweep.Tribe0);
        Assert.Equal(90, zone2Sweep.Tribe1);

        // Already drained -- a second flush pass finds nothing new until each zone's own next 10-minute mark.
        gateway.Sweeps.Clear();
        await host.FlushOnceAsync(CancellationToken.None);
        Assert.Empty(gateway.Sweeps);
    }

    [Fact]
    public async Task FlushOnceAsync_OneZonesGatewayFailure_NeverBlocksAnotherZonesSweep()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1, 2]);
        registry[1].CreditNpcServiceTribeTax(0, 1000);
        registry[2].CreditNpcServiceTribeTax(0, 1000);
        registry[1].Tick(TimeSpan.FromMinutes(10));
        registry[2].Tick(TimeSpan.FromMinutes(10));

        var throwingGateway = new RecordingGateway { ThrowOnSweep = true };
        var host = new TribeBankTaxSweepFlushHost(registry, throwingGateway,
            NullLogger<TribeBankTaxSweepFlushHost>.Instance);

        // Must not throw -- a failed send is logged and simply loses that window's tax (fire-and-forget by contract).
        await host.FlushOnceAsync(CancellationToken.None);

        Assert.Empty(throwingGateway.Sweeps);
    }

    [Fact]
    public async Task LoggingOnlyGateway_NeverThrows_ForEmptyOrNonEmptyPayloads()
    {
        var gateway = new LoggingOnlyTribeBankTaxSweepGateway(
            NullLogger<LoggingOnlyTribeBankTaxSweepGateway>.Instance);

        await gateway.SweepAsync(1, TribeBankTaxSweepPayload.Empty, CancellationToken.None);
        await gateway.SweepAsync(1, new TribeBankTaxSweepPayload(10, 20, 30, 40), CancellationToken.None);
    }

    private sealed class RecordingGateway : ITribeBankTaxSweepGateway
    {
        public readonly List<(short MapId, TribeBankTaxSweepPayload Payload)> Sweeps = [];
        public bool ThrowOnSweep { get; set; }

        public Task SweepAsync(short mapId, TribeBankTaxSweepPayload payload, CancellationToken ct)
        {
            if (ThrowOnSweep)
                throw new InvalidOperationException("simulated persistent-store failure");

            Sweeps.Add((mapId, payload));
            return Task.CompletedTask;
        }
    }
}
