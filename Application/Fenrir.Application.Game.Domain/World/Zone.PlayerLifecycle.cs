using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

        private const short CharacterDeathEventCode = 1;

        private const short DeathExperienceLossEventCode = 2;

        private const byte ExperienceLossOutcome = 0;

        private const byte ContributionPointsLossOutcome = 1;

        private const int SkillCastEffectCategoryCode = 2;

        private const int RestActionSort = 0;

        private const int SkillEffectConfirmActionSort = 1;

        private const int HolyShieldSkillId = 82;

        private const short HolyShieldCooldownZoneId = 124;

        private const int CharacterHpStatSort = 10;

        private static readonly TimeSpan HolyShieldReapplyCooldown = TimeSpan.FromSeconds(10);

        private readonly List<int> _buffStateNeighborScratch = [];

        private readonly SemaphoreSlim _deathEventLogSignal = new(0, int.MaxValue);

        private readonly List<int> _deathNeighborScratch = [];

        private readonly List<int> _enterNeighborScratch = [];

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

            state.LastAvatarRebroadcastAt = _clock;

            if (state.IsDead)
                continue;

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
            Life = data.Life,
            MaxLife = data.MaxLife,
            Mana = data.Mana,
            MaxMana = data.MaxMana,
            FlushSequence = data.FlushSequence,
            LastMoveUtc = DateTime.UtcNow,
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
            WarPoint = data.WarPoint,
            PremiumExpireUtc = data.PremiumExpireUtc,
            BuffX2Time = data.BuffX2Time,
            StoreMoney = data.StoreMoney,
            BigMoney = data.BigMoney,
            InventoryDate = data.InventoryDate,
            StoreDate = data.StoreDate,
            PetBagDate = data.PetBagDate,
            M15PetLuckyBoxPity = data.M15PetLuckyBoxPity,
            SourceIp = data.SourceIp
        };

        state.ResetVolatileAntiCheatCountersOnEntry(_clock);

        state.RecomputeSupportSkillTimeUpRatio(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

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

                if (existing.Session is ZoneClientSession staleZoneSession)
                    staleZoneSession.CurrentZone = null;

                if (existing.Session is ClientSession staleClientSession)
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
            if (_players.TryGetValue(otherId, out var other))
                SendAvatarAction(state.Session, other);

        BroadcastAvatarAction(_enterNeighborScratch, state);

        SendExistingMonstersTo(state);

        if (IsZone241TypeZone)
            TryEnterZone241PersonalInstance(characterId);

        TryPublishPartyResyncRequest(characterId, state.Name);
    }

        private void TryPublishPartyResyncRequest(int characterId, string avatarName)
    {
        if (_partyResyncRelayQueue is null || _partyRegistry.IsInParty(characterId))
            return;

        _partyResyncRelayQueue.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.Request, options.ShardId, characterId, avatarName, avatarName));
    }

    private void HandleLeave(int characterId, Zone? handoffTarget, (float X, float Y, float Z)? handoffPosition = null)
    {
        if (!_players.TryRemove(characterId, out var state))
            return;

        _grid.Remove(characterId, state.CurrentCell);

        if (handoffTarget is null)
        {
            logger.LogInformation("Character {CharacterId} left zone {MapId}", characterId, MapId);

            if (!state.IsMovingZone)
                BreakPartyOnDisconnect(characterId, state.Name);

            ClearTradeOnDisconnect(characterId);

            ClearDungeonInstanceOnDisconnect(state);

            if (characterShardLocations is not null)
                _ = CleanupShardLocationAsync(characterId);
            return;
        }

        var enterData = ZoneTransfer.CreateEnterData(state, handoffTarget.MapId, handoffPosition);

        if (!handoffTarget.Post(ZoneCommand.Enter(characterId, enterData)))
        {
            logger.LogError(
                "Zone {TargetMapId} inbox full: dropped handoff Enter for character {CharacterId} from zone {MapId} -- aborting session",
                handoffTarget.MapId, characterId, MapId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        if (state.Session is ZoneClientSession zoneSession)
            zoneSession.CurrentZone = handoffTarget;

        logger.LogInformation("Character {CharacterId} handed off from zone {MapId} to zone {TargetMapId}",
            characterId, MapId, handoffTarget.MapId);
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
        if (_players.TryGetValue(characterId, out var state))
            state.IsMovingZone = true;
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

    public void ApplyDeath(int characterId, DeathCause cause = DeathCause.Unknown)
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

        if (cause == DeathCause.MonsterKill)
            ApplyDeathExperienceLoss(state);

        if (state.IsStunned)
        {
            state.IsStunned = false;
            state.StunDurationSeconds = 0;
            state.StunCountdownAccumulatorTicks = 0;
        }

        ClearAllBuffs(state);

        ResetPartyBuffMarker(state);

        var deathPet = PetActionFieldsOf(state);
        var deathAction = new ActionInfo
        {
            Type = 0,
            Sort = 12,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
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
                state.ContributionPoints -= ExperienceFormulas.CpLossAtLevelCap;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId, ContributionPointsLossOutcome,
                    $"Kind=ContributionPoints;Loss={ExperienceFormulas.CpLossAtLevelCap};Level={state.Level}");
                return;
        }

        if (!worldData.LevelsByLevel.TryGetValue(state.Level, out var levelRow))
            return;

        var loss = ExperienceFormulas.ComputeDeathExperienceLoss(state.Experience, levelRow.ExpRangeMin);
        if (loss <= 0)
            return;

        state.Experience -= loss;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId, ExperienceLossOutcome,
            $"Kind=Experience;Loss={loss};Level={state.Level}");
    }

    public void GrantReviveEligibility(PlayerRuntimeState state)
    {
        if (!state.IsDead)
            return;

        state.IsDead = false;
        state.Life = 1;
        state.ReviveHackFlag = false;
        state.CanUseConsumables = true;
        state.TicksSinceDeath = 0;
        state.DeathSubCounter = ReviveEligibilityRules.DeathSubCounterBaseline;

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

        var motion = default(CharacterMotionEvaluation);
        if (isResumeAction)
        {
            if (!AvatarActionResumeWhitelist.IsLegal(action.Sort, action.Type))
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId} DISCONNECTED (Faulted) -- op16 resume-action " +
                    "Sort={Sort} Type={Type} not in AvatarActionResumeWhitelist",
                    MapId, characterId, action.Sort, action.Type);
                if (state.Session is ClientSession client)
                    client.Abort(DisconnectReason.Faulted);
                return;
            }
        }
        else if (!CharacterMotionWhitelist.TryEvaluate(action.Sort, action.Type, out motion))
        {
            logger.LogWarning(
                "Zone {MapId}: character {CharacterId} DISCONNECTED (Faulted) -- op15 Sort={Sort} Type={Type} " +
                "not in CharacterMotionWhitelist",
                MapId, characterId, action.Sort, action.Type);
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        var now = DateTime.UtcNow;

        if (!movementRules.IsPlausible(state, in action, Geometry))
        {
            logger.LogWarning(
                "Zone {MapId}: MOVE REJECTED for character {CharacterId} -- Sort={Sort} Type={Type} " +
                "From=({FromX},{FromY},{FromZ}) To=({ToX},{ToY},{ToZ}) GeometryLoaded={GeometryLoaded}",
                MapId, characterId, action.Sort, action.Type,
                state.PosX, state.PosY, state.PosZ,
                action.Location[0], action.Location[1], action.Location[2],
                Geometry is not null);

            SendAvatarAction(state.Session, state);
            return;
        }

        logger.LogDebug(
            "Zone {MapId}: move ACCEPTED for character {CharacterId} -- Sort={Sort} Type={Type} Frame={Frame} " +
            "To=({ToX},{ToY},{ToZ}) Front={Front}",
            MapId, characterId, action.Sort, action.Type, action.Frame,
            action.Location[0], action.Location[1], action.Location[2], action.Front);

        if (IsFormationSkillZoneLocked(action.SkillNumber))
            return;

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
            if (motion.SkillCategoryCode == SkillCastEffectCategoryCode)
            {
                if (!EvaluateSkillCastTamperGuard(state, action))
                    return;

                ApplySkillCastManaCharge(state, action);
            }
            else if (action.Sort == RestActionSort)
            {
                ApplyRestActionProtectionAndHeal(state);
            }
            else if (PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction, action.Sort))
            {
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

        private bool EvaluateSkillCastTamperGuard(PlayerRuntimeState state, ActionInfo action)
    {
        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);

        var equipSlotItems = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        for (var slot = 0; slot < SkillGradeAuthority.EquipSlotCount; slot++)
        {
            var equippedStack = state.Inventory.GetSlot(ContainerMatrix.Equipment, (byte)slot);
            if (equippedStack is { } stack && worldData.ItemsById.TryGetValue(stack.ItemId, out var itemDef))
                equipSlotItems[slot] = itemDef;
        }

        var serverBonusGrade = SkillGradeAuthority.GetBonusSkillValue(action.SkillNumber, equipSlotItems, 0,
            skillDef, state.GuildBuffType, state.GuildBuffActive);
        var serverMaxGrade = SkillGradeAuthority.GetMaxSkillGradeNum(action.SkillNumber, state.LearnedSkills);

        var isRealSkillCast = action.SkillNumber != 0 &&
            !FormationSkillCatalog.IsExemptFromGradeBoundCheck(action.SkillNumber, action.Sort,
                isPrimaryHandler: true);

        var offense = SkillCastGuard.Evaluate(new SkillCastGuardContext(
            SkillCastEffectCategoryCode,
            state.AutoHuntEnabled,
            action.SkillNumber,
            action.SkillGradeNum1,
            action.SkillGradeNum2,
            serverBonusGrade,
            serverMaxGrade,
            isRealSkillCast,
            state.Hotkeys,
            state.LearnedSkills));

        if (offense == SkillCastOffense.None)
            return true;

        eventLogQueue?.Enqueue(new EventLogEntryTvp(
            (short)offense,
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
            $"SkillCastOffense={offense};Skill={action.SkillNumber};ClaimedGrade1={action.SkillGradeNum1};ClaimedGrade2={action.SkillGradeNum2};ServerBonus={serverBonusGrade};ServerMax={serverMaxGrade}",
            DateTime.UtcNow));

        logger.LogWarning(
            "Character {CharacterId} skill-cast tamper guard tripped ({Offense}) on zone {MapId} -- disconnecting",
            state.CharacterId, offense, MapId);

        if (state.Session is ClientSession client)
            client.Abort(DisconnectReason.Faulted);

        return false;
    }

        private void ApplyRestActionProtectionAndHeal(PlayerRuntimeState state)
    {
        state.ZoneEntryAtZoneClock = _clock;

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        state.Life = maxLife / 3 + 1;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = CharacterHpStatSort, Value = state.Life, Value2 = 0 });
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

        private void ApplySkillCastManaCharge(PlayerRuntimeState state, ActionInfo action)
    {
        if (state.LastSkillCastAtZoneClock is { } lastCast && _clock - lastCast < SimulationClock.LegacyTick)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var manaGradePoints = action.SkillGradeNum1;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var result = SkillCastResolver.TryCast(skillDef, manaGradePoints, state.Mana, maxLife, weaponSort,
            state.SupportSkillTimeUpRatio);
        if (!result.Success)
            return;

        state.LastSkillCastAtZoneClock = _clock;
        state.Mana -= result.ManaCost;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);


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


        if (result.RequiresFullParty &&
            (!HasFullPartyPresent(state.CharacterId) || state.PartyBuffAct != PartyBuffAction.Done))
            return;

        switch (result.Kind)
        {
            case SkillEffectKind.SelfBuff:
                if (action.SkillNumber == HolyShieldSkillId && MapId == HolyShieldCooldownZoneId)
                {
                    var now = DateTime.UtcNow;
                    if (now - state.LastHolyShieldAppliedUtc < HolyShieldReapplyCooldown)
                        break;

                    state.LastHolyShieldAppliedUtc = now;
                }

                ApplyBuffWrites(state, result.BuffWrites);

                if (PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(action.SkillNumber))
                    ResetPartyBuffMarker(state);
                break;
            case SkillEffectKind.HealLife:
                ApplyTargetedHeal(action, true, result.HealAmount);
                break;
            case SkillEffectKind.HealMana:
                ApplyTargetedHeal(action, false, result.HealAmount);
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

        internal void ApplyBuffWrites(PlayerRuntimeState state, ImmutableArray<SkillCastResolver.BuffWrite> writes)
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

        RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

        private void ApplyTargetedHeal(ActionInfo action, bool isLife, int rawAmount)
    {
        if (rawAmount < 1)
            return;
        if (!_players.TryGetValue(action.TargetObjectIndex, out var target))
            return;
        if (target.UniqueNumber != unchecked((uint)action.TargetObjectUniqueNumber))
            return;
        if (target.IsDead)
            return;

        if (isLife)
        {
            var max = target.Stats?.MaxLife ?? target.MaxLife;
            var amount = Math.Min(rawAmount, max - target.Life);
            if (amount < 1) return;
            target.Life += amount;
        }
        else
        {
            var max = target.Stats?.MaxMana ?? target.MaxMana;
            var amount = Math.Min(rawAmount, max - target.Mana);
            if (amount < 1) return;
            target.Mana += amount;
        }

        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

        public void RecomputeStatsAndBroadcastBuffs(PlayerRuntimeState state, int[] changedSlots)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount, state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        state.Stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, runtimeState: state);

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

            SendBuffStateFrame(state.CharacterId, span);
            _buffStateNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_buffStateNeighborScratch, state.CurrentCell, state.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            foreach (var neighborId in _buffStateNeighborScratch)
                SendBuffStateFrame(neighborId, span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void SendBuffStateFrame(int recipientId, ReadOnlySpan<byte> frame)
    {
        try
        {
            if (_players.TryGetValue(recipientId, out var recipient) &&
                recipient.Session is ClientSession clientSession)
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} buff-state broadcast to character {RecipientId} failed", MapId,
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
        if (recipientCharacterIds.Count == 0)
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
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        !IsReviveHackBroadcastSuppressed(recipient))
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
                AnimalAbsorbState = 0,
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
