using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Pets;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Tribes;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_WORK_SEND (opcode 79) -- the generic tribe sub-command channel. Sub-commands 12-15 always
///     abort; unrecognized sorts also abort. Unlike GUILD_WORK, every mutation here is either this
///     character's own progression state (write-behind) or a synchronous money debit.
/// </summary>
/// <remarks>ZC_TRIBE_WORK_RECV always echoes the client's raw tData back verbatim, never server-computed content.</remarks>
public sealed class TribeActionHandler(
    ZoneRegistry zones,
    ITribeRepository tribes,
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<TribeActionHandler> logger) : IAsyncPacketHandler<TribeActionRequest>
{
    private const int TribeWeaponMoneyCost = 100_000_000;
    private const int TowerScrollMoneyCost = 500_000_000;
    private const int HaloEnchantMoneyCost = 1_000_000;
    private const int HaloEnchantCpCost = 100;
    private const int MapScrollCpCost = 1;
    private const int AlertCharmCpCost = 10;
    private const int RebirthCpCost = 10_000;

    /// <summary>
    ///     The real, EU33 rebirth cap. The legacy's own tSort 11 gate constant, <c>MAX_REBIRTH_LIMIT</c>, is
    ///     12 -- but the legacy's own handler body never lets a character actually get that far: once
    ///     aRebirthNum reaches 6 it takes its own separate "already maxed" branch (echoes failure, no
    ///     further increment) every time after, so 6 is the one and only cap real play ever reaches. Folding
    ///     both legacy checks into this single, lower cap reproduces that same real ceiling directly, instead
    ///     of the legacy's two-tier (unreachable 12, actual 6) shape; <c>MyWork::MaxRebirth</c>'s own
    ///     <c>==12</c> drop/broadcast branch (S04_MyWork05.cpp:4851) is this same dead code, confirming 12 is
    ///     a non-EU33 debug artifact rather than a real milestone.
    /// </summary>
    private const int MaxRebirth = 6;

    /// <summary>MAX_LIMIT_HIGH_LEVEL_NUM (DEFINE.h) -- Level2's own cap, the "G12" gate on Max Rebirth.</summary>
    private const int MaxHighLevel = 12;

    /// <summary>
    ///     LEVELSYSTEM::ReturnHighExpValue's <c>mRangeForHigh</c> table (GameSystem_01_Level.cpp:319-330):
    ///     the XP needed to be considered "100%" at Level2 N is HighLevelExpTable[N-1]. Only ever consulted
    ///     at N=<see cref="MaxHighLevel" /> here (Max Rebirth requires Level2 already at its cap), so this
    ///     is a plain lookup, not a data-driven catalog like <see cref="GameData.WorldDataCache.LevelsByLevel" />.
    /// </summary>
    private static readonly int[] HighLevelExpTable =
    [
        962_105_896, 1_000_590_131, 1_040_613_736, 1_082_238_285, 1_125_527_816, 1_170_548_928,
        1_217_370_885, 1_266_065_720, 1_316_708_348, 1_369_376_681, 1_424_151_748, 1_481_117_817
    ];

    // Indexed by current title rank (0-11) before purchase; the 13th entry is dead but kept for table fidelity.
    private static readonly int[] TitleCostCp =
        [800, 1700, 2500, 3400, 4200, 5100, 5900, 6800, 7600, 8500, 9300, 10000, 10000];

    public async ValueTask HandleAsync(TribeActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        switch (packet.Sort)
        {
            case 1:
                await HandleStatResetAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 2:
                await HandleAppointSubMasterAsync(packet, session, zoneSession, zone, state, ct);
                return;
            case 3:
                await HandleRemoveSubMasterAsync(packet, session, zoneSession, zone, state, ct);
                return;
            case 4:
                await HandleTribeWeaponAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 5:
                HandleTribeSkill(packet, session, zoneSession, state);
                return;
            case 6:
                await HandleTitleAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 7:
                await HandleHaloEnchantAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 8:
                await HandleLevelBonusAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 9:
                await HandleOrnamentAsync(packet, session, zoneSession, zone, state, characterId, true, ct);
                return;
            case 10:
                await HandleOrnamentAsync(packet, session, zoneSession, zone, state, characterId, false, ct);
                return;
            case 11:
                await HandleRebirthAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 12:
            case 13:
            case 14:
            case 15:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 16:
                await HandleScrollAsync(packet, session, zoneSession, zone, state, characterId, 591,
                    MapScrollCpCost, ct);
                return;
            case 17:
                await HandleScrollAsync(packet, session, zoneSession, zone, state, characterId, 590,
                    AlertCharmCpCost, ct);
                return;
            case 18:
                await HandleTowerScrollAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    /// <summary>
    ///     tSort 1 -- reset spent base stats back into unspent points. Requires level &lt;=39 and a valid tribe-capital
    ///     zone.
    /// </summary>
    private async ValueTask HandleStatResetAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.Level > 39 || !IsValidTown(state.Tribe, zone.MapId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var refund = state.StatVit + state.StatStr + state.StatInt + state.StatDex - 4;
        var newStatPoints = state.StatPoints + refund;

        var attributes = new CharacterBaseAttributes(1, 1, 1, 1, state.Level, state.Tribe, state.Title, state.Halo,
            state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        SendEcho(session, packet);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            StatVit: 1, StatStr: 1, StatInt: 1, StatDex: 1, StatPoints: newStatPoints,
            Life: 1, Mana: 0, UpdatedStats: updatedStats), ct);
    }

    /// <summary>tSort 2 -- appoint a sub-master, Force Leader only. Target must be in the actor's own zone.</summary>
    private async ValueTask HandleAppointSubMasterAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.TribeRole != 1 || !IsSubMasterCapitalZone(state.Tribe, zone.MapId) ||
            !TribeWorkNamePayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // "Already a sub-master" is resolved by name->id (independent of online state), since most
        // sub-masters won't be online in this zone.
        var targetIdByName = await characters.GetIdByNameAsync(targetName, ct);
        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (targetIdByName is { } knownId && subMasters.Any(s => s.CharacterId == knownId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var freeSlot = -1;
        for (byte slot = 0; slot < 12; slot++)
            if (subMasters.All(s => s.SlotIndex != slot))
            {
                freeSlot = slot;
                break;
            }

        if (freeSlot < 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            SendEcho(session, packet, 1);
            return;
        }

        if (target.Level < 113)
        {
            SendEcho(session, packet, 2);
            return;
        }

        if (target.ContributionPoints < 1000)
        {
            SendEcho(session, packet, 3);
            return;
        }

        if (subMasters.Any(s => s.CharacterId == target.CharacterId))
        {
            SendEcho(session, packet, 4);
            return;
        }

        await tribes.SetSubMasterAsync(state.Tribe, (byte)freeSlot, target.CharacterId, ct);

        SendEcho(session, packet);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(target.CharacterId, TribeRole: 2));
    }

    /// <summary>tSort 3 -- remove a sub-master, Force Leader only. Target need not be online.</summary>
    private async ValueTask HandleRemoveSubMasterAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.TribeRole != 1 || !IsSubMasterCapitalZone(state.Tribe, zone.MapId) ||
            !TribeWorkNamePayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetName = payload.AvatarName.Trim();
        var targetId = await characters.GetIdByNameAsync(targetName, ct);

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (targetId is null || subMasters.All(s => s.CharacterId != targetId.Value))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await tribes.ClearSubMasterAsync(state.Tribe, targetId.Value, ct);

        SendEcho(session, packet);

        if (zones.TryGetPlayerAndZone(targetId.Value, out _, out var targetZone))
            targetZone.PostTribeProgressCommand(new TribeProgressZoneCommand(targetId.Value, TribeRole: 0));
    }

    /// <summary>
    ///     tSort 4 -- tribe weapon, Force Leader/sub-master. Money debited but never notified to the client (matches
    ///     legacy).
    /// </summary>
    private async ValueTask HandleTribeWeaponAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2) || !IsValidTown(state.Tribe, zone.MapId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var itemId = 1075 + state.Tribe;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -TribeWeaponMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} tribe-weapon money debit failed (insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendEcho(session, packet);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);
    }

    /// <summary>
    ///     tSort 5 -- tribe skill call ability, Force Leader only. The full gate also requires a
    ///     world-scope "tribe symbol battle" flag that no scheduled job ever sets (that world event is a
    ///     separate, unimplemented system), so this always aborts today by design, not by omission.
    /// </summary>
    private void HandleTribeSkill(TribeActionRequest packet, IPacketSession session, ZoneClientSession zoneSession,
        PlayerRuntimeState state)
    {
        if (state.TribeRole != 1)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!TribeWorkSkillPayload.TryRead(packet.Data, out var payload) || payload.TribeSkillSort is < 0 or > 4)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        zoneSession.Abort(DisconnectReason.Faulted);
    }

    /// <summary>tSort 6 -- title tier purchase, no role gate at all; any tribe member may buy, CP-gated only.</summary>
    private async ValueTask HandleTitleAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (!TribeWorkTitlePayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var currentRank = state.Title % 100;
        if (currentRank is < 0 or > 11)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var cost = TitleCostCp[currentRank];
        if (state.ContributionPoints < cost)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newTitle = (payload.TitleSort - 1) * 100 + currentRank + 1;

        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, newTitle, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        SendEcho(session, packet);

        // Unconditional full heal to the new max, not a clamp (legacy SetIntegerUp idiom).
        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - cost, Title: newTitle,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats), ct);
    }

    /// <summary>
    ///     tSort 7 -- halo enchant, no role gate at all. Anti-double-click-per-tick is not reproduced (open
    ///     issue -- no per-zone-tick counter is exposed to this handler).
    /// </summary>
    private async ValueTask HandleHaloEnchantAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.ContributionPoints < HaloEnchantCpCost || state.Halo >= 96)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -HaloEnchantMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} halo-enchant money debit failed (insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var (outcome, newHalo, newProtect) =
            TribeHaloEnchantResolver.Resolve(state.Halo, state.ProtectForHalo, SystemRandomSource.Instance);

        var result = outcome switch
        {
            TribeHaloEnchantOutcome.Success => 0,
            TribeHaloEnchantOutcome.Downgraded => 2,
            _ => 1
        };

        SendEcho(session, packet, result);

        if (outcome is TribeHaloEnchantOutcome.Success or TribeHaloEnchantOutcome.Downgraded)
        {
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
                state.Level, state.Tribe, state.Title, newHalo, state.RebirthCount);
            var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
            var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                pet: ComputePetContribution(state, equipmentContainer));

            await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                state.ContributionPoints - HaloEnchantCpCost, Halo: newHalo,
                ProtectForHalo: newProtect, UpdatedStats: updatedStats), ct);
        }
        else
        {
            await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                state.ContributionPoints - HaloEnchantCpCost, ProtectForHalo: newProtect), ct);
        }
    }

    /// <summary>
    ///     tSort 8 -- level-milestone bonus claim. <see cref="PlayerRuntimeState.BonusItemLevel" /> is
    ///     session-scoped and never populated by any batch to date, so this always aborts today, matching
    ///     the legacy's own behavior for the same zero case. Only tiers 45/65/85/105/145 are matched; other
    ///     legacy tiers' level values aren't resolved by any available report and fall to the default abort.
    /// </summary>
    private async ValueTask HandleLevelBonusAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.BonusItemLevel < 1)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        ImmutableArray<TribeGroundItemDrop> drops;
        switch (state.BonusItemLevel)
        {
            case 45:
                drops = [new TribeGroundItemDrop(99700, 1), new TribeGroundItemDrop(539, 1)];
                break;
            case 65:
                drops = [new TribeGroundItemDrop(99701, 1), new TribeGroundItemDrop(539, 1)];
                break;
            case 85:
                drops = [new TribeGroundItemDrop(99702, 1), new TribeGroundItemDrop(539, 1)];
                break;
            case 105:
                drops = [new TribeGroundItemDrop(845, 1), new TribeGroundItemDrop(539, 2)];
                break;
            case 145:
                var tribeItemId = state.PreviousTribe switch
                {
                    0 => 83809,
                    1 => 83857,
                    2 => 83906,
                    _ => 0
                };
                var builder = ImmutableArray.CreateBuilder<TribeGroundItemDrop>(tribeItemId == 0 ? 3 : 4);
                builder.Add(new TribeGroundItemDrop(851, 1));
                builder.Add(new TribeGroundItemDrop(1022, 10));
                builder.Add(new TribeGroundItemDrop(1023, 10));
                builder.Add(new TribeGroundItemDrop(1019, 10));
                if (tribeItemId != 0)
                    builder.Add(new TribeGroundItemDrop(tribeItemId, 20));
                drops = builder.ToImmutable();
                break;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }

        SendEcho(session, packet);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            BonusItemLevel: 0, BonusItemValue: false, DropItems: drops), ct);
    }

    /// <summary>tSort 9/10 -- ornament on/off, no gate at all.</summary>
    private async ValueTask HandleOrnamentAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, bool on,
        CancellationToken ct)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.Title, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        SendEcho(session, packet);

        // tSort 10 (OFF) additionally full-heals to the new max; tSort 9 (ON) does not touch Life/Mana.
        var command = on
            ? new TribeProgressZoneCommand(characterId, UseOrnament: true, UpdatedStats: updatedStats)
            : new TribeProgressZoneCommand(characterId, UseOrnament: false, Life: updatedStats.MaxLife,
                Mana: updatedStats.MaxMana, UpdatedStats: updatedStats);

        await zone.PostTribeProgressCommandAndWaitAsync(command, ct);
    }

    /// <summary>
    ///     tSort 11 -- Max Rebirth (S04_MyWork02.cpp:11343-11375, <c>__REBIRTH__</c>). Requires Level1 AND
    ///     Level2 both already at their own caps, Exp2 at 100% of Level2's threshold, a 10,000 CP toll, and
    ///     <see cref="MaxRebirth" /> not yet reached; on success resets Exp2, increments RebirthCount, debits
    ///     the CP, fully heals, and recomputes stats (RebirthCount feeds StatCalculator's critical-defence and
    ///     critical-wrapper bonuses).
    /// </summary>
    /// <remarks>
    ///     The legacy's own success branch also does <c>aZone241Time += 10</c> -- Fenrir has no field for that
    ///     counter yet (same "not modeled" posture as this file's other untracked wire-only counters), so it
    ///     is not reproduced. See <see cref="MaxRebirth" />'s own remarks for why 6, not the legacy's own
    ///     <c>MAX_REBIRTH_LIMIT</c>=12, is the cap enforced here.
    /// </remarks>
    private async ValueTask HandleRebirthAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.RebirthCount >= MaxRebirth ||
            state.Level != LevelProgressionCalculator.MaxLevel ||
            state.Level2 != MaxHighLevel ||
            state.Exp2 < HighLevelExpTable[state.Level2 - 1] ||
            state.ContributionPoints < RebirthCpCost)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newRebirthCount = state.RebirthCount + 1;
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.Title, state.Halo, newRebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        SendEcho(session, packet);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - RebirthCpCost, RebirthCount: newRebirthCount, Exp2: 0,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats,
            RebirthBroadcast: true), ct);
    }

    /// <summary>
    ///     tSort 16 (map/clan scroll, item 591, 1 CP) / tSort 17 (alert charm, item 590, 10 CP) -- Force
    ///     Leader/sub-master.
    /// </summary>
    private async ValueTask HandleScrollAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, int itemId, int cpCost,
        CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2) || state.ContributionPoints < cpCost)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendEcho(session, packet);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - cpCost,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);
    }

    /// <summary>
    ///     tSort 18 -- tower construction scroll, Force Leader/sub-master. Money debited but never notified (same as
    ///     tSort 4).
    /// </summary>
    private async ValueTask HandleTowerScrollAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -TowerScrollMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} tower-scroll money debit failed (insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendEcho(session, packet);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            DropItems: [new TribeGroundItemDrop(665, 1)]), ct);
    }

    /// <summary>Tribe 0-2 map to zones 1/6/11, tribe 3 to zone 140.</summary>
    private static bool IsValidTown(byte tribe, short mapId)
    {
        return tribe switch
        {
            0 => mapId == 1,
            1 => mapId == 6,
            2 => mapId == 11,
            3 => mapId == 140,
            _ => false
        };
    }

    /// <summary>
    ///     tSort 2/3's own zone-number gate: 71+tribe for tribes 0-2, 140 for tribe 3 -- a different mapping
    ///     than <see cref="IsValidTown" /> (used by tSort 1/4). A genuine, verified legacy inconsistency,
    ///     reproduced exactly as found.
    /// </summary>
    private static bool IsSubMasterCapitalZone(byte tribe, short mapId)
    {
        return tribe switch
        {
            0 or 1 or 2 => mapId == 71 + tribe,
            3 => mapId == 140,
            _ => false
        };
    }

    private static void SendEcho(IPacketSession session, TribeActionRequest packet, int result = 0)
    {
        session.Send(new TribeActionResponse { Result = result, Sort = packet.Sort, Data = packet.Data });
    }

    private PetStatContribution ComputePetContribution(PlayerRuntimeState state,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        return PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
    }
}
