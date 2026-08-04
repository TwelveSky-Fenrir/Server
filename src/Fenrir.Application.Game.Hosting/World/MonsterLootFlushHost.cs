using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class MonsterLootFlushHost : BackgroundService
{
    private const string MeterName = "Fenrir.GameServer.MonsterLootFlush";

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> PersistenceFaults = Meter.CreateCounter<long>(
        "fenrir.monster_loot.money_grant.persistence.faults", "{grant}",
        "Money grants whose idempotent credit result could not be read and await reconciliation.");

    private static readonly Counter<long> ReconciliationHolds = Meter.CreateCounter<long>(
        "fenrir.monster_loot.money_grant.reconciliation.holds", "{grant}",
        "Money grants retained for reconciliation with their stable correlation identifier.");

    private readonly ConcurrentDictionary<Guid, FaultHeldMoneyGrant> _faultHeldGrants = new();
    private readonly ICharacterRepository _characters;
    private readonly ILogger<MonsterLootFlushHost> _logger;
    private readonly ObservableGauge<long> _faultHeldBacklog;
    private readonly IMonsterMoneyGrantRepository _monsterMoneyGrants;
    private readonly ObservableGauge<long> _pendingBacklog;
    private readonly ZoneRegistry _zones;

    public MonsterLootFlushHost(
        ZoneRegistry zones,
        ICharacterRepository characters,
        IMonsterMoneyGrantRepository monsterMoneyGrants,
        ILogger<MonsterLootFlushHost> logger)
    {
        _zones = zones;
        _characters = characters;
        _monsterMoneyGrants = monsterMoneyGrants;
        _logger = logger;
        _pendingBacklog = Meter.CreateObservableGauge<long>(
            "fenrir.monster_loot.money_grant.pending.backlog", PendingGrantCount,
            "{grant}", "Money grants waiting for their first persistence attempt.");
        _faultHeldBacklog = Meter.CreateObservableGauge<long>(
            "fenrir.monster_loot.money_grant.reconciliation.backlog", FaultHeldGrantCount,
            "{grant}", "Money grants withheld from retry until their commit outcome is reconciled.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        var zoneList = _zones.Zones.ToArray();
        var wake = new Task[zoneList.Length];
        for (var i = 0; i < zoneList.Length; i++)
            wake[i] = zoneList[i].WaitForMoneyGrantAsync(stoppingToken);

        var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

                var candidates = new Task[wake.Length + 1];
                Array.Copy(wake, candidates, wake.Length);
                candidates[wake.Length] = timerTick;
                var woken = await Task.WhenAny(candidates).ConfigureAwait(false);

                if (ReferenceEquals(woken, timerTick))
                {
                    timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                    continue;
                }

                for (var i = 0; i < wake.Length; i++)
                {
                    if (!ReferenceEquals(wake[i], woken))
                        continue;

                    wake[i] = zoneList[i].WaitForMoneyGrantAsync(stoppingToken);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        await FlushAllZonesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task FlushAllZonesAsync(CancellationToken stoppingToken)
    {
        await ReconcileFaultHeldGrantsAsync(stoppingToken).ConfigureAwait(false);

        foreach (var zone in _zones.Zones)
        {
            var grants = zone.DrainPendingMoneyGrants();
            if (grants.Count == 0)
                continue;

            foreach (var grant in grants)
            {
                if (grant.PersistenceMode == MoneyGrantPersistenceMode.CharacterAdjustment)
                {
                    await PersistCharacterAdjustmentAsync(zone.MapId, grant, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (grant.PersistenceMode != MoneyGrantPersistenceMode.MonsterLootIdempotent)
                    throw new ArgumentOutOfRangeException(nameof(grant.PersistenceMode), grant.PersistenceMode,
                        "Unknown queued money-grant persistence mode.");

                if (_faultHeldGrants.ContainsKey(grant.CorrelationId))
                {
                    _logger.LogWarning(
                        "Zone {MapId}: skipped duplicate fault-held {Amount}-money grant for character {CharacterId} " +
                        "(CorrelationId={CorrelationId}); its stable correlation is being reconciled",
                        zone.MapId, grant.Amount, grant.CharacterId, grant.CorrelationId);
                    continue;
                }

                try
                {
                    var result = await _monsterMoneyGrants
                        .ApplyIdempotentAsync(grant.CorrelationId, grant.CharacterId, grant.Amount, stoppingToken)
                        .ConfigureAwait(false);
                    EnsureAppliedOrAlreadyApplied(result, grant.CorrelationId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    HoldForReconciliation(zone.MapId, grant, ex);
                }
            }
        }
    }

    private async Task PersistCharacterAdjustmentAsync(short mapId, PendingMoneyGrant grant,
        CancellationToken stoppingToken)
    {
        try
        {
            await _characters.AdjustMoneyAsync(grant.CharacterId, grant.Amount, 0, stoppingToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Zone {MapId}: failed to persist a {Amount}-money grant for character {CharacterId}",
                mapId, grant.Amount, grant.CharacterId);
        }
    }

    private async Task ReconcileFaultHeldGrantsAsync(CancellationToken stoppingToken)
    {
        foreach (var (correlationId, held) in _faultHeldGrants)
        {
            try
            {
                var grant = held.Grant;
                var result = await _monsterMoneyGrants
                    .ApplyIdempotentAsync(correlationId, grant.CharacterId, grant.Amount, stoppingToken)
                    .ConfigureAwait(false);
                EnsureAppliedOrAlreadyApplied(result, correlationId);

                if (!_faultHeldGrants.TryRemove(correlationId, out _))
                    continue;

                _logger.LogInformation(
                    "Zone {MapId}: reconciled {Amount}-money grant for character {CharacterId} " +
                    "(CorrelationId={CorrelationId}, Result={Result}, HeldFor={HeldFor})",
                    held.MapId, grant.Amount, grant.CharacterId, correlationId,
                    result.WasApplied ? "Applied" : "AlreadyApplied", DateTimeOffset.UtcNow - held.HeldAtUtc);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Zone {MapId}: reconciliation still cannot determine the {Amount}-money grant for character " +
                    "{CharacterId} (CorrelationId={CorrelationId}); it remains fault-held",
                    held.MapId, held.Grant.Amount, held.Grant.CharacterId, correlationId);
            }
        }
    }

    private static void EnsureAppliedOrAlreadyApplied(MonsterMoneyGrantResultDto result, Guid correlationId)
    {
        if (result.WasApplied == result.WasAlreadyApplied)
            throw new InvalidOperationException(
                $"Monster-money grant {correlationId} returned an invalid idempotency result.");
    }

    private long PendingGrantCount()
    {
        return _zones.Zones.Sum(static zone => (long)zone.PendingMoneyGrantCount);
    }

    private long FaultHeldGrantCount()
    {
        return _faultHeldGrants.Count;
    }

    private void HoldForReconciliation(short mapId, PendingMoneyGrant grant, Exception exception)
    {
        if (!_faultHeldGrants.TryAdd(grant.CorrelationId,
                new FaultHeldMoneyGrant(mapId, grant, DateTimeOffset.UtcNow)))
            return;

        var tags = new TagList { { "map", mapId } };
        PersistenceFaults.Add(1, tags);
        ReconciliationHolds.Add(1, tags);

        _logger.LogCritical(exception,
            "Zone {MapId}: the {Amount}-money grant for character {CharacterId} (CorrelationId={CorrelationId}) " +
            "was withheld for reconciliation because its idempotent result could not be read. The same correlation " +
            "will be replayed through usp_MonsterMoneyGrant_ApplyIdempotent; that operation prevents duplicate " +
            "credit for this correlation only. The fault-held backlog is {FaultHeldBacklog}",
            mapId, grant.Amount, grant.CharacterId, grant.CorrelationId, _faultHeldGrants.Count);
    }

    private readonly record struct FaultHeldMoneyGrant(
        short MapId,
        PendingMoneyGrant Grant,
        DateTimeOffset HeldAtUtc);
}
