using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class PositionWriteBehindHost : BackgroundService, ICharacterWriteBehindFlusher
{
    private readonly ICharacterRepository _characters;
    private readonly SemaphoreSlim _disconnectRetrySignal = new(0, 1);

    private readonly PeriodicTimer _disconnectRetryTimer = new(WriteBehindFlusher<int>.DefaultInterval);
    private readonly WriteBehindFlusher<int> _flusher;

    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly ILogger<PositionWriteBehindHost> _logger;
    private readonly ICharacterLogoutStateRepository _logoutState;

    private readonly Dictionary<int, (CharacterProgressTvp Progress, CharacterPositionTvp Position,
            List<CharacterCostumeSlotTvp> Costumes, PlayerRuntimeState
            CapturedState, int CapturedWarPoint, int CapturedBloodCoin)>
        _pendingDisconnectRetries = new();

    private readonly ZoneRegistry _zones;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, ICharacterRepository characters,
        ICharacterLogoutStateRepository logoutState, ProgressWriteBehindHost progress,
        ILogger<PositionWriteBehindHost> logger)
    {
        _zones = zones;
        _characters = characters;
        _logoutState = logoutState;
        _logger = logger;

        _flusher = new WriteBehindFlusher<int>(
            dirtyTracker,
            async (dirty, ct) =>
            {
                await _flushGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    // Progress and Position are guarded by their own independent game.Characters columns
                    // (ProgressFlushSequence / PositionFlushSequence, Migrations/048) precisely so these two
                    // writes can never race each other on the same idempotency guard column -- no ordering
                    // or claim/defer dance needed between them any more. See usp_Character_PersistBatch /
                    // usp_Character_PersistProgressBatch's own header comments for the guard split.
                    await progress.FlushAsync(dirty, ct).ConfigureAwait(false);

                    var rows = new List<CharacterPositionTvp>(dirty.Count);
                    var logoutRows = new List<CharacterLogoutStateTvp>(dirty.Count);

                    foreach (var (characterId, flags) in dirty)
                    {
                        if (!zones.TryGetPlayer(characterId, out var state))
                            continue;

                        logoutRows.Add(new CharacterLogoutStateTvp(characterId, state.FlushSequence, state.MapId,
                            (int)state.PosX, (int)state.PosY, (int)state.PosZ, state.Life, state.Mana));

                        if ((flags & DirtyFlags.Position) == 0)
                            continue;

                        rows.Add(new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
                            state.PosY, state.PosZ, state.Heading));
                    }

                    await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
                    await logoutState.PersistBatchAsync(logoutRows, ct).ConfigureAwait(false);
                }
                finally
                {
                    _flushGate.Release();
                }
            },
            onFlushError: ex => logger.LogError(ex, "Character write-behind flush failed (position and/or progress)"));
    }

    public void RequestImmediateFlush()
    {
        _flusher.RequestImmediateFlush();
        ReleaseDisconnectRetrySignal();
    }

    public async ValueTask FlushCharacterNowAsync(int characterId, CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_zones.TryGetPlayer(characterId, out var state))
            {
                _logger.LogDebug(
                    "FlushCharacterNowAsync({CharacterId}): character not found in any zone's live registry -- nothing to persist",
                    characterId);
                return;
            }

            await PersistFinalFlushAsync(state, ct).ConfigureAwait(false);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    public async ValueTask FlushCharacterSnapshotAsync(PlayerRuntimeState snapshot, CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PersistFinalFlushAsync(snapshot, ct).ConfigureAwait(false);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private async ValueTask PersistFinalFlushAsync(PlayerRuntimeState state, CancellationToken ct)
    {
        var characterId = state.CharacterId;
        var warPoint = state.WarPoint;
        var bloodCoin = state.BloodCoin;

        var progressRow = new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
                state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit, state.StatStr,
                state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints, state.ContributionPoints,
                state.Exp2, state.RebirthCount, state.EatLifePotion, state.EatManaPotion, state.EatStrPotion,
                state.EatDexPotion, state.EatElePotion, state.DropItemTime, state.M15PetLuckyBoxPity,
                state.MountGarage[MountPersistenceCodec.PersistedGarageSlot],
                MountPersistenceCodec.EncodeExpActivity(state.MountActivity, state.MountAccumulatedExp),
                MountPersistenceCodec.EncodePower(state.MountRolledAttributes),
                state.AnimalIndex, state.AnimalTime,
                state.VisibleState, state.SpecialState, state.UseOrnament ? 1 : 0,
                state.Title, state.Halo, state.TeacherPoint,
                warPoint - state.PersistedWarPoint, bloodCoin - state.PersistedBloodCoin,
                state.PetExpX2Time, state.AnimalAbsorbTime, state.AnimalAbsorbState, state.CostumeIndex,
                state.ProtectForHalo,
                state.BonusItemLevel,
                state.BonusItemValue,
                state.TribeNotifyScrollCount,
                state.TribeFourReturnAllowance,
                BottleSlotsCodec.Encode(state.BottleSlots),
                state.DrunkBottleIndex,
                state.AutoBuffTime,
                AutoBuffSkillCodec.Encode(state.AutoBuffSkill),
                state.RankPointDate,
                state.RankBuffType,
                state.AutoHuntPaidDayBudget,
                state.AutoHuntPaidMinuteBudget,
                state.BuffX2Time,
                state.PremiumExpireUtc,
                state.PetGrowth,
                state.PetActivity,
                RankPoint: state.RankPoint,
                CloakLuckyBoxPity: state.CloakLuckyBoxPity,
                CloakVariantBoxPity: state.CloakVariantBoxPity,
                MountVariantBoxPity: state.MountVariantBoxPity,
                ImproveItemValue: state.ImproveItemValue,
                AddItemValue: state.AddItemValue,
                HighItemValue: state.HighItemValue,
                TaiyanKeyTimer: state.TaiyanKeyTimer,
                ProtectForRefine: state.ProtectForRefine,
                ProtectForDestroy: state.ProtectForDestroy,
                ProtectForCostume: state.ProtectForCostume,
                ProtectForDestroy2: state.ProtectForDestroy2,
                LodRounds: state.LodRounds,
                StellarCoreExpireDate: StellarCoreExpireDateCodec.Encode(state.StellarCoreExpireDate),
                EliteDungeonTime: state.EliteDungeonTime,
                DungeonKeyTime: state.DungeonKeyTime,
                IvyHallTicketTime: state.IvyHallTicketTime,
                ScrollOfSeekersTime: state.ScrollOfSeekersTime,
                FightingGodForDestroy: state.FightingGodForDestroy,
                PetBagDate: state.PetBagDate,
                PlayTime1: state.PlayTime1,
                PlayTime3: state.PlayTime3,
                HsbStoneRewardClaimed: state.HsbStoneRewardClaimed ? 1 : 0,
                TowerCpMilestoneCounter: state.TowerCpMilestoneCounter,
                InventoryDate: state.InventoryDate,
                StoreDate: state.StoreDate,
                WarriorPill: state.WarriorPill,
                WarriorScroll: state.WarriorScroll,
                SilverTime: state.SilverTime,
                GoldTime: state.GoldTime,
                DoubleKillNumTime: state.DoubleKillNumTime,
                DoubleKillExpTime: state.DoubleKillExpTime,
                DoubleKillNumTime2: state.DoubleKillNumTime2,
                ProtectForDeath: state.ProtectForDeath);

        var positionRow = new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
            state.PosY, state.PosZ, state.Heading);

        var costumeRows = new List<CharacterCostumeSlotTvp>();
        CostumePersistenceCodec.AppendOccupiedSlots(costumeRows, characterId, state);

        try
        {
            await _characters.PersistFinalFlushAsync(progressRow, positionRow, costumeRows, ct)
                .ConfigureAwait(false);

            state.PersistedWarPoint = warPoint;
            state.PersistedBloodCoin = bloodCoin;

            var logoutRow = new CharacterLogoutStateTvp(characterId, state.FlushSequence, state.MapId,
                (int)state.PosX, (int)state.PosY, (int)state.PosZ, state.Life, state.Mana);
            await _logoutState.PersistBatchAsync([logoutRow], ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _pendingDisconnectRetries[characterId] =
                (progressRow, positionRow, costumeRows, state, warPoint, bloodCoin);
            _logger.LogError(ex,
                "Failed to persist final Position/Vitals/Progression state for character {CharacterId} on disconnect -- queued for retry until it succeeds",
                characterId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _flusher.DisposeAsync().ConfigureAwait(false);
        _flushGate.Dispose();
        _disconnectRetryTimer.Dispose();
        _disconnectRetrySignal.Dispose();
    }

    private void ReleaseDisconnectRetrySignal()
    {
        try
        {
            _disconnectRetrySignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private async Task RetryPendingDisconnectFlushesAsync(CancellationToken ct)
    {
        try
        {
            var tickTask = _disconnectRetryTimer.WaitForNextTickAsync(ct).AsTask();
            var signalTask = _disconnectRetrySignal.WaitAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(tickTask, signalTask).ConfigureAwait(false);

                if (ct.IsCancellationRequested)
                    break;

                if (completed == tickTask)
                    tickTask = _disconnectRetryTimer.WaitForNextTickAsync(ct).AsTask();
                else
                    signalTask = _disconnectRetrySignal.WaitAsync(ct);

                await DrainPendingDisconnectRetriesAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await DrainPendingDisconnectRetriesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask DrainPendingDisconnectRetriesAsync(CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_pendingDisconnectRetries.Count == 0)
                return;

            foreach (var characterId in _pendingDisconnectRetries.Keys.ToArray())
            {
                var (progressRow, positionRow, costumeRows, capturedState, capturedWarPoint, capturedBloodCoin) =
                    _pendingDisconnectRetries[characterId];

                if (_zones.TryGetPlayer(characterId, out var liveState) && !ReferenceEquals(liveState, capturedState))
                {
                    _pendingDisconnectRetries.Remove(characterId);
                    _logger.LogWarning(
                        "Discarding stale queued disconnect-time flush for character {CharacterId} -- a new session's live state now supersedes the queued snapshot",
                        characterId);
                    continue;
                }

                try
                {
                    await _characters.PersistFinalFlushAsync(progressRow, positionRow, costumeRows, ct)
                        .ConfigureAwait(false);

                    capturedState.PersistedWarPoint = Math.Max(capturedState.PersistedWarPoint, capturedWarPoint);
                    capturedState.PersistedBloodCoin = Math.Max(capturedState.PersistedBloodCoin, capturedBloodCoin);

                    _pendingDisconnectRetries.Remove(characterId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Retry of the queued disconnect-time flush for character {CharacterId} failed again -- will retry next cycle",
                        characterId);
                }
            }
        }
        finally
        {
            _flushGate.Release();
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(_flusher.RunAsync(stoppingToken), RetryPendingDisconnectFlushesAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _flusher.DisposeAsync().ConfigureAwait(false);
    }
}
