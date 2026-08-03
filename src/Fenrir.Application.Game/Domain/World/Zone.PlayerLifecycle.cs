using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const short CharacterDeathEventCode = 1;

    private const short DeathExperienceLossEventCode = 2;

    private const byte ExperienceLossOutcome = 0;

    private const byte ContributionPointsLossOutcome = 1;

    private const int RestActionSort = 0;

    private const int SkillEffectConfirmActionSort = 1;

    private const int HolyShieldSkillId = 82;

    private const short HolyShieldCooldownZoneId = 124;

    private const int ContributionPointStatSort = 3;

    private const int CharacterHpStatSort = 10;

    private const int ManaRecoveredAvatarChangeInfoSort = 9;

    private const int RegularWarAfkResetSortCode = 2;

    private const int ImplausibleMoveDisconnectThreshold = 5;

    internal const string RegularWarAfkCheckerSenderName = "AFK Checker";

    private static readonly TimeSpan HolyShieldReapplyCooldown = TimeSpan.FromSeconds(10);

    internal static readonly ItemLinkInfo RegularWarAfkCheckerLink =
        new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    private readonly List<int> _buffStateNeighborScratch = [];

    private readonly SemaphoreSlim _deathEventLogSignal = new(0, int.MaxValue);

    private readonly List<int> _deathNeighborScratch = [];

    private readonly List<int> _enterNeighborScratch = [];

    private readonly List<int> _healTargetNeighborScratch = [];

    private readonly List<int> _moveNeighborScratch = [];

    private readonly ConcurrentQueue<PendingDeathEventLog> _pendingDeathEventLogs = new();

    private readonly List<int> _rebroadcastNeighborScratch = [];

    private readonly List<int> _reviveNeighborScratch = [];

    private void QueueDeathEventLog(short eventCode, int characterId, byte? outcome, string? payload)
    {
        _pendingDeathEventLogs.Enqueue(new PendingDeathEventLog(eventCode, characterId, options.ShardId, outcome,
            payload));
        _deathEventLogSignal.Release();
    }

    public Task WaitForDeathEventLogAsync(CancellationToken ct)
    {
        return _deathEventLogSignal.WaitAsync(ct);
    }

    public IReadOnlyList<PendingDeathEventLog> DrainPendingDeathEventLogs()
    {
        if (_pendingDeathEventLogs.IsEmpty)
            return [];

        List<PendingDeathEventLog>? entries = null;
        while (_pendingDeathEventLogs.TryDequeue(out var entry))
            (entries ??= []).Add(entry);

        return (IReadOnlyList<PendingDeathEventLog>?)entries ?? [];
    }

    private void RebroadcastAvatars()
    {
        foreach (var (characterId, state) in _players)
        {
            if (_clock - state.LastAvatarRebroadcastAt < SimulationClock.AvatarRebroadcastInterval)
                continue;

            if (state.IsDead || IsHiding(state))
            {
                state.LastAvatarRebroadcastAt = _clock;
                continue;
            }

            _rebroadcastNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_rebroadcastNeighborScratch, state.CurrentCell, characterId, state.PosX,
                state.PosY, state.PosZ);
            BroadcastAvatarAction(_rebroadcastNeighborScratch, state);
        }
    }

    private void HandleEnter(int characterId, PlayerEnterData data)
    {
        _duelRegistry.ForceClearOnZoneEntry(characterId);

        var state = new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = data.Session,
            Name = data.Name,
            Tribe = data.Tribe,
            Gender = data.Gender,
            HeadType = data.HeadType,
            FaceType = data.FaceType,
            Level = data.Level,
            MapId = data.MapId,
            PosX = data.PosX,
            PosY = data.PosY,
            PosZ = data.PosZ,
            Heading = data.Heading,
            PetActionLocationX = data.PosX,
            PetActionLocationY = data.PosY,
            PetActionLocationZ = data.PosZ,
            ActionSort = data.ActionSort,
            ActionSkillNumber = data.ActionSkillNumber,
            ActionSkillGradeNum1 = data.ActionSkillGradeNum1,
            ActionSkillGradeNum2 = data.ActionSkillGradeNum2,
            PetActionSort = data.PetActionSort,
            PetActionFront = data.PetActionFront,
            PetActionTargetLocationX = data.PetActionTargetLocationX,
            PetActionTargetLocationY = data.PetActionTargetLocationY,
            PetActionTargetLocationZ = data.PetActionTargetLocationZ,
            Life = data.Life,
            MaxLife = data.MaxLife,
            Mana = data.Mana,
            MaxMana = data.MaxMana,
            FlushSequence = data.FlushSequence,
            LastMoveUtc = DateTime.UtcNow,
            LastOneSecondGateTick = RawLogicTick,
            LastAvatarRebroadcastAt = _clock,
            IsDead = data.IsDead,
            TicksSinceDeath = data.TicksSinceDeath,
            ReviveHackFlag = data.ReviveHackFlag,
            CanUseConsumables = data.CanUseConsumables,
            DeathSubCounter = data.DeathSubCounter,
            StatVit = data.StatVit,
            StatStr = data.StatStr,
            StatInt = data.StatInt,
            StatDex = data.StatDex,
            StatPoints = data.StatPoints,
            Title = data.Title,
            Halo = data.Halo,
            RebirthCount = data.RebirthCount,
            Experience = data.Experience,
            ContributionPoints = data.ContributionPoints,
            TeacherPoint = data.TeacherPoint,
            Level2 = data.Level2,
            Exp2 = data.Exp2,
            Zone241Time = data.Zone241Time,
            IsMuted = data.IsMuted,
            GuildId = data.GuildId,
            GuildName = data.GuildName,
            GuildRoleDb = data.GuildRoleDb,
            GuildCallName = data.GuildCallName,
            GuildBuffType = data.GuildBuffType,
            GuildBuffActive = data.GuildBuffActive,
            TribeRole = data.TribeRole,
            PreviousTribe = data.PreviousTribe,
            ZoneEntryAtZoneClock = _clock,
            KnownCashCatalogVersion = data.KnownCashCatalogVersion,
            DungeonInstanceRoundsRemaining = data.DungeonInstanceRoundsRemaining,
            HeroRankPoints = data.HeroRankPoints,
            EatLifePotion = data.EatLifePotion,
            EatManaPotion = data.EatManaPotion,
            EatStrPotion = data.EatStrPotion,
            EatDexPotion = data.EatDexPotion,
            EatElePotion = data.EatElePotion,
            DropItemTime = data.DropItemTime,
            ImproveItemValue = data.ImproveItemValue,
            AddItemValue = data.AddItemValue,
            HighItemValue = data.HighItemValue,
            TaiyanKeyTimer = data.TaiyanKeyTimer,
            WarPoint = data.WarPoint,
            PersistedWarPoint = data.WarPoint,
            BloodCoin = data.BloodCoin,
            PersistedBloodCoin = data.BloodCoin,
            PremiumExpireUtc = data.PremiumExpireUtc,
            BuffX2Time = data.BuffX2Time,
            AutoHuntPaidDayBudget = data.AutoHuntPaidDayBudget,
            AutoHuntPaidMinuteBudget = data.AutoHuntPaidMinuteBudget,
            StoreMoney = data.StoreMoney,
            BigMoney = data.BigMoney,
            InventoryDate = data.InventoryDate,
            StoreDate = data.StoreDate,
            PetBagDate = data.PetBagDate,
            PlayTime1 = data.PlayTime1,
            PlayTime3 = data.PlayTime3,
            HsbStoneRewardClaimed = data.HsbStoneRewardClaimed,
            TowerCpMilestoneCounter = data.TowerCpMilestoneCounter,
            WarriorPill = data.WarriorPill,
            WarriorScroll = data.WarriorScroll,
            SilverTime = data.SilverTime,
            GoldTime = data.GoldTime,
            DoubleKillNumTime = data.DoubleKillNumTime,
            DoubleKillExpTime = data.DoubleKillExpTime,
            DoubleKillNumTime2 = data.DoubleKillNumTime2,
            M15PetLuckyBoxPity = data.M15PetLuckyBoxPity,
            VisibleState = data.VisibleState,
            SpecialState = data.SpecialState,
            UseOrnament = data.UseOrnament,
            SourceIp = data.SourceIp,
            ProtectForDeath = data.ProtectForDeath
        };

        state.ResetVolatileAntiCheatCountersOnEntry(_clock);

        state.RecomputeSupportSkillTimeUpRatio();

        if (data.Items is { } items)
            state.Inventory.Seed(items);
        if (data.Stats is { } stats)
            state.Stats = stats;
        if (data.Skills is { } skills)
        {
            var builder = ImmutableDictionary.CreateBuilder<byte, LearnedSkill>();
            foreach (var skill in skills)
                builder[skill.SlotIndex] = new LearnedSkill(skill.SkillId, skill.Grade);
            state.LearnedSkills = builder.ToImmutable();
        }

        if (data.Hotkeys is { } hotkeys)
        {
            var hotkeyBuilder = ImmutableDictionary.CreateBuilder<(byte Page, byte Index), HotkeySlot>();
            foreach (var hotkey in hotkeys)
                hotkeyBuilder[(hotkey.Page, hotkey.KeyIndex)] =
                    new HotkeySlot((HotkeyBindingKind)hotkey.Value2, hotkey.Sort, hotkey.Value1);
            state.Hotkeys = hotkeyBuilder.ToImmutable();
        }

        if (data.FriendsBySlot is { } friends)
            foreach (var (slot, friendId) in friends)
                state.Friends[slot] = friendId;

        if (data.Buffs is { } buffs)
            buffs.Buff.CopyTo(state.Buffs.Buff, 0);

        state.TeacherCharacterId = data.TeacherCharacterId;
        state.StudentCharacterId = data.StudentCharacterId;

        state.QuestStepPermanent = data.QuestProgress.StepPermanent;
        state.QuestActiveFlag = data.QuestProgress.ActiveFlag;
        state.QuestSort = data.QuestProgress.QSort;
        state.QuestTargetPhase = data.QuestProgress.TargetPhase;
        state.QuestKillCounter = data.QuestProgress.KillCounter;
        state.MissionJoinWar = data.MissionJoinWar;
        state.MissionKillOtherTribe = data.MissionKillOtherTribe;
        state.MissionKillMonster = data.MissionKillMonster;
        state.MissionPlayTime = data.MissionPlayTime;
        state.AutoHuntEnabled = data.AutoHuntEnabled;
        state.AutoHuntConfig = data.AutoHuntConfig;
        state.AutoLifeRatio = data.AutoLifeRatio;
        state.AutoManaRatio = data.AutoManaRatio;
        state.PetGrowth = data.PetGrowth;
        state.PetActivity = data.PetActivity;
        state.PetExpX2Time = data.PetExpX2Time;
        state.LastSeenPetItemId = data.Items is { } petScanItems
            ? PetSlots.ResolveEquippedPetItemId(petScanItems)
            : 0;

        if (data.RuneSystem is { } runeSystem)
            state.RuneSystem = runeSystem;
        if (data.RuneSystemStat is { } runeSystemStat)
            state.RuneSystemStat = runeSystemStat;

        if (data.BottleSlots is { } bottleSlots)
            state.BottleSlots = bottleSlots;
        if (data.DrunkBottleIndex is { } drunkBottleIndex)
            state.DrunkBottleIndex = drunkBottleIndex;
        if (data.DrunkBottleTicksRemaining is { } drunkBottleTicksRemaining)
            state.DrunkBottleTicksRemaining = drunkBottleTicksRemaining;

        state.AutoBuffTime = data.AutoBuffTime;
        if (data.AutoBuffSkill is { } autoBuffSkill)
            state.AutoBuffSkill = autoBuffSkill;
        state.RankPointDate = data.RankPointDate;
        state.RankBuffType = data.RankBuffType;
        state.RankPoint = data.RankPoint;
        state.CloakLuckyBoxPity = data.CloakLuckyBoxPity;
        state.CloakVariantBoxPity = data.CloakVariantBoxPity;
        state.MountVariantBoxPity = data.MountVariantBoxPity;

        HydrateMountState(state, data);
        HydrateCostumeState(state, data);

        RecomputeAndPublish(state, true);

        var cell = _grid.CellOf(state.PosX, state.PosZ);
        state.CurrentCell = cell;

        if (!_players.TryAdd(characterId, state))
        {
            _players.TryGetValue(characterId, out var existing);

            if (existing is not null && !ReferenceEquals(existing.Session, state.Session))
            {
                logger.LogWarning(
                    "Character {CharacterId} entered zone {MapId} while a stale prior session was still tracked -- evicting the old session and adopting the newer one",
                    characterId, MapId);

                _grid.Remove(characterId, existing.CurrentCell);
                _players[characterId] = state;

                if (existing.Session is IZoneSession staleZoneSession)
                    staleZoneSession.CurrentZone = null;

                if (existing.Session is { } staleClientSession)
                    staleClientSession.Abort(DisconnectReason.Evicted);
            }
            else
            {
                logger.LogWarning(
                    "Character {CharacterId} entered zone {MapId} while already tracked -- ignoring duplicate Enter",
                    characterId, MapId);
                return;
            }
        }

        _grid.Add(characterId, cell, state.PosX, state.PosY, state.PosZ);

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        logger.LogInformation("Character {CharacterId} entered zone {MapId}", characterId, MapId);

        _enterNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_enterNeighborScratch, cell, characterId, state.PosX, state.PosY, state.PosZ);

        foreach (var otherId in _enterNeighborScratch)
            if (_players.TryGetValue(otherId, out var other) &&
                other.VisibleState != 0 &&
                IsVisibleAcrossDungeonInstance(other.DungeonInstanceId, state.DungeonInstanceId))
                SendAvatarAction(state.Session, other);

        BroadcastAvatarAction(_enterNeighborScratch, state);

        SendExistingMonstersTo(state);

        if (WrapCheckSpecialDestinationCatalog.IsInstancedDestination(MapId))
            TryEnterLegendsOfDarknessInstance(characterId);
        else if (IsZone241TypeZone)
            TryEnterZone241PersonalInstance(characterId);

        TryPublishPartyResyncRequest(characterId, state.Name);
    }

    private static void HydrateMountState(PlayerRuntimeState state, PlayerEnterData data)
    {
        const int slot = MountPersistenceCodec.PersistedGarageSlot;

        state.MountGarage = state.MountGarage.SetItem(slot, data.MountItemId);
        state.MountActivity = state.MountActivity.SetItem(slot, MountActivityExpCodec.Activity(data.MountExpActivity));
        state.MountAccumulatedExp =
            state.MountAccumulatedExp.SetItem(slot, MountActivityExpCodec.Exp(data.MountExpActivity));
        state.MountRolledAttributes =
            MountPowerCodec.WithSlotDigits(state.MountRolledAttributes, slot, data.MountPower);
        state.MountRolledAttributeTotal =
            state.MountRolledAttributeTotal.SetItem(slot, MountPowerCodec.DigitSum(data.MountPower));

        state.AnimalIndex = data.MountSlotIndex;
        state.AnimalTime = data.MountTime;
        state.AnimalNumber = MountPersistenceCodec.IsMounted(data.MountSlotIndex) ? data.MountItemId : 0;
        state.AnimalAbsorbTime = data.AnimalAbsorbTime;
        state.AnimalAbsorbState = data.AnimalAbsorbState;
    }

    private static void HydrateCostumeState(PlayerRuntimeState state, PlayerEnterData data)
    {
        if (data.CostumeWardrobe is { } wardrobe)
            state.CostumeWardrobe = wardrobe;
        if (data.CostumeDate is { } costumeDate)
            state.CostumeDate = costumeDate;
        if (data.CostumeExpireDate is { } costumeExpireDate)
            state.CostumeExpireDate = costumeExpireDate;

        state.CostumeIndex = CostumePersistenceCodec.NormalizeIndexOnLoad(data.CostumeIndex, state.CostumeWardrobe);
        state.CostumeNumber = CostumePersistenceCodec.ResolveWornNumber(state.CostumeIndex, state.CostumeWardrobe);
    }

    private void TryPublishPartyResyncRequest(int characterId, string avatarName)
    {
        if (_partyResyncRelayQueue is null || _partyRegistry.IsInParty(characterId))
            return;

        _partyResyncRelayQueue.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.Request, options.ShardId, characterId, avatarName, avatarName));
    }

    private void HandleLeave(int characterId, TaskCompletionSource<PlayerRuntimeState?>? snapshotSignal = null)
    {
        PlayerRuntimeState? state = null;

        try
        {
            if (!_players.TryRemove(characterId, out state))
                return;

            _grid.Remove(characterId, state.CurrentCell);

            logger.LogInformation("Character {CharacterId} left zone {MapId}", characterId, MapId);

            if (!state.IsMovingZone)
            {
                BreakPartyOnDisconnect(characterId, state.Name);

                if (characterShardLocations is not null)
                    _ = CleanupShardLocationAsync(characterId);
            }

            ClearTradeOnDisconnect(characterId);

            ClearAcceptedNegotiationsOnDisconnect(characterId);

            ClearDungeonInstanceOnDisconnect(state);
        }
        finally
        {
            snapshotSignal?.TrySetResult(state);
        }
    }

    private void BreakPartyOnDisconnect(int characterId, string disconnectingName)
    {
        var result = _partyRegistry.LeaveForDisconnect(characterId);

        switch (result.Kind)
        {
            case PartyDisconnectKind.NotInParty:
                return;

            case PartyDisconnectKind.LeaderDisbanded:
            {
                var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
                foreach (var memberId in result.MembersBeforeLeave)
                    if (memberId != characterId)
                        SendToCharacter(memberId, disbandNotice);
                return;
            }

            case PartyDisconnectKind.MemberLeft:
            {
                var leaveNotice = new PartyLeaveResponse { AvatarName = disconnectingName };
                foreach (var memberId in result.MembersBeforeLeave)
                    if (memberId != characterId)
                        SendToCharacter(memberId, leaveNotice);

                var roster = BuildPartyRoster(3, result.RemainingMembers);
                foreach (var memberId in result.RemainingMembers)
                    SendToCharacter(memberId, roster);
                return;
            }

            case PartyDisconnectKind.MemberLeftAndDisbanded:
            {
                var leaveNotice = new PartyLeaveResponse { AvatarName = disconnectingName };
                var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
                foreach (var memberId in result.MembersBeforeLeave)
                {
                    if (memberId == characterId)
                        continue;

                    SendToCharacter(memberId, leaveNotice);
                    SendToCharacter(memberId, disbandNotice);
                }

                return;
            }
        }
    }

    private void ClearTradeOnDisconnect(int characterId)
    {
        var result = _tradeRegistry.ClearForDisconnect(characterId);

        switch (result.Notification)
        {
            case TradeDisconnectNotification.Cancel:
                SendToCharacter(result.PartnerId, new TradeCancelResponse());
                return;

            case TradeDisconnectNotification.End:
                RestoreStagedBigMoney(characterId, result.SelfBigMoneyRestore);
                RestoreStagedBigMoney(result.PartnerId, result.PartnerBigMoneyRestore);
                SendToCharacter(result.PartnerId, new TradeEndResponse { Result = 1 });
                return;
        }
    }

    private void ClearAcceptedNegotiationsOnDisconnect(int characterId)
    {
        _duelRegistry.TryClearAcceptedForDisconnect(characterId, out _);
        _friendRegistry.TryClearAcceptedForDisconnect(characterId, out _);
    }

    private void RestoreStagedBigMoney(int characterId, int amount)
    {
        if (amount == 0)
            return;

        if (_players.TryGetValue(characterId, out var state))
        {
            state.BigMoney += amount;
            return;
        }

        if (_zoneRegistry is not null && _zoneRegistry.TryGetPlayerAndZone(characterId, out _, out var otherZone))
            otherZone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, BigMoneyDelta: amount));
    }

    private PartyRosterResponse BuildPartyRoster(int sort, IReadOnlyList<int> memberIds)
    {
        Span<string> names = ["", "", "", "", ""];
        for (var i = 0; i < memberIds.Count && i < 5; i++)
            if (TryFindPlayer(memberIds[i], out var member))
                names[i] = member.Name;

        return new PartyRosterResponse
        {
            Sort = sort,
            AvatarName01 = names[0],
            AvatarName02 = names[1],
            AvatarName03 = names[2],
            AvatarName04 = names[3],
            AvatarName05 = names[4]
        };
    }

    private void SendToCharacter<TPacket>(int characterId, in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        if (TryFindPlayer(characterId, out var member))
            member.Session.Send(packet);
    }

    private bool TryFindPlayer(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
    {
        if (_players.TryGetValue(characterId, out state))
            return true;

        return _zoneRegistry is not null && _zoneRegistry.TryGetPlayer(characterId, out state);
    }

    private void HandleMarkZoneTransferPending(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        state.IsMovingZone = true;
        state.ZoneTransferRegisteredAtUtc = DateTime.UtcNow;
    }

    private void HandleClearZoneTransferPending(int characterId)
    {
        if (_players.TryGetValue(characterId, out var state))
            state.IsMovingZone = false;
    }

    private void HandleRefreshZoneTransferRegistrationTimestamp(int characterId)
    {
        if (_players.TryGetValue(characterId, out var state))
            state.ZoneTransferRegisteredAtUtc = DateTime.UtcNow;
    }

    private void HandleSetMuted(int characterId, bool muted)
    {
        if (_players.TryGetValue(characterId, out var state))
            state.IsMuted = muted;
    }

    private async Task CleanupShardLocationAsync(int characterId)
    {
        try
        {
            await characterShardLocations!.RemoveAsync(characterId, options.ShardId, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Zone {MapId}: failed to remove character {CharacterId} from the cross-shard location directory",
                MapId, characterId);
        }
    }

    public void ApplyDeath(int characterId, DeathCause cause = DeathCause.Unknown,
        (float X, float Z)? deathSourcePosition = null, bool suppressExperienceLoss = false)
    {
        if (!_players.TryGetValue(characterId, out var state))
        {
            logger.LogWarning(
                "ApplyDeath({CharacterId}) on zone {MapId}: character not tracked here -- ignoring (already disconnected or mid-handoff)",
                characterId, MapId);
            return;
        }

        if (state.IsDead)
            return;

        state.Life = 0;
        state.IsDead = true;
        state.TicksSinceDeath = 0;

        state.ReviveHackFlag = cause != DeathCause.Duel;
        state.CanUseConsumables = false;
        state.DeathSubCounter = ReviveEligibilityRules.DeathSubCounterBaseline;

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        QueueDeathEventLog(CharacterDeathEventCode, characterId, (byte)cause, $"Cause={cause};Level={state.Level}");

        if (cause == DeathCause.MonsterKill && !suppressExperienceLoss)
            ApplyDeathExperienceLoss(state);

        if (state.IsStunned)
        {
            state.IsStunned = false;
            state.StunDurationSeconds = 0;
        }

        ClearAllBuffs(state);

        ExpireDrunkBottleEffect(state);

        ResetPartyBuffMarker(state);

        var deathDirection = deathSourcePosition is { } source
            ? AvatarDeathDirection.FromPositions(state.PosX, state.PosZ, source.X, source.Z)
            : (AvatarDeathDirection?)null;

        var deathPet = PetActionFieldsOf(state);
        var deathAction = new ActionInfo
        {
            Type = 0,
            Sort = 12,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = deathDirection is { } direction
                ? [direction.DirectionX, 0f, direction.DirectionZ]
                : [state.PosX, state.PosY, state.PosZ],
            Front = deathDirection?.FacingAngle ?? state.Heading,
            TargetFront = deathDirection?.FacingAngle ?? state.Heading,
            PetLocation = deathPet.PetLocation,
            PetTargetLocation = deathPet.PetTargetLocation,
            PetFront = deathPet.PetFront,
            PetSort = deathPet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        _deathNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_deathNeighborScratch, state.CurrentCell, characterId, state.PosX, state.PosY,
            state.PosZ);
        BroadcastAvatarAction(_deathNeighborScratch, state, deathAction);
    }

    private void ClearAllBuffs(PlayerRuntimeState state)
    {
        var changedSlots = state.BuffChangeScratch;
        var anyChanged = false;

        for (var slot = 0; slot < 35; slot++)
        {
            if (state.Buffs.Buff[slot * 2] == 0 && state.Buffs.Buff[slot * 2 + 1] == 0)
                continue;

            state.Buffs.Buff[slot * 2] = 0;
            state.Buffs.Buff[slot * 2 + 1] = 0;

            if (!anyChanged)
            {
                Array.Clear(changedSlots);
                anyChanged = true;
            }

            changedSlots[slot] = 1;
        }

        state.DarkAttackKind = 0;
        state.DarkAttackUseTick = 0;
        state.DarkAttackActiveTick = 0;
        state.HitRateKind = 0;
        state.HitRateTick = 0;
        state.DodgeRateKind = 0;
        state.DodgeRateTick = 0;

        state.IsUnderDarkAttackPotionDebuff = false;
        state.DarkAttackDebuffActivatedAtUtc = default;

        if (anyChanged)
            RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    private void ApplyDeathExperienceLoss(PlayerRuntimeState state)
    {
        switch (state.Level)
        {
            case < ExperienceFormulas.MinimumLevelForDeathExperienceLoss:
                return;

            case >= ExperienceFormulas.MaxLimitLevel:
                if (state.ProtectForDeath > 0)
                {
                    state.ProtectForDeath--;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                    QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId,
                        ContributionPointsLossOutcome,
                        $"Kind=DeathShield;RemainingStacks={state.ProtectForDeath};Level={state.Level}");
                }
                else
                {
                    state.ContributionPoints -= ExperienceFormulas.CpLossAtLevelCap;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                    state.Session.Send(new AvatarStatUpdateResponse
                        { Sort = ContributionPointStatSort, Value = state.ContributionPoints, Value2 = 0 });
                    QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId,
                        ContributionPointsLossOutcome,
                        $"Kind=ContributionPoints;Loss={ExperienceFormulas.CpLossAtLevelCap};Level={state.Level}");
                }

                return;
        }

        if (!worldData.LevelsByLevel.TryGetValue(state.Level, out var levelRow))
            return;

        var personalExpDownRatio = ExperienceFormulas.ResolvePersonalExpDownRatio(state.PremiumExpireUtc);
        var loss = ExperienceFormulas.ComputeDeathExperienceLoss(state.Experience, levelRow.ExpRangeMin,
            personalExpDownRatio, options.GlobalExpDownRatio);
        if (loss <= 0)
            return;

        if (state.ProtectForDeath > 0)
        {
            state.ProtectForDeath--;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId, ExperienceLossOutcome,
                $"Kind=DeathShield;RemainingStacks={state.ProtectForDeath};Level={state.Level}");
            return;
        }

        state.Experience -= loss;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId, ExperienceLossOutcome,
            $"Kind=Experience;Loss={loss};Level={state.Level}");
    }

    public void GrantReviveEligibility(PlayerRuntimeState state)
    {
        ClearDeathWindow(state, true);
    }

    public void ClearDeathWindowKeepLockArmed(PlayerRuntimeState state)
    {
        ClearDeathWindow(state, false);
    }

    private void ClearDeathWindow(PlayerRuntimeState state, bool clearLock)
    {
        if (!state.IsDead)
            return;

        state.IsDead = false;
        state.Life = 1;
        state.CanUseConsumables = true;
        state.DeathSubCounter = ReviveEligibilityRules.DeathSubCounterBaseline;

        if (clearLock)
        {
            state.ReviveHackFlag = false;
            state.TicksSinceDeath = 0;
        }

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        SendAvatarAction(state.Session, state);

        _reviveNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_reviveNeighborScratch, state.CurrentCell, state.CharacterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_reviveNeighborScratch, state);
    }

    private void HandleMove(int characterId, in ActionInfo action, bool isResumeAction = false)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.IsStunned && action.Sort != StunActionSort)
        {
            BroadcastStunActionState(state, state.StunDurationSeconds);
            return;
        }

        if (!isResumeAction)
            MaybeResetRegularWarAfkTick(state, in action);

        if (IsFormationSkillZoneLocked(action.SkillNumber, isResumeAction))
            return;

        var motion = default(CharacterMotionEvaluation);
        if (isResumeAction)
        {
            if (!AvatarActionResumeWhitelist.IsLegal(action.Sort, action.Type))
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId} DISCONNECTED (Faulted) -- op16 resume-action " +
                    "Sort={Sort} Type={Type} not in AvatarActionResumeWhitelist",
                    MapId, characterId, action.Sort, action.Type);
                if (state.Session is { } client)
                    client.Abort(DisconnectReason.Faulted);
                return;
            }

            state.DefenseHackPreviousPosX = state.PosX;
            state.DefenseHackPreviousPosY = state.PosY;
            state.DefenseHackPreviousPosZ = state.PosZ;

            if (action.Sort == StunActionSort)
                state.AfkTick = 0;
            else if (AvatarActionResumeWhitelist.ClearsFishingProgress(action.Sort))
                state.FishingState = 0;
        }
        else if (!CharacterMotionWhitelist.TryEvaluate(action.Sort, action.Type, out motion))
        {
            logger.LogWarning(
                "Zone {MapId}: character {CharacterId} DISCONNECTED (Faulted) -- op15 Sort={Sort} Type={Type} " +
                "not in CharacterMotionWhitelist",
                MapId, characterId, action.Sort, action.Type);
            if (state.Session is { } client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        var now = DateTime.UtcNow;

        if (!isResumeAction && !movementRules.IsPlausible(state, in action, Geometry))
        {
            state.ImplausibleMoveStreak++;

            logger.LogWarning(
                "Zone {MapId}: character {CharacterId} claimed an implausible position, REJECTED " +
                "(consecutive={Streak}) -- Sort={Sort} Type={Type} From=({FromX},{FromY},{FromZ}) " +
                "To=({ToX},{ToY},{ToZ}) GeometryLoaded={GeometryLoaded}",
                MapId, characterId, state.ImplausibleMoveStreak, action.Sort, action.Type,
                state.PosX, state.PosY, state.PosZ,
                action.Location[0], action.Location[1], action.Location[2],
                Geometry is not null);

            if (state.ImplausibleMoveStreak >= ImplausibleMoveDisconnectThreshold)
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId} DISCONNECTED (StateViolation) -- {Streak} consecutive " +
                    "implausible-position claims",
                    MapId, characterId, state.ImplausibleMoveStreak);
                if (state.Session is { } offender)
                    offender.Abort(DisconnectReason.StateViolation);
            }

            return;
        }

        if (!isResumeAction)
            state.ImplausibleMoveStreak = 0;

        logger.LogDebug(
            "Zone {MapId}: move ACCEPTED for character {CharacterId} -- Sort={Sort} Type={Type} Frame={Frame} " +
            "To=({ToX},{ToY},{ToZ}) Front={Front}",
            MapId, characterId, action.Sort, action.Type, action.Frame,
            action.Location[0], action.Location[1], action.Location[2], action.Front);

        if (isResumeAction && !EvaluateResumeActionSkillGradeGuard(state, in action))
            return;

        var isGuardedSkillCast = !isResumeAction &&
                                 motion.SkillCategoryCode is SkillCastGuard.HotkeyBoundCategoryCode
                                     or SkillCastGuard.SkillEffectCategoryCode;

        if (isGuardedSkillCast)
        {
            if (!EvaluateSkillCastPreCastGuard(state, action, motion.SkillCategoryCode, out var guardContext))
                return;

            if (motion.SkillCategoryCode == SkillCastGuard.SkillEffectCategoryCode &&
                !ApplySkillCastManaCharge(state, action))
                return;

            if (!EvaluateSkillCastPostCastGuard(state, action, in guardContext))
                return;
        }

        var previousActionSkillNumber = state.ActionSkillNumber;
        var previousActionSkillGradeNum1 = state.ActionSkillGradeNum1;
        var previousActionSkillGradeNum2 = state.ActionSkillGradeNum2;

        state.PosX = action.Location[0];
        state.PosY = action.Location[1];
        state.PosZ = action.Location[2];
        state.Heading = action.Front;
        state.LastMoveUtc = now;
        state.FlushSequence++;

        state.ActionSort = action.Sort;
        state.ActionSkillNumber = action.SkillNumber;
        state.ActionSkillGradeNum1 = action.SkillGradeNum1;
        state.ActionSkillGradeNum2 = action.SkillGradeNum2;

        if (!isResumeAction)
        {
            state.AttackBudgetEnforced = motion.AttackBudgetEnforced;
            state.AttackFamilyTag = motion.AttackFamilyTag;
            state.AttackSubPacketCeiling = motion.AttackSubPacketCeiling;
            state.AttackSubPacketsUsed = 0;
        }

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(characterId, state.CurrentCell, newCell, state.PosX, state.PosY, state.PosZ);
        state.CurrentCell = newCell;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        if (!isResumeAction)
            SendAvatarAction(state.Session, state, action);

        if (!isResumeAction)
        {
            _moveNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_moveNeighborScratch, newCell, characterId, state.PosX, state.PosY,
                state.PosZ);
            BroadcastAvatarAction(_moveNeighborScratch, state, action);
        }


        if (!isResumeAction)
        {
            if (!isGuardedSkillCast)
            {
                if (action.Sort == RestActionSort)
                    ApplyRestActionProtectionAndHeal(state);
                else if (PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction, action.Sort))
                    AdvanceCasterPartyBuffMarker(state, action.SkillNumber, action.Sort);
            }
        }
        else if (action.Sort == SkillEffectConfirmActionSort)
        {
            ApplySkillEffectConfirm(state, action, previousActionSkillNumber, previousActionSkillGradeNum1,
                previousActionSkillGradeNum2);
        }
        else if (PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction, action.Sort))
        {
            AdvanceCasterPartyBuffMarker(state, action.SkillNumber, action.Sort);
        }
    }

    private void MaybeResetRegularWarAfkTick(PlayerRuntimeState state, in ActionInfo action)
    {
        var isZone195 = options.Zone195MapIds.Contains(MapId);
        if (!isZone195 && !IsWarZone049Type)
            return;

        if (IsRegularWarAfkExemptSkill(action.SkillNumber) || state.AutoHuntEnabled)
            return;

        if (action.SkillNumber <= 0 && action.Sort != RegularWarAfkResetSortCode &&
            action.Sort != StunActionSort && state.ActionSort != StunActionSort)
            return;

        var fullUnits =
            isZone195 ? RegularWarAfkTickSystem.Zone195FullUnits : RegularWarAfkTickSystem.WarActiveFullUnits;
        if (state.AfkTick >= RegularWarAfkTickSystem.ResetNotificationLegacyTicks)
            state.Session.Send(new LocalChatResponse
            {
                AvatarName = RegularWarAfkCheckerSenderName,
                Content = $"Reset 0/{fullUnits}",
                Link = RegularWarAfkCheckerLink
            });

        state.AfkTick = 0;
    }

    private static bool IsRegularWarAfkExemptSkill(int skillNumber)
    {
        return skillNumber switch
        {
            1 or 6 or 7 or 10 or 11 or 14 or 15 or 18 or 19 or 20 or 25 or 26 or 29 or 30 or 33 or 34 or 37 or 38 or 39
                or 44 or 45 or 48 or 49 or 52 or 53 or 56 or 57 or 82 or 83 or 84 or 103 or 104 or 105 => true,
            _ => false
        };
    }

    private SkillCastGuardContext BuildSkillCastGuardContext(PlayerRuntimeState state, ActionInfo action,
        int skillCategoryCode)
    {
        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var equipSlotItems = BuildEquipSlotItems(state);

        var serverBonusGrade = SkillGradeAuthority.GetBonusSkillValue(action.SkillNumber, equipSlotItems, 0,
            skillDef, state.GuildBuffType, state.GuildBuffActive);
        var serverMaxGrade = SkillGradeAuthority.GetMaxSkillGradeNum(action.SkillNumber, state.LearnedSkills);

        var isRealSkillCast = action.SkillNumber != 0 &&
                              !FormationSkillCatalog.IsExemptFromGradeBoundCheck(action.SkillNumber, action.Sort,
                                  true);

        return new SkillCastGuardContext(
            skillCategoryCode,
            state.AutoHuntEnabled,
            action.SkillNumber,
            action.SkillGradeNum1,
            action.SkillGradeNum2,
            serverBonusGrade,
            serverMaxGrade,
            isRealSkillCast,
            state.Hotkeys,
            state.LearnedSkills);
    }

    private bool EvaluateResumeActionSkillGradeGuard(PlayerRuntimeState state, in ActionInfo action)
    {
        if (action.SkillNumber == 0 ||
            FormationSkillCatalog.IsExemptFromGradeBoundCheck(action.SkillNumber, action.Sort, false))
            return true;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var equipSlotItems = BuildEquipSlotItems(state);

        var serverMaxGrade = SkillGradeAuthority.GetMaxSkillGradeNum(action.SkillNumber, state.LearnedSkills);
        var serverBonusGrade = SkillGradeAuthority.GetBonusSkillValue(action.SkillNumber, equipSlotItems, 0,
            skillDef, state.GuildBuffType, state.GuildBuffActive);

        return action.SkillGradeNum1 <= serverMaxGrade && action.SkillGradeNum2 <= serverBonusGrade;
    }

    private ItemDefinition?[] BuildEquipSlotItems(PlayerRuntimeState state)
    {
        var equipSlotItems = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        for (var slot = 0; slot < SkillGradeAuthority.EquipSlotCount; slot++)
        {
            var equippedStack = state.Inventory.GetSlot(ContainerMatrix.Equipment, (byte)slot);
            if (equippedStack is { } stack && worldData.ItemsById.TryGetValue(stack.ItemId, out var itemDef))
                equipSlotItems[slot] = itemDef;
        }

        return equipSlotItems;
    }

    private bool EvaluateSkillCastPreCastGuard(PlayerRuntimeState state, ActionInfo action, int skillCategoryCode,
        out SkillCastGuardContext context)
    {
        context = BuildSkillCastGuardContext(state, action, skillCategoryCode);
        return HandleSkillCastVerdict(state, action, SkillCastGuard.EvaluatePreCast(context), in context);
    }

    private bool EvaluateSkillCastPostCastGuard(PlayerRuntimeState state, ActionInfo action,
        in SkillCastGuardContext context)
    {
        return HandleSkillCastVerdict(state, action, SkillCastGuard.EvaluatePostCast(context), in context);
    }

    private bool HandleSkillCastVerdict(PlayerRuntimeState state, ActionInfo action, SkillCastVerdict verdict,
        in SkillCastGuardContext context)
    {
        if (verdict.Offense == SkillCastOffense.None)
            return true;

        eventLogQueue?.Enqueue(new EventLogEntryTvp(
            (short)verdict.Offense,
            (byte)EventLogCategory.AntiCheat,
            null,
            state.CharacterId,
            null,
            null,
            options.ShardId,
            null,
            null,
            null,
            null,
            null,
            $"SkillCastOffense={verdict.Offense};Skill={action.SkillNumber};ClaimedGrade1={action.SkillGradeNum1};ClaimedGrade2={action.SkillGradeNum2};ServerBonus={context.ServerBonusGrade};ServerMax={context.ServerMaxGrade}",
            DateTime.UtcNow));

        if (verdict.Enforcement != SkillCastEnforcement.Disconnect)
        {
            logger.LogWarning(
                "Character {CharacterId} skill-cast tamper guard tripped ({Offense}) on zone {MapId} -- dropping packet",
                state.CharacterId, verdict.Offense, MapId);

            return false;
        }

        logger.LogWarning(
            "Character {CharacterId} skill-cast tamper guard tripped ({Offense}) on zone {MapId} -- disconnecting",
            state.CharacterId, verdict.Offense, MapId);

        if (state.Session is { } client)
            client.Abort(DisconnectReason.Faulted);

        return false;
    }

    private void ApplyRestActionProtectionAndHeal(PlayerRuntimeState state)
    {
        state.ZoneEntryAtZoneClock = _clock;

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        state.Life = maxLife / 3 + 1;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        var response = new AvatarStatUpdateResponse
            { Sort = CharacterHpStatSort, Value = state.Life, Value2 = 0 };

        var total = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendRawFrameToRecipient(state.CharacterId, span, state.DungeonInstanceId);
            _healTargetNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_healTargetNeighborScratch, state.CurrentCell, state.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            foreach (var neighborId in _healTargetNeighborScratch)
                SendRawFrameToRecipient(neighborId, span, state.DungeonInstanceId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void HandlePetAction(int characterId, in ActionInfo action)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        state.PetActionSort = action.PetSort;
        state.PetActionFront = action.PetFront;
        state.PetActionLocationX = action.PetLocation[0];
        state.PetActionLocationY = action.PetLocation[1];
        state.PetActionLocationZ = action.PetLocation[2];
        state.PetActionTargetLocationX = action.PetTargetLocation[0];
        state.PetActionTargetLocationY = action.PetTargetLocation[1];
        state.PetActionTargetLocationZ = action.PetTargetLocation[2];
    }

    private bool ApplySkillCastManaCharge(PlayerRuntimeState state, ActionInfo action)
    {
        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var manaGradePoints = action.SkillGradeNum1;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var reductionRatioPercent = ManaCostReduction.GetRatioPercent(BuildEquipSlotItems(state));

        var result = SkillCastResolver.TryCast(skillDef, manaGradePoints, state.Mana, maxLife, weaponSort,
            state.SupportSkillTimeUpRatio, reductionRatioPercent);

        if (result.Failure == SkillCastResolver.FailureReason.InsufficientMana)
            return false;

        if (result.ManaCost > 0)
        {
            state.Mana -= result.ManaCost;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }

        return true;
    }

    private void ApplySkillEffectConfirm(PlayerRuntimeState state, ActionInfo action, int previousSkillNumber,
        int previousGradeNum1, int previousGradeNum2)
    {
        if (action.SkillNumber != previousSkillNumber ||
            action.SkillGradeNum1 != previousGradeNum1 ||
            action.SkillGradeNum2 != previousGradeNum2)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var gradePoints = action.SkillGradeNum1 + action.SkillGradeNum2;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var result = SkillCastResolver.TryCast(skillDef, gradePoints, int.MaxValue, maxLife, weaponSort,
            state.SupportSkillTimeUpRatio);
        if (!result.Success)
            return;

        if (!result.RequiresFullParty)
        {
            var equipSlotItems = BuildEquipSlotItems(state);
            var serverBonusGrade = SkillGradeAuthority.GetBonusSkillValue(action.SkillNumber, equipSlotItems, 0,
                skillDef, state.GuildBuffType, state.GuildBuffActive);
            var serverMaxGrade = SkillGradeAuthority.GetMaxSkillGradeNum(action.SkillNumber, state.LearnedSkills);

            if (action.SkillGradeNum1 > serverMaxGrade || action.SkillGradeNum2 > serverBonusGrade)
                return;
        }

        DispatchSkillEffect(state, result, action.SkillNumber, action);
    }

    private void ApplyRegisteredAutoBuffs(PlayerRuntimeState state)
    {
        var equipSlotItems = BuildEquipSlotItems(state);
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        foreach (var (skillId, _) in state.AutoBuffSkill)
        {
            if (skillId == 0)
                continue;

            worldData.SkillsById.TryGetValue(skillId, out var skillDef);
            var gradePoints = SkillGradeAuthority.GetMaxSkillGradeNum(skillId, state.LearnedSkills) +
                              SkillGradeAuthority.GetBonusSkillValue(skillId, equipSlotItems, 0, skillDef,
                                  state.GuildBuffType, state.GuildBuffActive);

            var result = SkillCastResolver.TryCast(skillDef, gradePoints, int.MaxValue, maxLife, weaponSort,
                state.SupportSkillTimeUpRatio);
            if (!result.Success)
                continue;

            DispatchSkillEffect(state, result, skillId, default);
        }
    }

    private void DispatchSkillEffect(PlayerRuntimeState state, SkillCastResolver.Result result, int skillNumber,
        ActionInfo action)
    {
        if (result.RequiresFullParty &&
            (!HasFullPartyPresent(state.CharacterId) || state.PartyBuffAct != PartyBuffAction.Done))
            return;

        switch (result.Kind)
        {
            case SkillEffectKind.SelfBuff:
                if (skillNumber == HolyShieldSkillId && MapId == HolyShieldCooldownZoneId)
                {
                    var now = DateTime.UtcNow;
                    if (now - state.LastHolyShieldAppliedUtc < HolyShieldReapplyCooldown)
                        break;

                    state.LastHolyShieldAppliedUtc = now;
                }

                ApplyBuffWrites(state, result.BuffWrites);

                if (PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(skillNumber))
                    ResetPartyBuffMarker(state);
                break;
            case SkillEffectKind.HealLife:
                if (ApplyTargetedHeal(state, action, true, result.HealAmount, out _) is not null)
                    BroadcastCasterEffectSnapshot(state);
                break;
            case SkillEffectKind.HealMana:
                if (ApplyTargetedHeal(state, action, false, result.HealAmount, out var recoveredMana) is
                    { } manaRecipient)
                {
                    BroadcastCasterEffectSnapshot(state);
                    BroadcastManaRecoveryToTarget(manaRecipient);
                    BroadcastAvatarStateFlag(manaRecipient, ManaRecoveredAvatarChangeInfoSort, recoveredMana, 0, 0);
                }

                break;
        }
    }

    private bool HasFullPartyPresent(int characterId)
    {
        var members = _partyRegistry.GetMembers(characterId);
        if (members.Count != PartyRegistry.MaxMembers)
            return false;

        var presentCount = 0;
        foreach (var memberId in members)
            if (_players.ContainsKey(memberId))
                presentCount++;

        return presentCount == PartyRegistry.MaxMembers;
    }

    internal void ApplyBuffWrites(PlayerRuntimeState state, ImmutableArray<SkillCastResolver.BuffWrite> writes,
        bool recomputeStats = true)
    {
        if (writes.IsEmpty)
            return;

        var changedSlots = state.BuffChangeScratch;
        Array.Clear(changedSlots);
        foreach (var write in writes)
        {
            if (write.Slot is < 0 or >= 35) continue;
            state.Buffs.Buff[write.Slot * 2] = write.Value;
            state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;
            changedSlots[write.Slot] = 1;
        }

        if (recomputeStats)
            RecomputeStatsAndBroadcastBuffs(state, changedSlots);
        else
            BroadcastEffectStateSnapshot(state, changedSlots);
    }

    private PlayerRuntimeState? ApplyTargetedHeal(PlayerRuntimeState caster, ActionInfo action, bool isLife,
        int rawAmount, out int appliedAmount)
    {
        appliedAmount = 0;
        if (rawAmount < 1)
            return null;
        if (!_players.TryGetValue(action.TargetObjectIndex, out var target))
            return null;

        var currentValue = isLife ? target.Life : target.Mana;
        var maxValue = isLife ? target.Stats?.MaxLife ?? target.MaxLife : target.Stats?.MaxMana ?? target.MaxMana;

        var eligibility = new TargetedHealResolver.Target(
            target.CharacterId,
            target.UniqueNumber,
            target.IsDead,
            target.IsStunned,
            IsHiding(target),
            target.PshopOpen,
            target.ActionSort,
            currentValue,
            maxValue);

        if (!TargetedHealResolver.TryResolveAmount(caster.CharacterId,
                unchecked((uint)action.TargetObjectUniqueNumber), eligibility, rawAmount, out appliedAmount))
            return null;

        if (isLife)
            target.Life += appliedAmount;
        else
            target.Mana += appliedAmount;

        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        return target;
    }

    private static bool IsHiding(PlayerRuntimeState player)
    {
        return player.VisibleState == 0;
    }

    public void RecomputeStatsAndBroadcastBuffs(PlayerRuntimeState state, int[] changedSlots)
    {
        RecomputeDerivedStats(state);
        BroadcastEffectStateSnapshot(state, changedSlots);
    }

    private void RecomputeDerivedStats(PlayerRuntimeState state)
    {
        RecomputeAndPublish(state, false);
    }

    private void BroadcastCasterEffectSnapshot(PlayerRuntimeState state)
    {
        Array.Clear(state.BuffChangeScratch);
        BroadcastEffectStateSnapshot(state, state.BuffChangeScratch);
    }

    private void BroadcastEffectStateSnapshot(PlayerRuntimeState state, int[] changedSlots)
    {
        var response = new AvatarEffectStateResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            EffectValue = state.Buffs.Buff,
            EffectValueState = changedSlots
        };

        var total = FrameWriter.FrameSizeOf<AvatarEffectStateResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendRawFrameToRecipient(state.CharacterId, span, state.DungeonInstanceId);
            _buffStateNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_buffStateNeighborScratch, state.CurrentCell, state.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            foreach (var neighborId in _buffStateNeighborScratch)
                SendRawFrameToRecipient(neighborId, span, state.DungeonInstanceId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void BroadcastManaRecoveryToTarget(PlayerRuntimeState target)
    {
        var response = new AvatarStatUpdateResponse
            { Sort = CharacterMpStatSort, Value = target.Mana, Value2 = 0 };

        var total = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendRawFrameToRecipient(target.CharacterId, span, target.DungeonInstanceId);
            _healTargetNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_healTargetNeighborScratch, target.CurrentCell, target.CharacterId,
                target.PosX, target.PosY, target.PosZ);
            foreach (var neighborId in _healTargetNeighborScratch)
                SendRawFrameToRecipient(neighborId, span, target.DungeonInstanceId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void SendRawFrameToRecipient(int recipientId, ReadOnlySpan<byte> frame, int? sourceInstanceId)
    {
        try
        {
            if (TryGetBroadcastRecipient(recipientId, out var recipient, out var clientSession) &&
                IsVisibleAcrossDungeonInstance(sourceInstanceId, recipient.DungeonInstanceId))
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} raw-frame send to character {RecipientId} failed", MapId,
                recipientId);
        }
    }

    private void SendAvatarAction(IPacketSession session, PlayerRuntimeState state)
    {
        session.Send(BuildAvatarActionRecv(state));
    }

    private void SendAvatarAction(IPacketSession session, PlayerRuntimeState state, ActionInfo action)
    {
        session.Send(BuildAvatarActionRecv(state, action));
    }

    private void BroadcastAvatarAction(IReadOnlyList<int> recipientCharacterIds, PlayerRuntimeState state,
        ActionInfo? action = null)
    {
        state.LastAvatarRebroadcastAt = _clock;

        if (recipientCharacterIds.Count == 0 || state.VisibleState == 0)
            return;

        var packet = action is null ? BuildAvatarActionRecv(state) : BuildAvatarActionRecv(state, action.Value);
        var total = FrameWriter.FrameSizeOf<AvatarActionResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipientCharacterIds)
                try
                {
                    if (TryGetBroadcastRecipient(id, out var recipient, out var clientSession) &&
                        IsVisibleAcrossDungeonInstance(state.DungeonInstanceId, recipient.DungeonInstanceId))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} broadcast to character {RecipientId} failed", MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private bool IsReviveHackBroadcastSuppressed(PlayerRuntimeState recipient)
    {
        if (MapId == ReviveEligibilityZones.BroadcastSuppressionExemptZoneId)
            return false;

        return recipient.ReviveHackFlag &&
               recipient.TicksSinceDeath >= SimulationClock.DeathBroadcastSuppressionLegacyTicks;
    }

    private bool TryGetBroadcastRecipient(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? recipient,
        [NotNullWhen(true)] out IPacketSession? clientSession)
    {
        if (_players.TryGetValue(characterId, out recipient) &&
            recipient.Session is { } session &&
            !recipient.IsMovingZone &&
            !IsReviveHackBroadcastSuppressed(recipient))
        {
            clientSession = session;
            return true;
        }

        clientSession = null;
        return false;
    }

    private bool TryGetZoneWideBroadcastRecipient(int characterId,
        [NotNullWhen(true)] out IPacketSession? clientSession)
    {
        if (_players.TryGetValue(characterId, out var recipient) &&
            recipient.Session is { } session &&
            !recipient.IsMovingZone)
        {
            clientSession = session;
            return true;
        }

        clientSession = null;
        return false;
    }

    private AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state)
    {
        var pet = PetActionFieldsOf(state);

        return BuildAvatarActionRecv(state, new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = pet.PetLocation,
            PetTargetLocation = pet.PetTargetLocation,
            PetFront = pet.PetFront,
            PetSort = pet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        });
    }

    private static (float[] PetLocation, float[] PetTargetLocation, float PetFront, int PetSort) PetActionFieldsOf(
        PlayerRuntimeState state)
    {
        return (
            [state.PetActionLocationX, state.PetActionLocationY, state.PetActionLocationZ],
            [state.PetActionTargetLocationX, state.PetActionTargetLocationY, state.PetActionTargetLocationZ],
            state.PetActionFront,
            state.PetActionSort);
    }

    public AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state, ActionInfo action)
    {
        return new AvatarActionResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Data = new ObjectForAvatar
            {
                VisibleState = state.VisibleState,
                SpecialState = state.SpecialState,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = "",
                GuildRole = 0,
                CallName = "",
                GuildMarkEffect = 0,
                Name = state.Name,
                Tribe = state.Tribe,
                PreviousTribe = state.PreviousTribe,
                Gender = state.Gender,
                HeadType = state.HeadType,
                FaceType = state.FaceType,
                Level1 = state.Level,
                Level2 = state.Level2,
                EquipForView =
                    EquipmentViewCodec.BuildEquipForView(state.Inventory.GetContainer(ContainerMatrix.Equipment)),
                AnimalNumber = 0,
                Title = state.Title,
                Halo = state.Halo,
                RebirthNum = state.RebirthCount,
                BattleTeam = 0,
                Action = action,
                MaxLifeValue = state.MaxLife,
                LifeValue = state.Life,
                MaxManaValue = state.MaxMana,
                ManaValue = state.Mana,
                EffectValueForView = BuildEffectValueForView(state),
                PartyName = "",
                DuelState = ResolveDuelStateForView(state.CharacterId),
                PShopState = 0,
                PShopName = "",
                CostumeNumber = state.CostumeNumber,
                BufEffectTimeState = 0,
                BufSort = 0,
                AutoState = 0,
                FishingState = 0,
                FishingStep = 0,
                FishingPoint = new float[3],
                RankPoint = 0,
                TargetState = 0,
                AnimalAbsorbState = state.AnimalAbsorbState,
                PetValid = 0,
                Unk1 = 0,
                PetLocation = new float[3],
                PetFrame = 0,
                Unk624 = 0,
                Unk625 = 0,
                UniqueSkillNumber = 0,
                UniqueSkillBuffTime = 0,
                CostumeState = state.CostumeState,
                StellarCoreNumber = 0
            },
            CheckChangeActionState = 0
        };
    }

    private static int[] BuildEffectValueForView(PlayerRuntimeState state)
    {
        var view = new int[35];
        for (var slot = 0; slot < 35; slot++)
            view[slot] = state.Buffs.Buff[slot * 2];

        view[DarkAttackPotionDefenderEffectSlot] = state.IsUnderDarkAttackPotionDebuff ? 1 : 0;

        return view;
    }

    private int[] ResolveDuelStateForView(int characterId)
    {
        return _duelRegistry.TryGetActiveDuel(characterId, out var duel) && duel is not null
            ? [1, duel.UniqueNumber, characterId == duel.PlayerA ? 1 : 2]
            : new int[3];
    }

    public readonly record struct PendingDeathEventLog(
        short EventCode,
        int ActorCharacterId,
        short? ShardId,
        byte? Outcome,
        string? Payload);
}
