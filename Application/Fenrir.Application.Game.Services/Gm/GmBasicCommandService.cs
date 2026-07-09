using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmBasicCommandService" />'s own remarks for the full wire-level contract summary and the
///     three deliberate simplifications (FIND's process-local lookup, CALL's single-target-only branch,
///     TRIBEBANK/522's dead-code shape). Citations: Server/ts25zone/S04_MyWork04.cpp:290-339,925-1622,2105-2123
///     (the whole tSort switch body this type's sixteen methods each cover one case of) ;
///     Server/ts25zone/S05_MyTransfer.cpp:514-567,1150-1204 (dedicated-response send helpers) ;
///     Server/ts25zone/H05_MyTransfer.h:28-37 (<c>B_GM_COMMAND_INFO</c>, <see cref="GmCommandResponse" />'s
///     legacy counterpart) ; Server/ts25zone/UpperCom/S06_MyUpperCom01.cpp:315-338 (FIND's blocking upstream
///     round trip, NOT reproduced -- see this type's own remarks) ; Server/ts25zone/H07_MyGame.h:592-602,
///     Server/ts25zone/S07_MyGame03.cpp:4422-4452 (by-name lookup scope: process-local, excludes the invoker) ;
///     Server/Header/Protocol/CLIENT.h:195-207,520-528 ; Server/Header/Protocol/DEFINE.h:1-80,275-284,365-367,
///     604-609,700-763 ; Server/Header/Protocol/STRUCT.h:1260-1298,1518-1527 ; Server/Header/Protocol/ZONE.h:
///     355-468,925-944,1430-1441,1580-1589.
/// </summary>
public sealed class GmBasicCommandService(
    ZoneRegistry zones,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<GmBasicCommandService> logger) : IGmBasicCommandService
{
    /// <summary>Legacy tResult's own default-initialized/rejected value (S04_MyWork04.cpp:305).</summary>
    private const int FailureResult = 1;

    private const int SuccessResult = 0;

    private const int ShowSort = 502;
    private const int MoveSelfSort = 507;
    private const int DieSort = 508;
    private const int TribeSort = 510;
    private const int EquipSort = 511;
    private const int UnequipSort = 512;
    private const int FindSort = 513;
    private const int CallSort = 514;
    private const int MoveToTargetSort = 515;
    private const int NchatSort = 516;
    private const int YchatSort = 517;
    private const int KickSort = 518;
    private const int TribeBankSort = 520;
    private const int LevelSort = 521;
    private const int StatEditSort = 522;

    /// <summary>
    ///     Legacy literal tag <c>B_GM_COMMAND_INFO</c> writes verbatim as <see cref="GmCommandResponse" />.Sort
    ///     for FIND -- NOT the outer switch's tSort (513). See S05_MyTransfer.cpp:1159-1164 and
    ///     S04_MyWork04.cpp:1319.
    /// </summary>
    private const int FindGmDataTag = 1;

    /// <summary>
    ///     Legacy literal tag <c>B_GM_COMMAND_INFO</c> writes verbatim as <see cref="GmCommandResponse" />.Sort
    ///     for both CALL and MOVE-to-target -- NOT the outer switch's tSort (514/515). See
    ///     S05_MyTransfer.cpp:1159-1164 and S04_MyWork04.cpp:1348,1406.
    /// </summary>
    private const int CallMoveGmDataTag = 2;

    /// <summary>AvatarStatUpdateResponse.Sort for the HIDE/SHOW dedicated notification (STRUCT.h:1518-1524).</summary>
    private const int VisibilityStatSort = 9;

    private const int HiddenVisibleState = 0;
    private const int ShownVisibleState = 1;

    private const int NchatSpecialState = 2;
    private const int YchatSpecialState = 0;
    private const int EquipSpecialState = 1;
    private const int UnequipSpecialState = 0;

    /// <summary>Tribe selector's special "no PreviousTribe change" value -- see TRIBE's own contract.</summary>
    private const int Tribe4SpecialValue = 3;

    /// <summary>H01_MainApplication.h:76 -- the live monster-instance table's fixed capacity.</summary>
    private const int MonsterInstanceCapacity = 3000;

    private const int GmDataSize = 100; // MAX_TRIBE_WORK_SIZE, matches GmCommandResponse.GmData

    private const int BaseLevelCap = 145; // DEFINE.h:604 -- LevelProgressionCalculator.MaxLevel
    private const int HighLevelSpan = 12; // DEFINE.h:605
    private const int RebirthSpan = 12;
    private const int LevelCombinedCapacity = BaseLevelCap + HighLevelSpan + RebirthSpan; // 169

    public async ValueTask HandleVisibilityAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, sort))
            return;

        var newVisibleState = sort == ShowSort ? ShownVisibleState : HiddenVisibleState;

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, VisibleState: newVisibleState), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped HIDE/SHOW mirror for character {CharacterId} (sort {Sort})",
                zone.MapId, state.CharacterId, sort);

        // Dedicated visibility-change notification, self-only, THEN the shared generic acknowledgment --
        // double-acknowledgment shape (S04_MyWork04.cpp:933-958, S05_MyTransfer.cpp:519-541).
        zoneSession.Send(
            new AvatarStatUpdateResponse { Sort = VisibilityStatSort, Value = newVisibleState, Value2 = 0 });
        SendAck(zoneSession, sort, data, SuccessResult);
    }

    public async ValueTask HandleSelfTeleportAsync(byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, MoveSelfSort))
            return;

        if (!GmMoveCoordinatePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId,
                    TeleportTo: (payload.Location[0], payload.Location[1], payload.Location[2])),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped self-teleport mirror for character {CharacterId}",
                zone.MapId, state.CharacterId);

        SendAck(zoneSession, MoveSelfSort, data, SuccessResult);
    }

    public async ValueTask HandleForceKillMonsterAsync(byte[] data, ZoneClientSession zoneSession, Zone zone,
        CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, DieSort))
            return;

        if (!GmMonsterInstanceIndexPayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result = FailureResult;
        var index = payload.MonsterIndex;
        if (index is >= 0 and < MonsterInstanceCapacity && zone.TryGetMonster(index, out var monster) &&
            monster is not null)
        {
            // Audit-logged BEFORE the mutation, matching this sub-operation's own citation ordering.
            await eventLog.LogAsync(GmActionEventCodes.MonsterForceKill, EventLogCategory.GmAction,
                zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null,
                monster.Template.MonsterId,
                null, 1, $"ServerIndex={index};MonsterName={monster.Template.Name}", cancellationToken);

            // Lethal, unattributed damage -- no attacker credit means MonsterSpawnScheduler.ProcessDeath's own
            // credited-character gate leaves this kill unattributed, so no loot/experience is granted; the
            // normal death pipeline still arms this instance's respawn timer from "now", matching this
            // sub-operation's own contract.
            zone.TryDamageMonster(index, monster.Life, null, out _, out _);
            result = SuccessResult;
        }

        SendAck(zoneSession, DieSort, data, result);
    }

    public async ValueTask HandleTribeChangeAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, TribeSort))
            return;

        if (!GmTribeChangePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var selector = payload.Tribe;
        if (selector is < 0 or > Tribe4SpecialValue || selector == state.Tribe)
            // Silently dropped: no mutation, no disconnect, no acknowledgment of any kind.
            return;

        var command = new TribeProgressZoneCommand(state.CharacterId, Tribe: (byte)selector,
            PreviousTribe: selector == Tribe4SpecialValue ? null : (byte)selector, QuestProgress: QuestProgress.None);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped GM TRIBE mirror for character {CharacterId} (selector {Selector})",
                zone.MapId, state.CharacterId, selector);

        logger.LogWarning(
            "Character {CharacterId} applied the Basic-tier TRIBE self-command (selector {Selector}) -- forcing logout, no reply. PreviousTribe persistence gap: see IGmBasicCommandService.HandleTribeChangeAsync's own remarks.",
            state.CharacterId, selector);

        // Normal successful completion for this command, not an error path -- see this method's own remarks.
        zoneSession.Abort(DisconnectReason.GmCommandLogout);
    }

    public async ValueTask HandleSelfSpecialStateAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, sort))
            return;

        var newSpecialState = sort == EquipSort ? EquipSpecialState : UnequipSpecialState;

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, SpecialState: newSpecialState), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped EQUIP/UNEQUIP mirror for character {CharacterId} (sort {Sort})",
                zone.MapId, state.CharacterId, sort);

        SendAck(zoneSession, sort, data, SuccessResult);
    }

    public ValueTask HandleFindAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, FindSort))
            return ValueTask.CompletedTask;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return ValueTask.CompletedTask;
        }

        // Deliberate, flagged simplification of legacy's genuinely cluster-wide blocking upstream lookup -- see
        // IGmBasicCommandService's own class remarks. Zero-filled GmData ("not found") is an inferred sentinel,
        // not a confirmed legacy value (flagged for cpp-ts25-explorer re-check in the source contract).
        // Self-exclusion mirrors this family's other by-name lookups (shared framing) even though real FIND
        // does not use this process-local search at all.
        var gmData = new byte[GmDataSize];
        if (zones.TryGetPlayerAndZoneByName(payload.TargetName, out var found, out var targetZone) &&
            found!.CharacterId != state.CharacterId)
            BinaryPrimitives.WriteInt32LittleEndian(gmData, targetZone!.MapId);

        zoneSession.Send(new GmCommandResponse { Sort = FindGmDataTag, GmData = gmData });
        SendAck(zoneSession, FindSort, data, SuccessResult);
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleCallAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, CallSort))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out var targetZone);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            SendAck(zoneSession, CallSort, data, FailureResult);
            return;
        }

        var destination = (state.PosX, state.PosY, state.PosZ);
        if (!await targetZone!.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(target.CharacterId, TeleportTo: destination,
                    NeighborActionBroadcast: true),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CALL mirror for target character {CharacterId}",
                targetZone.MapId, target.CharacterId);

        await eventLog.LogAsync(GmActionEventCodes.Call, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, ((ZoneClientSession)target.Session).AccountId, target.CharacterId, null, null,
            null, null, null, 1, $"TargetName={target.Name}", cancellationToken);

        var gmData = new byte[GmDataSize];
        new GmMoveCoordinatePayload { Location = [destination.Item1, destination.Item2, destination.Item3] }
            .Write(gmData);
        target.Session.Send(new GmCommandResponse { Sort = CallMoveGmDataTag, GmData = gmData });

        SendAck(zoneSession, CallSort, data, SuccessResult);
    }

    public async ValueTask HandleMoveToTargetAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, MoveToTargetSort))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out _);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            SendAck(zoneSession, MoveToTargetSort, data, FailureResult);
            return;
        }

        var destination = (target.PosX, target.PosY, target.PosZ);
        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, TeleportTo: destination), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped MOVE-to-target mirror for character {CharacterId}",
                zone.MapId, state.CharacterId);

        var gmData = new byte[GmDataSize];
        new GmMoveCoordinatePayload { Location = [destination.Item1, destination.Item2, destination.Item3] }
            .Write(gmData);
        zoneSession.Send(new GmCommandResponse { Sort = CallMoveGmDataTag, GmData = gmData });

        SendAck(zoneSession, MoveToTargetSort, data, SuccessResult);
    }

    public async ValueTask HandleTargetSpecialStateAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, sort))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out var targetZone);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            // Not-found falls through to the shared closing step in legacy (a bare `break` inside the
            // switch, not `return`) -- only the target-FOUND branch below is truly silent. See this
            // method's own remarks in IGmBasicCommandService.HandleTargetSpecialStateAsync.
            SendAck(zoneSession, sort, data, FailureResult);
            return;
        }

        var newSpecialState = sort == NchatSort ? NchatSpecialState : YchatSpecialState;

        // Shared log point for both NCHAT and YCHAT -- see GmActionEventCodes.Chat's own remarks.
        await eventLog.LogAsync(GmActionEventCodes.Chat, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, ((ZoneClientSession)target.Session).AccountId, target.CharacterId, null, null,
            null, null, null, (byte)newSpecialState, $"TargetName={target.Name}", cancellationToken);

        if (!await targetZone!.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(target.CharacterId, SpecialState: newSpecialState), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped NCHAT/YCHAT mirror for target character {CharacterId} (sort {Sort})",
                targetZone.MapId, target.CharacterId, sort);

        // No acknowledgment of any kind -- matches legacy's own `return` (not `break`) for this case.
    }

    public async ValueTask HandleKickAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, KickSort))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out _);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            SendAck(zoneSession, KickSort, data, FailureResult);
            return;
        }

        await eventLog.LogAsync(GmActionEventCodes.Kick, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, ((ZoneClientSession)target.Session).AccountId, target.CharacterId, null, null,
            null, null, null, 1, $"TargetName={target.Name}", cancellationToken);

        ((ZoneClientSession)target.Session).Abort(DisconnectReason.GmKicked);

        SendAck(zoneSession, KickSort, data, SuccessResult);
    }

    public ValueTask HandleTribeBankAsync(byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, TribeBankSort))
            return ValueTask.CompletedTask;

        // Dead code behind a live gate -- see IGmBasicCommandService's own class remarks. Nothing between the
        // gate and here executes in the shipped legacy binary; always the default-failure outcome.
        SendAck(zoneSession, TribeBankSort, data, FailureResult);
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleLevelSetAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, LevelSort))
            return;

        if (!GmLevelSetPayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var requested = payload.Level;
        if (requested > LevelCombinedCapacity)
        {
            SendAck(zoneSession, LevelSort, data, FailureResult);
            return;
        }

        short newLevel;
        short newLevel2;
        int newRebirthCount;
        long newExperience;
        int newExp2;

        var maxBaseExperience =
            worldData.LevelsByLevel.TryGetValue(BaseLevelCap, out var maxRow) ? maxRow.ExpRangeMax : 0;

        if (requested <= BaseLevelCap)
        {
            newLevel = (short)requested;
            newLevel2 = 0;
            newRebirthCount = 0;
            newExperience = worldData.LevelsByLevel.TryGetValue(newLevel, out var row) ? row.ExpRangeMin : 0;
            // Plain base-level tier carries no high-level/rebirth component -- Exp2 is cleared (confirmed,
            // S04_MyWork04.cpp:1566-1580's own tier-1 branch never touches aExp2).
            newExp2 = 0;
        }
        else if (requested <= BaseLevelCap + HighLevelSpan)
        {
            newLevel = BaseLevelCap;
            newLevel2 = (short)(requested - BaseLevelCap);
            newRebirthCount = 0;
            newExperience = maxBaseExperience;
            // wAvatar.aExp2 = mLEVEL.ReturnHighExpValue(wAvatar.aLevel2) -- S04_MyWork04.cpp:1566-1580 ;
            // GameSystem_01_Level.cpp:712-719,319-330 (mRangeForHigh[Level2-1], ported as
            // RebirthProgression.HighLevelExpTable).
            newExp2 = RebirthProgression.HighLevelExpTable[newLevel2 - 1];
        }
        else
        {
            newLevel = BaseLevelCap;
            newLevel2 = HighLevelSpan;
            newRebirthCount = requested - BaseLevelCap - HighLevelSpan;
            newExperience = maxBaseExperience;
            // wAvatar.aExp2 = mLEVEL.ReturnHighExpValue(MAX_LIMIT_HIGH_LEVEL_NUM) -- Level2 pinned at its own
            // cap for the rebirth tier, same citations as the high-level branch above.
            newExp2 = RebirthProgression.HighLevelExpTable[RebirthProgression.MaxHighLevel - 1];
        }

        // Recompute derived combat stats from the NEW level/rebirth values and current equipment, then heal to
        // the newly computed maximum unconditionally (not gated on being alive, unlike ordinary level-up) --
        // see this method's own contract.
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack) ? petStack.ItemId : 0;
        var petContribution =
            PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            newLevel, state.Tribe, state.PreviousTribe, state.Title, state.Halo, newRebirthCount);
        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);

        var command = new TribeProgressZoneCommand(state.CharacterId, Level: newLevel, Level2: newLevel2,
            RebirthCount: newRebirthCount, Experience: newExperience, Exp2: newExp2,
            UpdatedStats: stats, MaxLife: stats.MaxLife, MaxMana: stats.MaxMana, Life: stats.MaxLife,
            Mana: stats.MaxMana);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped GM LEVEL mirror for character {CharacterId} (requested {Requested})",
                zone.MapId, state.CharacterId, requested);

        SendAck(zoneSession, LevelSort, data, SuccessResult);
    }

    public ValueTask HandleStatEditAsync(byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!MeetsTierOrAbort(zoneSession, StatEditSort))
            return ValueTask.CompletedTask;

        // Dead code behind a live gate -- see IGmBasicCommandService's own class remarks.
        SendAck(zoneSession, StatEditSort, data, FailureResult);
        return ValueTask.CompletedTask;
    }

    private bool MeetsTierOrAbort(ZoneClientSession zoneSession, int sort)
    {
        if (zoneSession.MeetsGmTier(GmCommandTier.Basic))
            return true;

        logger.LogWarning(
            "Character {CharacterId} attempted the Basic-tier GM command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
            zoneSession.CharacterId, sort);
        zoneSession.Abort(DisconnectReason.Faulted);
        return false;
    }

    private static void SendAck(ZoneClientSession zoneSession, int sort, byte[] data, int result)
    {
        zoneSession.Send(new GenericActionResponse { Result = result, Sort = sort, Data = data, RuneValue = 0 });
    }
}
