using System.Buffers;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Keep-alive rebroadcast: re-emits every avatar's current state to its surroundings every 3.5 s even
    ///     when idle, so late-arriving or packet-lossy neighbors converge.
    /// </summary>
    private void RebroadcastAvatars()
    {
        // Direct enumeration (no Values snapshot): ConcurrentDictionary's enumerator is lock-free, and the
        // tick thread is the only mutator anyway.
        foreach (var (characterId, state) in _players)
        {
            if (_clock - state.LastAvatarRebroadcastAt < SimulationClock.AvatarRebroadcastInterval)
                continue;

            state.LastAvatarRebroadcastAt = _clock;

            var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
            BroadcastAvatarAction(neighbors, state);
        }
    }

    private void HandleEnter(int characterId, PlayerEnterData data)
    {
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
            // Carried through an in-process handoff so a player mid-death who transfers zones before the
            // auto-revive fires doesn't silently come back "alive" with 0 HP on arrival.
            IsDead = data.IsDead,
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
            IsMuted = data.IsMuted,
            GuildId = data.GuildId,
            GuildName = data.GuildName,
            GuildRoleDb = data.GuildRoleDb,
            GuildCallName = data.GuildCallName,
            TribeRole = data.TribeRole,
            // No rebirth/tribe-transition system populates a real "previous tribe" yet -- defaults to the
            // character's current tribe (a "never transferred" inference).
            PreviousTribe = data.Tribe,
            // This zone's own clock plus whatever remained of the revive timer in the source zone.
            ReviveAtZoneClock = _clock + (data.ReviveRemaining ?? TimeSpan.Zero),
            // The only write site for this field: a one-shot ~10s combat grace period starting now, for every
            // arrival. Combat code must never write this field again after today.
            ZoneEntryAtZoneClock = _clock
        };

        // Items/Stats are already-computed data handed down through the command -- a plain copy, never a
        // catalog lookup, keeping this tick-thread method's cost independent of WorldDataCache size.
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

        if (data.FriendsBySlot is { } friends)
            foreach (var (slot, friendId) in friends)
                state.Friends[slot] = friendId;

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
        state.LastSeenPetItemId = data.Items is { } petScanItems
            ? PetSlots.ResolveEquippedPetItemId(petScanItems)
            : 0;

        var cell = _grid.CellOf(state.PosX, state.PosZ);
        state.CurrentCell = cell;

        if (!_players.TryAdd(characterId, state))
        {
            logger.LogWarning(
                "Character {CharacterId} entered zone {MapId} while already tracked -- ignoring duplicate Enter",
                characterId, MapId);
            return;
        }

        _grid.Add(characterId, cell);

        // Marked dirty on entry so a handoff's map change reaches SQL even if the player never moves again;
        // on a fresh world entry the sequence already equals the DB baseline, so this flush is a deliberate no-op.
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Mutual visibility: existing neighbors learn about the new arrival, and vice versa. The self-spawn
        // packet is sent directly by the registration handler before this command is posted.
        var others = _grid.Neighbors(cell).Where(id => id != characterId).ToArray();

        // Direct send to each neighbor's own session for the new arrival's view of them; the new arrival
        // itself is announced to neighbors via the broadcast below. Swapping these would send the new
        // arrival's own data to itself and leave it blind to everyone already there.
        foreach (var otherId in others)
            if (_players.TryGetValue(otherId, out var other))
                SendAvatarAction(state.Session, other);

        BroadcastAvatarAction(others, state);
    }

    private void HandleLeave(int characterId, Zone? handoffTarget, (float X, float Y, float Z)? handoffPosition = null)
    {
        if (!_players.TryRemove(characterId, out var state))
            return;

        _grid.Remove(characterId, state.CurrentCell);

        if (handoffTarget is null)
            // Plain leave (disconnect). No despawn/logout opcode exists in the M1 client protocol -- nearby
            // clients simply stop receiving updates for this entity. A documented gap, not an oversight.
            return;

        // In-process map transfer: the live state is snapshotted into the Enter command and travels inside
        // it -- this zone has already forgotten the player (TryRemove above), so the character never exists
        // in two zones at once.
        var enterData = ZoneTransfer.CreateEnterData(state, handoffTarget.MapId, _clock, handoffPosition);

        if (!handoffTarget.Post(ZoneCommand.Enter(characterId, enterData)))
        {
            // The player is now in no zone, permanently invisible, while their client still believes it is in
            // the world. Fail loudly and drop the connection rather than leave a phantom.
            logger.LogError(
                "Zone {TargetMapId} inbox full: dropped handoff Enter for character {CharacterId} from zone {MapId} -- aborting session",
                handoffTarget.MapId, characterId, MapId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        // Plain reference write: atomic, and a stale read by a racing movement handler is benign.
        if (state.Session is ZoneClientSession zoneSession)
            zoneSession.CurrentZone = handoffTarget;
    }

    /// <summary>
    ///     Kills <paramref name="characterId" /> in this zone: Life -&gt; 0, <see cref="PlayerRuntimeState.IsDead" />
    ///     set, and an automatic revive scheduled <see cref="SimulationClock.DeathReviveDelay" /> later. Public
    ///     and characterId-addressed so the combat handler never needs its own <see cref="PlayerRuntimeState" />
    ///     reference -- only this zone's own tick may construct/mutate one. A no-op if the character is not
    ///     tracked here, or already dead (so a duplicate killing blow never re-arms the revive timer).
    /// </summary>
    /// <remarks>
    ///     XP penalty on death is applied here, but only for <see cref="DeathCause.MonsterKill" /> -- a PvP
    ///     death instead rewards the killer (not implemented, see <see cref="Combat.CombatResolver" />'s
    ///     remarks) and does not dock the victim's XP.
    /// </remarks>
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
        state.ReviveAtZoneClock = _clock + SimulationClock.DeathReviveDelay;

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (cause == DeathCause.MonsterKill)
            ApplyDeathExperienceLoss(state);

        // Death pose (aAction.aSort = 12) so nearby clients see the character fall immediately. Self is
        // excluded: the combat handler tells the dying player's own client via combat-result packets instead.
        var deathAction = new ActionInfo
        {
            Type = 0,
            Sort = 12,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, deathAction);
    }

    /// <summary>
    ///     The MvP XP-loss branch of <see cref="ApplyDeath" /> (<c>S07_MyGame02.cpp:3445-3489</c>): refuses
    ///     below level 10 or at/above the level cap (loses CP instead, <see cref="ExperienceFormulas.CpLossAtLevelCap" />).
    ///     A level outside the catalog contributes 0 (no loss).
    /// </summary>
    private void ApplyDeathExperienceLoss(PlayerRuntimeState state)
    {
        switch (state.Level)
        {
            case < ExperienceFormulas.MinimumLevelForDeathExperienceLoss:
                return;
            case >= ExperienceFormulas.MaxLimitLevel:
                state.ContributionPoints -= ExperienceFormulas.CpLossAtLevelCap;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                return;
        }

        if (!worldData.LevelsByLevel.TryGetValue(state.Level, out var levelRow))
            return;

        var loss = ExperienceFormulas.ComputeDeathExperienceLoss(state.Experience, levelRow.ExpRangeMin);
        if (loss <= 0)
            return;

        state.Experience -= loss;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    /// <summary>Sweeps every dead player whose scheduled revive (<see cref="ApplyDeath" />) is due this tick.</summary>
    private void ProcessPendingRevives()
    {
        List<(int CharacterId, PlayerRuntimeState State)>? due = null;

        foreach (var (characterId, state) in _players)
        {
            if (!state.IsDead || _clock < state.ReviveAtZoneClock)
                continue;

            (due ??= []).Add((characterId, state));
        }

        if (due is null)
            return;

        foreach (var (characterId, state) in due)
            Revive(characterId, state);
    }

    /// <summary>
    ///     Executes a due revive: HP forced to 1 regardless of MaxLife, in place (same zone/position) -- the
    ///     legacy only auto-clears the death flag locally; an actual "return to town" transfer is always
    ///     client-driven (CZ_DEMAND_ZONE_SERVER_INFO_2), already handled by <c>ZoneMoveHandler</c>. A prior
    ///     version of this auto-timer also teleported to a hardcoded tribe capital, which silently misfired on
    ///     a player revived mid zone-handoff; reviving in place removes that bug class by construction.
    /// </summary>
    private void Revive(int characterId, PlayerRuntimeState state)
    {
        state.IsDead = false;
        state.Life = 1;

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        SendAvatarAction(state.Session, state);
        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
    }

    private void HandleMove(int characterId, in ActionInfo action)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        var now = DateTime.UtcNow;

        if (!movementRules.IsPlausible(state, in action, now, Geometry))
        {
            // Reject: reply with the player's own last-known-good state so the client corrects itself -- no
            // dedicated ForcePositionSync packet exists in the M1 protocol, so this reuses ZC_AVATAR_ACTION_RECV.
            SendAvatarAction(state.Session, state);
            return;
        }

        state.PosX = action.Location[0];
        state.PosY = action.Location[1];
        state.PosZ = action.Location[2];
        state.Heading = action.Front;
        state.LastMoveUtc = now;
        state.FlushSequence++;

        // Mirrors the legacy's persistent mDATA.aAction fields for every accepted action, not just movement --
        // sit/meditation and skill casts ride the same unified CZ_AVATAR_ACTION_SEND wire shape.
        state.ActionSort = action.Sort;
        state.ActionSkillNumber = action.SkillNumber;
        state.ActionSkillGradeNum1 = action.SkillGradeNum1;
        state.ActionSkillGradeNum2 = action.SkillGradeNum2;

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(characterId, state.CurrentCell, newCell);
        state.CurrentCell = newCell;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Self is excluded: the legacy client applies its own movement locally (client-side prediction) and
        // does not need its own action echoed back to it.
        var neighbors = _grid.Neighbors(newCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, action);

        if (action.Sort == 30)
            ApplySkillCast(state, action);
    }

    /// <summary>
    ///     Op156 CZ_UPDATE_PET_ACTION_SEND -- copies only the pet sub-fields of <paramref name="action" />,
    ///     matching the legacy handler exactly (no reply, no broadcast; the update rides along on the next
    ///     periodic full-avatar keep-alive rebroadcast instead).
    /// </summary>
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

    /// <summary>
    ///     Non-attack skill cast (Sort=30). Damage-dealing skills do not go through here -- those ride
    ///     <c>CZ_PROCESS_ATTACK_SEND</c>'s <c>AttackActionValue1==2</c> path instead. Silent no-op on every
    ///     failure path, matching the legacy's own bare early-return contract (no dedicated failure packet).
    /// </summary>
    private void ApplySkillCast(PlayerRuntimeState state, ActionInfo action)
    {
        // One skill-cast per legacy tick. Null (never cast) always passes.
        if (state.LastSkillCastAtZoneClock is { } lastCast && _clock - lastCast < SimulationClock.LegacyTick)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var gradePoints = action.SkillGradeNum1 + action.SkillGradeNum2;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var result = SkillCastResolver.TryCast(skillDef, gradePoints, state.Mana, maxLife, weaponSort);
        if (!result.Success)
            return;

        state.LastSkillCastAtZoneClock = _clock;
        state.Mana -= result.ManaCost;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        switch (result.Kind)
        {
            case SkillEffectKind.SelfBuff:
                ApplySkillBuffWrites(state, result.BuffWrites);
                break;
            case SkillEffectKind.HealLife:
                ApplyTargetedHeal(action, true, result.HealAmount);
                break;
            case SkillEffectKind.HealMana:
                ApplyTargetedHeal(action, false, result.HealAmount);
                break;
        }
    }

    private void ApplySkillBuffWrites(PlayerRuntimeState state, ImmutableArray<SkillCastResolver.BuffWrite> writes)
    {
        if (writes.IsEmpty)
            return;

        var changedSlots = new int[35];
        foreach (var write in writes)
        {
            if (write.Slot is < 0 or >= 35) continue;
            state.Buffs.Buff[write.Slot * 2] = write.Value;
            state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;
            changedSlots[write.Slot] = 1;
        }

        RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    /// <summary>
    ///     Targeted heal (skills 106-111): resolves the target against this same zone, clamps the flat heal
    ///     amount to remaining capacity (<c>S07_MyGame03.cpp:9500-9510/9563-9573</c>). A target at full HP/MP,
    ///     or not found/dead, silently receives nothing.
    /// </summary>
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

    /// <summary>
    ///     Recomputes <see cref="PlayerRuntimeState.Stats" /> from the live Equipment container + current
    ///     <see cref="PlayerRuntimeState.Buffs" /> snapshot, and broadcasts the updated buff view to this
    ///     player and their AOI neighbors. Unlike the legacy's live-read wrappers,
    ///     <see cref="PlayerRuntimeState.Stats" /> is an explicit cache that must be refreshed on every buff change.
    /// </summary>
    public void RecomputeStatsAndBroadcastBuffs(PlayerRuntimeState state, int[] changedSlots)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.Title, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        state.Stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);

        var response = new AvatarEffectStateResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            EffectValue = state.Buffs.Buff,
            EffectValueState = changedSlots
        };

        state.Session.Send(response);
        foreach (var neighborId in _grid.Neighbors(state.CurrentCell))
        {
            if (neighborId == state.CharacterId) continue;
            if (_players.TryGetValue(neighborId, out var neighbor))
                neighbor.Session.Send(response);
        }
    }

    private static void SendAvatarAction(IPacketSession session, PlayerRuntimeState state)
    {
        session.Send(BuildAvatarActionRecv(state));
    }

    /// <summary>
    ///     Serialize-once broadcast: the frame is written to a rented buffer once and copied into each recipient's own
    ///     pipe.
    /// </summary>
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
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    // A recipient whose transport is already gone must not abort the broadcast for every other one.
                    logger.LogError(ex, "Zone {MapId} broadcast to character {RecipientId} failed", MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state)
    {
        return BuildAvatarActionRecv(state, new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        });
    }

    /// <summary>
    ///     Internal (not private): reused by <c>ZoneMoveHandler</c> to build the self-spawn packet for a
    ///     zone-transfer, with an explicit <paramref name="action" /> carrying the just-resolved arrival
    ///     position rather than <paramref name="state" />'s own (still the source zone's).
    /// </summary>
    public static AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state, ActionInfo action)
    {
        return new AvatarActionResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Data = new ObjectForAvatar
            {
                VisibleState = 0,
                SpecialState = 0,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = "",
                GuildRole = 0,
                CallName = "",
                GuildMarkEffect = 0,
                Name = state.Name,
                Tribe = state.Tribe,
                PreviousTribe = 0,
                Gender = state.Gender,
                HeadType = state.HeadType,
                FaceType = state.FaceType,
                Level1 = state.Level,
                Level2 = state.Level2,
                // Reflects the live Equipment container instead of a hardcoded blank.
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
                EffectValueForView = new int[35],
                PartyName = "",
                DuelState = new int[3],
                PShopState = 0,
                PShopName = "",
                CostumeNumber = 0,
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
                CostumeState = 0,
                StellarCoreNumber = 0
            },
            CheckChangeActionState = 0
        };
    }
}
