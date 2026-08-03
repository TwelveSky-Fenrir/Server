using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmBasicCommandService(
    ZoneRegistry zones,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ICharacterShardLocationRepository characterShardLocations,
    PartyRegistry partyRegistry,
    ILogger<GmBasicCommandService> logger) : IGmBasicCommandService
{
    private const int FailureResult = 1;

    private const int SuccessResult = 0;

    private const int HideSort = 501;
    private const int ShowSort = 502;
    private const int MoveSelfSort = 507;
    private const int MoveToPositionSort = 528;
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

    private const int FindGmDataTag = 1;

    private const int CallMoveGmDataTag = 2;

    private const int VisibilityStatSort = 9;

    private const int HiddenVisibleState = 0;
    private const int ShownVisibleState = 1;

    private const int NchatSpecialState = 2;
    private const int YchatSpecialState = 0;
    private const int EquipSpecialState = 1;
    private const int UnequipSpecialState = 0;

    private const int Tribe4SpecialValue = 3;

    private const int MonsterInstanceCapacity = 3000;

    private const int GmDataSize = 100;

    private const int BaseLevelCap = 145;
    private const int HighLevelSpan = 12;
    private const int RebirthSpan = 12;
    private const int LevelCombinedCapacity = BaseLevelCap + HighLevelSpan + RebirthSpan;

    public async ValueTask HandleVisibilityAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, sort, cancellationToken))
            return;

        var newVisibleState = sort == ShowSort ? ShownVisibleState : HiddenVisibleState;

        await AuditAsync(sort, zoneSession, null, null, GmCommandCatalog.OutcomeExecuted,
            $"VisibleState={newVisibleState}", cancellationToken);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, VisibleState: newVisibleState), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped HIDE/SHOW mirror for character {CharacterId} (sort {Sort})",
                zone.MapId, state.CharacterId, sort);

        zoneSession.Send(
            new AvatarStatUpdateResponse { Sort = VisibilityStatSort, Value = newVisibleState, Value2 = 0 });
        SendAck(zoneSession, sort, data, SuccessResult);
    }

    public ValueTask HandleSelfTeleportAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        return TeleportToRawPositionAsync(MoveSelfSort, data, zoneSession, state, zone, cancellationToken);
    }

    public ValueTask HandleMoveToPositionAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        return TeleportToRawPositionAsync(MoveToPositionSort, data, zoneSession, state, zone, cancellationToken);
    }

    public async ValueTask HandleForceKillMonsterAsync(byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, DieSort, cancellationToken))
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
            await eventLog.LogAsync(GmCommandCatalog.Resolve(DieSort).AuditEventCode, EventLogCategory.GmAction,
                zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null,
                monster.Template.MonsterId,
                null, GmCommandCatalog.OutcomeExecuted,
                $"Command=DIE;Sort={DieSort};ServerIndex={index};MonsterName={monster.Template.Name}",
                cancellationToken);

            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(state.CharacterId, GmForceKillMonsterServerIndex: index),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped GM DIE mirror for character {CharacterId} (monster server index {ServerIndex})",
                    zone.MapId, state.CharacterId, index);

            result = SuccessResult;
        }
        else
        {
            await AuditAsync(DieSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"ServerIndex={index}", cancellationToken);
        }

        SendAck(zoneSession, DieSort, data, result);
    }

    public async ValueTask HandleTribeChangeAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, TribeSort, cancellationToken))
            return;

        if (!GmTribeChangePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var selector = payload.Tribe;
        if (selector is < 0 or > Tribe4SpecialValue || selector == state.Tribe)
        {
            await AuditAsync(TribeSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"Selector={selector};FromTribe={state.Tribe}", cancellationToken);
            return;
        }

        await AuditAsync(TribeSort, zoneSession, null, null, GmCommandCatalog.OutcomeExecuted,
            $"Selector={selector};FromTribe={state.Tribe}", cancellationToken);

        var command = new TribeProgressZoneCommand(state.CharacterId, Tribe: (byte)selector,
            PreviousTribe: selector == Tribe4SpecialValue ? null : (byte)selector, QuestProgress: QuestProgress.None);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped GM TRIBE mirror for character {CharacterId} (selector {Selector})",
                zone.MapId, state.CharacterId, selector);

        logger.LogWarning(
            "Character {CharacterId} applied the Basic-tier TRIBE self-command (selector {Selector}) -- forcing logout, no reply. PreviousTribe persistence gap: see IGmBasicCommandService.HandleTribeChangeAsync's own remarks.",
            state.CharacterId, selector);

        zoneSession.Abort(DisconnectReason.GmCommandLogout);
    }

    public async ValueTask HandleSelfSpecialStateAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, sort, cancellationToken))
            return;

        var newSpecialState = sort == EquipSort ? EquipSpecialState : UnequipSpecialState;

        await AuditAsync(sort, zoneSession, null, null, GmCommandCatalog.OutcomeExecuted,
            $"SpecialState={newSpecialState}", cancellationToken);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, SpecialState: newSpecialState), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped EQUIP/UNEQUIP mirror for character {CharacterId} (sort {Sort})",
                zone.MapId, state.CharacterId, sort);

        SendAck(zoneSession, sort, data, SuccessResult);
    }

    public async ValueTask HandleFindAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, FindSort, cancellationToken))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var gmData = new byte[GmDataSize];
        var foundMapId = -1;
        if (zones.TryGetPlayerAndZoneByName(payload.TargetName, out _, out var localZone))
        {
            foundMapId = localZone!.MapId;
            BinaryPrimitives.WriteInt32LittleEndian(gmData, foundMapId);
        }
        else
        {
            var remote = await characterShardLocations.FindByNameAsync(payload.TargetName, cancellationToken);
            if (remote is not null)
            {
                foundMapId = remote.MapId;
                BinaryPrimitives.WriteInt32LittleEndian(gmData, foundMapId);
            }
        }

        await AuditAsync(FindSort, zoneSession, null, null,
            foundMapId >= 0 ? GmCommandCatalog.OutcomeExecuted : GmCommandCatalog.OutcomeRejected,
            $"TargetName={payload.TargetName};FoundMapId={foundMapId}", cancellationToken);

        zoneSession.Send(new GmCommandResponse { Sort = FindGmDataTag, GmData = gmData });
        SendAck(zoneSession, FindSort, data, SuccessResult);
    }

    public async ValueTask HandleCallAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, CallSort, cancellationToken))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out var targetZone);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            await AuditAsync(CallSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"TargetName={payload.TargetName}", cancellationToken);
            SendAck(zoneSession, CallSort, data, FailureResult);
            return;
        }

        if (zone.MapId == Zone124DuelOverrideResolver.Zone124MapId)
        {
            await HandleZone124PartyPullAsync(data, zoneSession, state, zone, target, targetZone!,
                cancellationToken);
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

        await AuditAsync(CallSort, zoneSession, ((IZoneSession)target.Session).AccountId, target.CharacterId,
            GmCommandCatalog.OutcomeExecuted, $"TargetName={target.Name}", cancellationToken);

        var gmData = new byte[GmDataSize];
        new GmMoveCoordinatePayload { Location = [destination.Item1, destination.Item2, destination.Item3] }
            .Write(gmData);
        target.Session.Send(new GmCommandResponse { Sort = CallMoveGmDataTag, GmData = gmData });

        SendAck(zoneSession, CallSort, data, SuccessResult);
    }

    public async ValueTask HandleMoveToTargetAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, MoveToTargetSort, cancellationToken))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out _);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            await AuditAsync(MoveToTargetSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"TargetName={payload.TargetName}", cancellationToken);
            SendAck(zoneSession, MoveToTargetSort, data, FailureResult);
            return;
        }

        await AuditAsync(MoveToTargetSort, zoneSession, ((IZoneSession)target.Session).AccountId,
            target.CharacterId, GmCommandCatalog.OutcomeExecuted, $"TargetName={target.Name}", cancellationToken);

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

    public async ValueTask HandleTargetSpecialStateAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, sort, cancellationToken))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out var targetZone);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            await AuditAsync(sort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"TargetName={payload.TargetName}", cancellationToken);
            SendAck(zoneSession, sort, data, FailureResult);
            return;
        }

        var newSpecialState = sort == NchatSort ? NchatSpecialState : YchatSpecialState;

        await AuditAsync(sort, zoneSession, ((IZoneSession)target.Session).AccountId, target.CharacterId,
            GmCommandCatalog.OutcomeExecuted, $"TargetName={target.Name};SpecialState={newSpecialState}",
            cancellationToken);

        if (!await targetZone!.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(target.CharacterId, SpecialState: newSpecialState), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped NCHAT/YCHAT mirror for target character {CharacterId} (sort {Sort})",
                targetZone.MapId, target.CharacterId, sort);
    }

    public async ValueTask HandleKickAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, KickSort, cancellationToken))
            return;

        if (!GmTargetNamePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var found = zones.TryGetPlayerAndZoneByName(payload.TargetName, out var target, out _);
        if (!found || target!.CharacterId == state.CharacterId)
        {
            await AuditAsync(KickSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"TargetName={payload.TargetName}", cancellationToken);
            SendAck(zoneSession, KickSort, data, FailureResult);
            return;
        }

        await AuditAsync(KickSort, zoneSession, ((IZoneSession)target.Session).AccountId, target.CharacterId,
            GmCommandCatalog.OutcomeExecuted, $"TargetName={target.Name}", cancellationToken);

        ((IZoneSession)target.Session).Abort(DisconnectReason.GmKicked);

        SendAck(zoneSession, KickSort, data, SuccessResult);
    }

    public async ValueTask HandleTribeBankAsync(byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, TribeBankSort, cancellationToken))
            return;

        await AuditAsync(TribeBankSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected, null,
            cancellationToken);

        SendAck(zoneSession, TribeBankSort, data, FailureResult);
    }

    public async ValueTask HandleLevelSetAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, LevelSort, cancellationToken))
            return;

        if (!GmLevelSetPayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var requested = payload.Level;
        if (requested > LevelCombinedCapacity)
        {
            await AuditAsync(LevelSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected,
                $"RequestedLevel={requested}", cancellationToken);
            SendAck(zoneSession, LevelSort, data, FailureResult);
            return;
        }

        await AuditAsync(LevelSort, zoneSession, null, null, GmCommandCatalog.OutcomeExecuted,
            $"RequestedLevel={requested};FromLevel={state.Level}", cancellationToken);

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
            newExp2 = 0;
        }
        else if (requested <= BaseLevelCap + HighLevelSpan)
        {
            newLevel = BaseLevelCap;
            newLevel2 = (short)(requested - BaseLevelCap);
            newRebirthCount = 0;
            newExperience = maxBaseExperience;
            newExp2 = RebirthProgression.HighLevelExpTable[newLevel2 - 1];
        }
        else
        {
            newLevel = BaseLevelCap;
            newLevel2 = HighLevelSpan;
            newRebirthCount = requested - BaseLevelCap - HighLevelSpan;
            newExperience = maxBaseExperience;
            newExp2 = RebirthProgression.HighLevelExpTable[RebirthProgression.MaxHighLevel - 1];
        }

        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack) ? petStack.ItemId : 0;
        var petContribution =
            PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            newLevel, state.Tribe, state.PreviousTribe, state.Title, state.Halo, newRebirthCount, newLevel2);
        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);

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

    public async ValueTask HandleStatEditAsync(byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, StatEditSort, cancellationToken))
            return;

        await AuditAsync(StatEditSort, zoneSession, null, null, GmCommandCatalog.OutcomeRejected, null,
            cancellationToken);

        SendAck(zoneSession, StatEditSort, data, FailureResult);
    }

    private async ValueTask TeleportToRawPositionAsync(int sort, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!await MeetsTierOrAbortAsync(zoneSession, sort, cancellationToken))
            return;

        if (!GmMoveCoordinatePayload.TryRead(data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        await AuditAsync(sort, zoneSession, null, null, GmCommandCatalog.OutcomeExecuted,
            $"X={payload.Location[0]};Y={payload.Location[1]};Z={payload.Location[2]}", cancellationToken);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId,
                    TeleportTo: (payload.Location[0], payload.Location[1], payload.Location[2])),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped teleport-to-raw-position mirror for character {CharacterId} (sort {Sort})",
                zone.MapId, state.CharacterId, sort);

        if (sort == MoveToPositionSort)
        {
            var gmData = new byte[GmDataSize];
            payload.Write(gmData);
            zoneSession.Send(new GmCommandResponse { Sort = CallMoveGmDataTag, GmData = gmData });
        }

        SendAck(zoneSession, sort, data, SuccessResult);
    }

    private async ValueTask HandleZone124PartyPullAsync(byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, PlayerRuntimeState target, Zone targetZone,
        CancellationToken cancellationToken)
    {
        var targetPartyName = PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, target.CharacterId,
            target.Name, memberId => targetZone.TryGetPlayer(memberId, out var member) ? member?.Name : null);

        var pulled = await zone.PostGmZone124PartyPullCommandAndWaitAsync(
            new GmZone124PartyPullZoneCommand(target.CharacterId, targetPartyName, state.PosX, state.PosY,
                state.PosZ), cancellationToken);

        foreach (var member in pulled)
            await AuditAsync(CallSort, zoneSession, ((IZoneSession)member.Session).AccountId, member.CharacterId,
                GmCommandCatalog.OutcomeExecuted, $"TargetName={member.Name};PartyName={targetPartyName}",
                cancellationToken);

        SendAck(zoneSession, CallSort, data, SuccessResult);
    }

    private async ValueTask<bool> MeetsTierOrAbortAsync(IZoneSession zoneSession, int sort,
        CancellationToken cancellationToken)
    {
        var descriptor = GmCommandCatalog.Resolve(sort);
        if (zoneSession.MeetsGmTier(descriptor.RequiredTier))
            return true;

        await AuditAsync(sort, zoneSession, null, null, GmCommandCatalog.OutcomeDenied,
            $"RequiredTier={(short)descriptor.RequiredTier}", cancellationToken);

        logger.LogWarning(
            "Character {CharacterId} attempted GM command {Command} (sort {Sort}, required tier {RequiredTier}) without sufficient privilege -- disconnecting, no reply",
            zoneSession.CharacterId, descriptor.Name, sort, descriptor.RequiredTier);
        zoneSession.Abort(DisconnectReason.Faulted);
        return false;
    }

    private ValueTask AuditAsync(int sort, IZoneSession zoneSession, int? targetAccountId, int? targetCharacterId,
        byte outcome, string? detail, CancellationToken cancellationToken)
    {
        var descriptor = GmCommandCatalog.Resolve(sort);
        var payload = detail is null
            ? $"Command={descriptor.Name};Sort={sort}"
            : $"Command={descriptor.Name};Sort={sort};{detail}";

        return eventLog.LogAsync(descriptor.AuditEventCode, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, targetAccountId, targetCharacterId, null, null, null, null, null, outcome,
            payload, cancellationToken);
    }

    private static void SendAck(IZoneSession zoneSession, int sort, byte[] data, int result)
    {
        zoneSession.Send(new GenericActionResponse { Result = result, Sort = sort, Data = data, RuneValue = 0 });
    }
}
