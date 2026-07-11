using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>See <see cref="ITribeActionService" />; one method per CZ_TRIBE_WORK_SEND sub-command (tSort).</summary>
public sealed class TribeActionService(
    ZoneRegistry zones,
    ITribeRepository tribes,
    ICharacterRepository characters,
    WorldDataCache worldData,
    WorldStateService worldState,
    ILogger<TribeActionService> logger) : ITribeActionService
{
    private const int TribeWeaponMoneyCost = 100_000_000;
    private const int TowerScrollMoneyCost = 500_000_000;
    private const int HaloEnchantMoneyCost = 1_000_000;
    private const int HaloEnchantCpCost = 100;
    private const int MapScrollCpCost = 1;
    private const int AlertCharmCpCost = 10;
    private const int RebirthCpCost = 10_000;

    /// <summary>
    ///     Path B ("Max Rebirth")'s own path-specific cap -- confirmed by the Rebirth-advancement behavior
    ///     contract as a genuine, distinct rule from <see cref="RebirthProgression.MaxRebirthGeneration" />
    ///     (the absolute 12 cap): once <c>aRebirthNum</c> reaches 6, a further Path B request does not
    ///     disconnect (unlike every other Path B precondition failure) -- it is answered with a graceful
    ///     in-band failure instead, and neither the counter nor the CP cost is touched. Generations 7-12 are
    ///     reachable only via Path A, the Rebirth-Pill item-consumption path
    ///     (<c>UseInventoryItemService</c>'s own Rebirth-Pill branch), which checks against the true absolute
    ///     cap instead of this one.
    /// </summary>
    private const int MaxRebirth = 6;

    /// <summary>
    ///     tSort 1 -- reset spent base stats back into unspent points. Requires level &lt;=39 and a valid tribe-capital
    ///     zone.
    /// </summary>
    public async ValueTask<TribeActionOutcome> ResetStatsAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.Level > 39 || !IsValidTown(state.Tribe, zone.MapId))
            return TribeActionOutcome.Abort;

        var refund = state.StatVit + state.StatStr + state.StatInt + state.StatDex - 4;
        var newStatPoints = state.StatPoints + refund;

        var attributes = new CharacterBaseAttributes(1, 1, 1, 1, state.Level, state.Tribe, state.PreviousTribe,
            state.Title, state.Halo, state.RebirthCount, state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            StatVit: 1, StatStr: 1, StatInt: 1, StatDex: 1, StatPoints: newStatPoints,
            Life: 1, Mana: 0, UpdatedStats: updatedStats), ct);

        logger.LogInformation("Character {CharacterId} reset base stats, refunding {Refund} points", characterId,
            refund);

        return TribeActionOutcome.Ok();
    }

    /// <summary>tSort 2 -- appoint a sub-master, Force Leader only. Target must be in the actor's own zone.</summary>
    public async ValueTask<TribeActionOutcome> AppointSubMasterAsync(Zone zone, PlayerRuntimeState state, byte[] data,
        CancellationToken ct)
    {
        if (state.TribeRole != 1 || !IsSubMasterCapitalZone(state.Tribe, zone.MapId) ||
            !TribeWorkNamePayload.TryRead(data, out var payload))
            return TribeActionOutcome.Abort;

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
            return TribeActionOutcome.Abort;

        // "Already a sub-master" is resolved by name->id (independent of online state), since most
        // sub-masters won't be online in this zone.
        var targetIdByName = await characters.GetIdByNameAsync(targetName, ct);
        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (targetIdByName is { } knownId && subMasters.Any(s => s.CharacterId == knownId))
            return TribeActionOutcome.Abort;

        var freeSlot = -1;
        for (byte slot = 0; slot < 12; slot++)
            if (subMasters.All(s => s.SlotIndex != slot))
            {
                freeSlot = slot;
                break;
            }

        if (freeSlot < 0)
            return TribeActionOutcome.Abort;

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            logger.LogDebug("Tribe {Tribe} sub-master appointment rejected: target {TargetName} not online in zone",
                state.Tribe, targetName);
            return TribeActionOutcome.Ok(1);
        }

        if (target.Level < 113)
        {
            logger.LogDebug(
                "Tribe {Tribe} sub-master appointment of {TargetCharacterId} rejected: level {Level} below 113",
                state.Tribe, target.CharacterId, target.Level);
            return TribeActionOutcome.Ok(2);
        }

        if (target.ContributionPoints < 1000)
        {
            logger.LogDebug(
                "Tribe {Tribe} sub-master appointment of {TargetCharacterId} rejected: contribution points below 1000",
                state.Tribe, target.CharacterId);
            return TribeActionOutcome.Ok(3);
        }

        if (subMasters.Any(s => s.CharacterId == target.CharacterId))
        {
            logger.LogDebug(
                "Tribe {Tribe} sub-master appointment of {TargetCharacterId} rejected: already a sub-master",
                state.Tribe, target.CharacterId);
            return TribeActionOutcome.Ok(4);
        }

        await tribes.SetSubMasterAsync(state.Tribe, (byte)freeSlot, target.CharacterId, ct);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(target.CharacterId, TribeRole: 2));

        logger.LogInformation("Tribe {Tribe} appointed character {TargetCharacterId} as sub-master (slot {Slot})",
            state.Tribe, target.CharacterId, freeSlot);

        return TribeActionOutcome.Ok();
    }

    /// <summary>tSort 3 -- remove a sub-master, Force Leader only. Target need not be online.</summary>
    public async ValueTask<TribeActionOutcome> RemoveSubMasterAsync(Zone zone, PlayerRuntimeState state, byte[] data,
        CancellationToken ct)
    {
        if (state.TribeRole != 1 || !IsSubMasterCapitalZone(state.Tribe, zone.MapId) ||
            !TribeWorkNamePayload.TryRead(data, out var payload))
            return TribeActionOutcome.Abort;

        var targetName = payload.AvatarName.Trim();
        var targetId = await characters.GetIdByNameAsync(targetName, ct);

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (targetId is null || subMasters.All(s => s.CharacterId != targetId.Value))
            return TribeActionOutcome.Abort;

        await tribes.ClearSubMasterAsync(state.Tribe, targetId.Value, ct);

        if (zones.TryGetPlayerAndZone(targetId.Value, out _, out var targetZone))
            targetZone.PostTribeProgressCommand(new TribeProgressZoneCommand(targetId.Value, TribeRole: 0));

        logger.LogInformation("Tribe {Tribe} removed character {TargetCharacterId} as sub-master", state.Tribe,
            targetId.Value);

        return TribeActionOutcome.Ok();
    }

    /// <summary>
    ///     tSort 4 -- tribe weapon, Force Leader/sub-master. Money debited but never notified to the client (matches
    ///     legacy).
    /// </summary>
    public async ValueTask<TribeActionOutcome> UseTribeWeaponAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2) || !IsValidTown(state.Tribe, zone.MapId))
            return TribeActionOutcome.Abort;

        var itemId = 1075 + state.Tribe;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -TribeWeaponMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            // Economy gate (insufficient funds) -- Warning, not Information.
            logger.LogWarning(ex, "Character {CharacterId} tribe-weapon money debit failed (insufficient funds)",
                characterId);
            return TribeActionOutcome.Abort;
        }

        logger.LogInformation("Character {CharacterId} purchased tribe weapon (item {ItemId}) for tribe {Tribe}",
            characterId, itemId, state.Tribe);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);

        return TribeActionOutcome.Ok();
    }

    /// <summary>
    ///     tSort 5 -- tribe Formation-ability declaration (B10 branch A), Force Leader only. Declares this
    ///     character's own tribe's world-scope Formation ability code via
    ///     <see cref="WorldStateService.SetTribeFormationAbility" /> -- see
    ///     <see cref="FormationCombatResolver" /> for what a live code does to enemy-class combat math.
    /// </summary>
    /// <remarks>
    ///     All five eligibility gates <see cref="WorldStateService.SetTribeFormationAbility" />'s own remarks
    ///     name are now enforced here, in order: (1) Force Leader role (<c>state.TribeRole == 1</c>) and
    ///     payload shape/range (<see cref="TribeWorkSkillPayload.TribeSkillSort" />, 0-4) below; then gates
    ///     (a)-(c) via <see cref="TribeFormationAbilityEligibility" /> -- all four tribes' points above the
    ///     floor (<see cref="TribeFormationAbilityEligibility.AllTribesAboveFloor" />), the requester's tribe
    ///     being the strict single lowest by index on a tie
    ///     (<see cref="TribeFormationAbilityEligibility.FindLowestPointTribe" />), and that tribe's share of
    ///     the combined total under twenty percent (<see cref="TribeFormationAbilityEligibility.IsUnderShareThreshold" />,
    ///     evaluated only once gate (a) has already passed, so the combined-points divisor can never be zero);
    ///     then gate (d), a direct read of <see cref="WorldStateService.World" />'s
    ///     <see cref="WorldRvrState.TribeSymbolBattle" /> flag -- Tribe Symbol Battle must currently be active.
    ///     Every gate failure is answered identically to the pre-existing Force-Leader-role/payload-range
    ///     gates: <see cref="TribeActionOutcome.Abort" />, no further state touched.
    /// </remarks>
    public TribeActionOutcome ValidateTribeSkill(PlayerRuntimeState state, byte[] data)
    {
        if (state.TribeRole != 1)
            return TribeActionOutcome.Abort;

        if (!TribeWorkSkillPayload.TryRead(data, out var payload) || payload.TribeSkillSort is < 0 or > 4)
            return TribeActionOutcome.Abort;

        var tribes = worldState.GetAllTribes();

        // Gate (a): four-tribe RvR point floor -- a global floor across all four tribes, not scoped to the
        // requester's own tribe.
        if (!TribeFormationAbilityEligibility.AllTribesAboveFloor(tribes))
            return TribeActionOutcome.Abort;

        // Gate (b): the requester's own tribe must be the single strict-lowest-point tribe.
        var lowestPointTribe = TribeFormationAbilityEligibility.FindLowestPointTribe(tribes);
        if (state.Tribe != lowestPointTribe)
            return TribeActionOutcome.Abort;

        // Gate (c): that tribe's share of the four-tribe combined total must be under twenty percent. Safe to
        // evaluate now -- gate (a) already guarantees the combined total (the divisor) can never be zero.
        var combinedPoints = TribeFormationAbilityEligibility.CombinedPoints(tribes);
        if (!TribeFormationAbilityEligibility.IsUnderShareThreshold(tribes[state.Tribe].Points, combinedPoints))
            return TribeActionOutcome.Abort;

        // Gate (d): the Tribe Symbol Battle world event must currently be active.
        if (!worldState.World.TribeSymbolBattle)
            return TribeActionOutcome.Abort;

        var formationCode = (byte)payload.TribeSkillSort;
        worldState.SetTribeFormationAbility(state.Tribe, formationCode);

        logger.LogInformation(
            "Character {CharacterId} (tribe {Tribe}) declared Formation ability code {FormationCode}",
            state.CharacterId, state.Tribe, formationCode);

        return TribeActionOutcome.Ok();
    }

    /// <summary>tSort 6 -- title tier purchase, no role gate at all; any tribe member may buy, CP-gated only.</summary>
    public async ValueTask<TribeActionOutcome> PurchaseTitleAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte[] data, CancellationToken ct)
    {
        if (!TribeWorkTitlePayload.TryRead(data, out var payload))
            return TribeActionOutcome.Abort;

        var currentRank = state.Title % 100;
        if (currentRank is < 0 or > 11)
            return TribeActionOutcome.Abort;

        var cost = TitleContributionCost.PurchaseStepCost(currentRank);
        if (state.ContributionPoints < cost)
            return TribeActionOutcome.Abort;

        var newTitle = (payload.TitleSort - 1) * 100 + currentRank + 1;

        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, newTitle, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        // Unconditional full heal to the new max, not a clamp (legacy SetIntegerUp idiom).
        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - cost, Title: newTitle,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats), ct);

        logger.LogInformation(
            "Character {CharacterId} purchased title tier, new title {NewTitle} (spent {Cost} CP)", characterId,
            newTitle, cost);

        return TribeActionOutcome.Ok();
    }

    /// <summary>
    ///     tSort 7 -- halo enchant, no role gate at all.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11128-11136 (<c>mTickCountCPRFC == mGAME.mTickCount</c>
    ///     same-tick reentry guard, checked first, and its <c>Quit()</c>) ; :11137-11150 (the CP/money/level
    ///     checks this guard precedes -- <see cref="PlayerRuntimeState.LastHaloEnchantAttemptUtc" /> is stamped
    ///     unconditionally the instant the guard passes, before any of those checks run, so a same-tick retry
    ///     is rejected even if this first attempt later fails one of them) ; Server/ts25zone/H07_MyGame.h:836
    ///     (field declaration, "#Protect CP RFC Time").
    /// </remarks>
    public async ValueTask<TribeActionOutcome> HaloEnchantAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        // Same-tick reentry guard (mTickCountCPRFC) -- see this method's own <remarks>. A hostile-client
        // signal (a real client can never submit twice within the same server tick), so it disconnects
        // outright via TribeActionOutcome.Abort rather than a soft in-band failure -- the same posture the
        // CP-insufficiency check immediately below also uses.
        var now = DateTime.UtcNow;
        if (now - state.LastHaloEnchantAttemptUtc < SimulationClock.LegacyTick)
        {
            logger.LogWarning(
                "Character {CharacterId} halo-enchant rejected: same-tick repeat request", characterId);
            return TribeActionOutcome.Abort;
        }

        state.LastHaloEnchantAttemptUtc = now;

        if (state.ContributionPoints < HaloEnchantCpCost || state.Halo >= 96)
            return TribeActionOutcome.Abort;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -HaloEnchantMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            // Economy gate (insufficient funds) -- Warning, not Information.
            logger.LogWarning(ex, "Character {CharacterId} halo-enchant money debit failed (insufficient funds)",
                characterId);
            return TribeActionOutcome.Abort;
        }

        var (outcome, newHalo, newProtect) =
            TribeHaloEnchantResolver.Resolve(state.Halo, state.ProtectForHalo, SystemRandomSource.Instance);

        var result = outcome switch
        {
            TribeHaloEnchantOutcome.Success => 0,
            TribeHaloEnchantOutcome.Downgraded => 2,
            _ => 1
        };

        if (outcome is TribeHaloEnchantOutcome.Success or TribeHaloEnchantOutcome.Downgraded)
        {
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
                state.Level, state.Tribe, state.PreviousTribe, state.Title, newHalo, state.RebirthCount,
                state.Level2);
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

        logger.LogInformation("Character {CharacterId} halo-enchant {Outcome}: halo {OldHalo} -> {NewHalo}",
            characterId, outcome, state.Halo, newHalo);

        return TribeActionOutcome.Ok(result);
    }

    /// <summary>
    ///     tSort 8 -- level-milestone bonus claim. <see cref="PlayerRuntimeState.BonusItemLevel" /> is armed by
    ///     <c>Zone.ApplyCharacterExperienceGain</c>'s own level-milestone step (<see cref="LevelMilestoneBonus" />)
    ///     the moment a level-up crosses one of <see cref="LevelMilestoneBonus.ArmableMilestoneLevels" />;
    ///     before that field is ever populated this aborts, matching the legacy's own behavior for the same
    ///     zero case. Session-scoped only -- an armed-but-unclaimed milestone does not survive logout/zone
    ///     transfer today (see <see cref="PlayerRuntimeState.BonusItemLevel" />'s own remarks).
    /// </summary>
    public async ValueTask<TribeActionOutcome> ClaimLevelBonusAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (!LevelMilestoneBonus.TryResolveClaimDrops(state.BonusItemLevel, state.PreviousTribe, out var drops))
            return TribeActionOutcome.Abort;

        // Captured before the await below: PostTribeProgressCommandAndWaitAsync's own BonusItemLevel: 0 zeroes
        // this field in place on the tick thread before the await completes, so reading state.BonusItemLevel
        // afterward for the log line below would always report tier 0 (a pre-existing bug this fixes in
        // passing while this method is already being touched for the LevelMilestoneBonus wiring).
        var claimedTier = state.BonusItemLevel;

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            BonusItemLevel: 0, BonusItemValue: false, DropItems: drops), ct);

        logger.LogInformation(
            "Character {CharacterId} claimed level-milestone bonus for tier {Tier} ({DropCount} items)",
            characterId, claimedTier, drops.Length);

        return TribeActionOutcome.Ok();
    }

    /// <summary>tSort 9/10 -- ornament on/off, no gate at all.</summary>
    public async ValueTask<TribeActionOutcome> SetOrnamentAsync(Zone zone, PlayerRuntimeState state, int characterId,
        bool on, CancellationToken ct)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        // tSort 10 (OFF) additionally full-heals to the new max; tSort 9 (ON) does not touch Life/Mana.
        var command = on
            ? new TribeProgressZoneCommand(characterId, UseOrnament: true, UpdatedStats: updatedStats)
            : new TribeProgressZoneCommand(characterId, UseOrnament: false, Life: updatedStats.MaxLife,
                Mana: updatedStats.MaxMana, UpdatedStats: updatedStats);

        await zone.PostTribeProgressCommandAndWaitAsync(command, ct);

        // Routine, purely cosmetic per-request toggle -- Debug, not Information.
        logger.LogDebug("Character {CharacterId} set tribe ornament to {OnOff}", characterId, on ? "on" : "off");

        return TribeActionOutcome.Ok();
    }

    /// <summary>
    ///     tSort 11 -- Max Rebirth, Path B of the G1-G12 rebirth-advancement mechanism (S04_MyWork02.cpp:
    ///     10800,11342-11390, <c>__REBIRTH__</c>). Requires Level1 AND Level2 both already at their own caps
    ///     (equivalently, their combined sum at <see cref="RebirthProgression.CombinedLevelCap" />), Exp2 at
    ///     100% of Level2's threshold, a 10,000 CP toll, and the absolute rebirth cap
    ///     (<see cref="RebirthProgression.MaxRebirthGeneration" />) not yet reached; on success resets Exp2,
    ///     increments RebirthCount, debits the CP, banks <c>aZone241Time += 10</c>, fully heals, and recomputes
    ///     stats (RebirthCount feeds StatCalculator's critical-defence and critical-wrapper bonuses).
    /// </summary>
    /// <remarks>
    ///     Hardened against a legacy quirk the originating behavior contract explicitly calls out as a data-
    ///     loss bug, not a designed rule: the legacy unconditionally resets Exp2 and bumps aZone241Time the
    ///     instant the four preconditions above pass, BEFORE checking whether <see cref="MaxRebirth" /> (the
    ///     path-specific cap of 6, distinct from the absolute 12 above) has already been reached -- so a
    ///     character sitting at generation 6+ who re-grew Exp2 to 100% and retries Path B loses that bar for
    ///     zero reward. This method checks <see cref="MaxRebirth" /> before mutating anything, so no state is
    ///     ever touched on that path; see <see cref="MaxRebirth" />'s own remarks for why that check is
    ///     answered as a graceful in-band failure rather than the disconnect every other precondition above
    ///     uses.
    /// </remarks>
    public async ValueTask<TribeActionOutcome> RebirthAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.RebirthCount >= RebirthProgression.MaxRebirthGeneration ||
            state.Level + state.Level2 != RebirthProgression.CombinedLevelCap ||
            !RebirthProgression.IsHighLevelExperienceFull(state.Level2, state.Exp2) ||
            state.ContributionPoints < RebirthCpCost)
            return TribeActionOutcome.Abort;

        // Path-specific cap, checked only once every other precondition already passed -- see this method's
        // own <remarks> for why nothing above this line may ever mutate state.
        if (state.RebirthCount >= MaxRebirth)
        {
            logger.LogDebug(
                "Character {CharacterId} Max Rebirth (Path B) rejected: already at the path-specific cap ({RebirthCount}/{MaxRebirth})",
                characterId, state.RebirthCount, MaxRebirth);
            return TribeActionOutcome.Ok(1);
        }

        var newRebirthCount = state.RebirthCount + 1;
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, newRebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer));

        int newZone241Time;
        try
        {
            newZone241Time = await characters.AdjustZone241TimeAsync(characterId, 10, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} rebirth Zone241Time adjustment failed", characterId);
            return TribeActionOutcome.Abort;
        }

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - RebirthCpCost, RebirthCount: newRebirthCount, Exp2: 0,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats,
            RebirthBroadcast: true, Zone241Time: newZone241Time), ct);

        // Milestone-reward pass (generation 6/12 tribe-specific ground-item drop, plus a generation-12
        // server-wide notice naming the character) -- S04_MyWork05.cpp:4828-4865's MyWork::MaxRebirth, run
        // unconditionally after every successful transition on both rebirth paths. Deliberately NOT wired
        // here: no citation available to this pass establishes the actual tribe-keyed item ids or the notice
        // wording, and this project's policy against reproducing/guessing legacy-parity content from memory
        // means fabricating either is worse than leaving this an explicit, documented gap. Flagged for a
        // dedicated legacy-behavior-translator follow-up before this pass is wired in for either path.

        // A major, infrequent progression milestone -- Information regardless of how routine other tSorts are.
        logger.LogInformation(
            "Character {CharacterId} completed Max Rebirth (Path B): generation {OldGeneration} -> {NewGeneration}",
            characterId, state.RebirthCount, newRebirthCount);

        return TribeActionOutcome.Ok();
    }

    /// <summary>tSort 16 -- map/clan scroll, item 591, 1 CP, Force Leader/sub-master.</summary>
    public ValueTask<TribeActionOutcome> RedeemMapScrollAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        return RedeemScrollAsync(zone, state, characterId, 591, MapScrollCpCost, ct);
    }

    /// <summary>tSort 17 -- alert charm, item 590, 10 CP, Force Leader/sub-master.</summary>
    public ValueTask<TribeActionOutcome> RedeemAlertCharmAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        return RedeemScrollAsync(zone, state, characterId, 590, AlertCharmCpCost, ct);
    }

    /// <summary>
    ///     tSort 18 -- tower construction scroll, Force Leader/sub-master. Money debited but never notified (same as
    ///     tSort 4).
    /// </summary>
    public async ValueTask<TribeActionOutcome> UseTowerScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2))
            return TribeActionOutcome.Abort;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -TowerScrollMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            // Economy gate (insufficient funds) -- Warning, not Information.
            logger.LogWarning(ex, "Character {CharacterId} tower-scroll money debit failed (insufficient funds)",
                characterId);
            return TribeActionOutcome.Abort;
        }

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            DropItems: [new TribeGroundItemDrop(665, 1)]), ct);

        logger.LogInformation("Character {CharacterId} purchased a tower-construction scroll for tribe {Tribe}",
            characterId, state.Tribe);

        return TribeActionOutcome.Ok();
    }

    /// <summary>tSort 16 (map/clan scroll) / tSort 17 (alert charm) shared body -- Force Leader/sub-master.</summary>
    private async ValueTask<TribeActionOutcome> RedeemScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int itemId, int cpCost, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2) || state.ContributionPoints < cpCost)
            return TribeActionOutcome.Abort;

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - cpCost,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);

        logger.LogInformation("Character {CharacterId} redeemed item {ItemId} for {CpCost} CP", characterId, itemId,
            cpCost);

        return TribeActionOutcome.Ok();
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

    private PetStatContribution ComputePetContribution(PlayerRuntimeState state,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        return PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
    }
}
