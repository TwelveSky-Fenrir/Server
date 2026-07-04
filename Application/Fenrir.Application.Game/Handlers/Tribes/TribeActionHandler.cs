using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Tribes;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_WORK_SEND (opcode 79) -- the generic tribe sub-command channel (doc 10 §2, verified in full
///     against <c>Server/ts25zone/S04_MyWork02.cpp:10800-11484</c>). All 14 alive EU33 tSorts are
///     dispatched: 1 stat reset, 2/3 sub-master appoint/remove, 4 tribe weapon, 5 tribe skill, 6 title tier
///     (USE_TITLE), 7 halo enchant (USE_HALO), 8 level-milestone bonus, 9/10 ornament on/off, 11 rebirth
///     (__REBIRTH__), 16 map/clan scroll, 17 alert charm, 18 tower scroll (USE_TOWER). Dead sub-commands
///     12/13/14/15 all abort (<c>Quit()</c> unconditional in the source); anything else falls to the
///     legacy's own <c>default:</c>, which also aborts. Unlike GUILD_WORK, TRIBE_WORK never leaves the zone
///     process at all in the legacy (no ts25extra RPC) -- every mutation here is either this character's
///     OWN progression state (write-behind, same D7(a) posture as Title/Halo/RebirthCount already have) or
///     a synchronous money debit (D7(b), <see cref="CharacterRepository.AdjustMoneyAsync" />).
/// </summary>
/// <remarks>
///     ZC_TRIBE_WORK_RECV always echoes the client's raw 100-byte <c>tData</c> back verbatim (doc 10 §3) --
///     never server-computed content -- so every response here carries <c>packet.Data</c> unchanged.
/// </remarks>
public sealed class TribeActionHandler(
    ZoneRegistry zones,
    TribeRepository tribes,
    CharacterRepository characters,
    WorldDataCache worldData,
    ILogger<TribeActionHandler> logger) : IAsyncPacketHandler<TribeActionRequest>
{
    private const int TribeWeaponMoneyCost = 100_000_000; // mTribeWeaponMoneyCost, LNW33 (DEFINE.h:238)
    private const int TowerScrollMoneyCost = 500_000_000; // USE_TOWER, S04_MyWork02.cpp:11463
    private const int HaloEnchantMoneyCost = 1_000_000;
    private const int HaloEnchantCpCost = 100;
    private const int MapScrollCpCost = 1;
    private const int AlertCharmCpCost = 10;

    public async ValueTask HandleAsync(TribeActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        // Same blanket posture as GenericActionHandler/GuildActionHandler: every tSort here shares the same
        // per-character economy-adjacent state (CP, money, stats) -- see EconomyActionLock's own remarks.
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
                await HandleOrnamentAsync(packet, session, zoneSession, zone, state, characterId, on: true, ct);
                return;
            case 10:
                await HandleOrnamentAsync(packet, session, zoneSession, zone, state, characterId, on: false, ct);
                return;
            case 11:
                HandleRebirth(zoneSession);
                return;
            case 12:
            case 13:
            case 14:
            case 15:
                // register/enter ultimate war, "guild?", quest ui -- all unconditional Quit() in the source.
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 16:
                await HandleScrollAsync(packet, session, zoneSession, zone, state, characterId, itemId: 591,
                    cpCost: MapScrollCpCost, ct);
                return;
            case 17:
                await HandleScrollAsync(packet, session, zoneSession, zone, state, characterId, itemId: 590,
                    cpCost: AlertCharmCpCost, ct);
                return;
            case 18:
                await HandleTowerScrollAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    /// <summary>tSort 1 -- reset spent base stats back into unspent points (S04_MyWork02.cpp:10835-10858). Level &lt;=39 and a valid tribe-capital zone, else <c>Quit()</c>.</summary>
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

    /// <summary>
    ///     tSort 2 -- appoint a sub-master, Force Leader only (S04_MyWork02.cpp:10859-10953). Target resolved
    ///     WITHIN THE ACTOR'S OWN ZONE ONLY (<c>SearchAvatar</c> scope). Zone-number gate uses the legacy's
    ///     OWN (documented, inconsistent-with-<see cref="IsValidTown" />) 71/72/73/140 mapping -- doc 10
    ///     quirk 10.
    /// </summary>
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

        // Legacy checks "already a sub-master" by NAME against the raw client string, before ever resolving
        // an online target -- resolved here via name->id (independent of online state) rather than the
        // fragile "is this OTHER sub-master currently online in THIS zone" lookup a same-zone-only search
        // would require (most sub-masters are not the one sending this packet).
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
            SendEcho(session, packet, result: 1);
            return;
        }

        if (target.Level < 113)
        {
            SendEcho(session, packet, result: 2);
            return;
        }

        if (target.ContributionPoints < 1000)
        {
            SendEcho(session, packet, result: 3);
            return;
        }

        if (subMasters.Any(s => s.CharacterId == target.CharacterId))
        {
            SendEcho(session, packet, result: 4);
            return;
        }

        await tribes.SetSubMasterAsync(state.Tribe, (byte)freeSlot, target.CharacterId, ct);

        SendEcho(session, packet);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(target.CharacterId, TribeRole: 2));
    }

    /// <summary>tSort 3 -- remove a sub-master, Force Leader only (S04_MyWork02.cpp:10955-10997). Target need not be online.</summary>
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

    /// <summary>tSort 4 -- tribe weapon, Force Leader/sub-master (S04_MyWork02.cpp:10999-11053). Money debited but NEVER notified to the client (doc 10 quirk 8) -- matches the legacy's own missing B_AVATAR_CHANGE_INFO_2 call.</summary>
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
    ///     tSort 5 -- tribe skill call ability, Force Leader only (S04_MyWork02.cpp:11054-11093).
    /// </summary>
    /// <remarks>
    ///     DEFERRED (documented, not fabricated): the full gate also requires every tribe's world point total
    ///     &gt;100, the requester's tribe being the current minimum (<c>ReturnSmallTribe</c>), that tribe's
    ///     share &lt;20% of the realm total, AND <c>mWorldInfo-&gt;mTribeSymbolBattle==1</c> -- a WORLD-scope
    ///     flag (game.WorldState.TribeSymbolBattle) that no scheduled job in Fenrir ever sets to 1 (the
    ///     "tribe symbol battle" world event that would flip it is an entirely separate, unimplemented
    ///     system). Since that flag is always false today, the ENTIRE gate is unconditionally unreachable
    ///     regardless of the point-share sub-checks -- implementing those now would be untestable dead
    ///     weight; only the always-decisive role check plus an honest abort are implemented. A future batch
    ///     that adds the tribe-symbol-battle world event should complete this gate's point-share logic too.
    /// </remarks>
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

    /// <summary>tSort 6 -- title tier purchase, USE_TITLE, no role gate at all (S04_MyWork02.cpp:11095-11126, verified -- any tribe member may buy, CP-gated only).</summary>
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

        var newTitle = (payload.TitleSort - 1) * 100 + (currentRank + 1);

        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, newTitle, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        SendEcho(session, packet);

        // SetIntegerUp(aLifeValue, aMaxLifeValue, aMaxLifeValue) / SetIntegerUp(aManaValue, ...) -- both
        // target args are the SAME value, so this is an unconditional full heal to the new max, not a clamp.
        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            ContributionPoints: state.ContributionPoints - cost, Title: newTitle,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats), ct);
    }

    /// <summary>tSort 7 -- halo enchant, USE_HALO, no role gate at all (S04_MyWork02.cpp:11128-11231, verified). Anti-double-click-per-tick is NOT reproduced (open issue -- no per-zone-tick counter is exposed to this handler).</summary>
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
                ContributionPoints: state.ContributionPoints - HaloEnchantCpCost, Halo: newHalo,
                ProtectForHalo: newProtect, UpdatedStats: updatedStats), ct);
        }
        else
        {
            await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                ContributionPoints: state.ContributionPoints - HaloEnchantCpCost, ProtectForHalo: newProtect), ct);
        }
    }

    /// <summary>
    ///     tSort 8 -- level-milestone bonus claim (S04_MyWork02.cpp:11233-11326). <c>aBonusItemLevel</c> is
    ///     session-scoped only and never populated by any batch to date (see
    ///     <see cref="PlayerRuntimeState.BonusItemLevel" />'s own remarks) -- this dispatch is fully
    ///     implemented and correctly aborts for the (currently universal) zero case, matching the legacy's
    ///     own <c>Quit()</c> exactly.
    /// </summary>
    /// <remarks>
    ///     DEFERRED: only tiers 45/65/85/105 and LV_M33 (=145, contracts/06_guild_tribe.md's own resolved
    ///     constant) are matched -- LV_M2/M8/M14/M20/M26/M32's exact numeric level values are not resolved
    ///     by any report available to this pass (not guessed, per this task's own instructions); an
    ///     unmatched value correctly falls to the same default-abort branch the source itself uses for any
    ///     value it does not recognize either, so this is under-coverage, not a new incorrect acceptance.
    /// </remarks>
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
            case 145: // LV_M33
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

    /// <summary>tSort 9/10 -- ornament on/off, no gate at all (S04_MyWork02.cpp:11327-11341).</summary>
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

        // tSort 10 (OFF) additionally full-heals to the new max (SetIntegerUp, same idiom as tSort 6's own
        // title purchase); tSort 9 (ON) does not touch Life/Mana at all in the source.
        var command = on
            ? new TribeProgressZoneCommand(characterId, UseOrnament: true, UpdatedStats: updatedStats)
            : new TribeProgressZoneCommand(characterId, UseOrnament: false, Life: updatedStats.MaxLife,
                Mana: updatedStats.MaxMana, UpdatedStats: updatedStats);

        await zone.PostTribeProgressCommandAndWaitAsync(command, ct);
    }

    /// <summary>
    ///     tSort 11 -- Max Rebirth (S04_MyWork02.cpp:11342-11389, __REBIRTH__).
    /// </summary>
    /// <remarks>
    ///     DEFERRED (documented, not fabricated): the real gate is <c>aRebirthNum &lt; MAX_REBIRTH_LIMIT(12)
    ///     &amp;&amp; (aLevel1+aLevel2)==(MAX_LIMIT_LEVEL_NUM+MAX_LIMIT_HIGH_LEVEL_NUM) &amp;&amp; aExp2&gt;=
    ///     ReturnHighExpValue(aLevel2) &amp;&amp; aKillOtherTribe&gt;=10000</c>. Level2/Exp2 (the "high
    ///     level"/rebirth martial-level progression track) are not modeled anywhere in Fenrir to date --
    ///     Experience is stored as ONE merged BIGINT (game.Characters_progression.sql's own header), and
    ///     Exp1/Exp2 are not even split back out onto the AVATAR_INFO wire template yet
    ///     (<c>AvatarInfoFactory</c> leaves both at 0). Implementing the Level1+Level2==157 gate would
    ///     therefore require fabricating a Level2 progression system this domain does not own (that is a
    ///     Combat/leveling concern). This dispatch is wired (so an unknown-to-the-client sort doesn't fall
    ///     through incorrectly) but always aborts, exactly matching what the legacy would ACTUALLY do for
    ///     every real Fenrir character today (no character can satisfy the Level2 gate, so the real source
    ///     would also <c>Quit()</c> here for every current player, just via a different specific check).
    /// </remarks>
    private void HandleRebirth(ZoneClientSession zoneSession)
    {
        zoneSession.Abort(DisconnectReason.Faulted);
    }

    /// <summary>tSort 16 (map/clan scroll, item 591, 1 CP) / tSort 17 (alert charm, item 590, 10 CP) -- Force Leader/sub-master (S04_MyWork02.cpp:11403-11452).</summary>
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
            ContributionPoints: state.ContributionPoints - cpCost,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);
    }

    /// <summary>tSort 18 -- tower construction scroll, USE_TOWER, Force Leader/sub-master (S04_MyWork02.cpp:11453-11478). Money debited but NEVER notified (same doc 10 quirk 8 as tSort 4).</summary>
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

    /// <summary>
    ///     <c>mTitleCostCP</c> (S07_MyGame03.cpp:25, doc 10 §2 tSort 6) -- indexed by the CURRENT rank
    ///     (0-11) before the purchase; the 13th (index 12) legacy entry is dead (the source's own bound
    ///     check never allows <c>tCurrentTitle</c> to reach 12) but kept here for exact table fidelity.
    /// </summary>
    private static readonly int[] TitleCostCp =
        [800, 1700, 2500, 3400, 4200, 5100, 5900, 6800, 7600, 8500, 9300, 10000, 10000];

    /// <summary><c>IsValidTown</c> (mapcheck.h:83, verified via report 10 §0): tribe 0-2 map to zones 1/6/11, tribe 3 to zone 140.</summary>
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
    ///     TRIBE_WORK tSort 2/3's OWN zone-number gate (S04_MyWork02.cpp:10873-10891) -- 71+tribe for
    ///     tribes 0-2, 140 for tribe 3. Doc 10 quirk 10: this is a DIFFERENT mapping than
    ///     <see cref="IsValidTown" /> (which tSort 1/4 use) -- a genuine, verified legacy inconsistency, not
    ///     a transcription error; both are reproduced exactly as found.
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

    /// <summary>
    ///     Server Logic V9 Progression's pet stat contribution (same pattern as <c>GenericActionHandler</c>'s
    ///     own equipment-move recompute, verified against its exact call site) -- none of this handler's own
    ///     actions touch equipment, so the CURRENT container/growth/activity are always the right inputs
    ///     (unlike GenericActionHandler's own projected-container nuance for an in-flight equip/unequip).
    /// </summary>
    private Stats.PetStatContribution ComputePetContribution(PlayerRuntimeState state,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var petItemId = equipmentContainer.TryGetValue(Pets.PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        return Pets.PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
    }
}
