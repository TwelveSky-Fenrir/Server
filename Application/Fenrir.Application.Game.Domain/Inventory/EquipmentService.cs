using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>Bridges the raw equipment container onto StatCalculator's input shape and back onto EffectiveStats.</summary>
public static class EquipmentService
{
    /// <summary>An ItemId no longer present in the catalog is skipped rather than thrown on.</summary>
    public static ImmutableArray<EquippedItemSlot> BuildEquippedSlots(
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        var builder = ImmutableArray.CreateBuilder<EquippedItemSlot>(equipmentContainer.Count);

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (!itemsById.TryGetValue(stack.ItemId, out var definition))
                continue;

            builder.Add(new EquippedItemSlot(slot, definition.Item, stack.Enchant, stack.Combine, stack.Refine,
                stack.Socket, stack.SocketGem1, stack.SocketGem2, stack.SocketGem3));
        }

        return builder.ToImmutable();
    }

    /// <summary>Recomputes effective stats from the current Equipment container, buffs, and equipped pet.</summary>
    /// <param name="runtimeState">
    ///     When supplied, the cosmetic/zone/consumable/mount stat contexts are assembled from this player's
    ///     runtime state (rune/costume/stellar, ornament/rank-buff/tribe-role/zone, the five potion counters,
    ///     mount) and threaded into <see cref="StatCalculator.ComputeEffectiveStats" />. Introduced by
    ///     workstream B1 as the assembly point the cited finding names, this is no longer a pure signature
    ///     extension: workstreams B2/B4-B8 wired real <see cref="StatCalculator" /> formulas onto most of what
    ///     <see cref="AssembleStatContexts" /> assembles (rune -- <c>StatCalculator.PrimaryAttributes.cs</c>'s
    ///     Rune{Vitality,Strength,Ki,Wisdom}Bonus getters, see <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.RuneSystem" />'s
    ///     own remarks; costume enchant; the five Eat*Potion elixir counters; mount rolled attributes; rank-buff/
    ///     tribe-role/guild-buff), so passing a real runtime state DOES change the numeric result for those
    ///     inputs today -- see each context's own remarks in <see cref="AssembleStatContexts" /> below for which
    ///     specific fields still stay neutral pending their own follow-up. Null (still every call site except
    ///     <c>Zone.ApplyRuneSocketCommand</c>, the one production caller that supplies the live
    ///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState" /> -- see that method's own
    ///     remarks) leaves all four contexts at their neutral default; the very first post-login stat snapshot
    ///     built by <c>Application.Game.Services.ZoneLifecycle.EnterWorldService</c> is a structural instance of
    ///     this, not an oversight -- it runs before <c>Zone.HandleEnter</c> ever constructs this character's
    ///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState" />, so there is no runtime state
    ///     to pass yet at that specific call site.
    /// </param>
    public static EffectiveStats RecomputeStats(
        CharacterBaseAttributes attributes,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        WorldDataCache worldData,
        BuffInfo? buffs = null,
        PetStatContribution pet = default,
        PlayerRuntimeState? runtimeState = null)
    {
        var equipped = BuildEquippedSlots(equipmentContainer, worldData.ItemsById);
        var (cosmetic, zone, consumable, mount) = AssembleStatContexts(runtimeState, worldData);
        // B4-set: feed the non-NXT legacy set number (StatCalculator.DetectLegacySetNumber, MyUtil::
        // ReturnSetItemValue) into the existing set-bonus path -- ResolveEffectiveSetNumber overlays NXT on
        // top when it matches, matching legacy's own first-match-wins order.
        var legacySetNumber = StatCalculator.DetectLegacySetNumber(equipped);
        // B13-socket-prerequisites: threads the (Type,Value02)-keyed gem-socket effect table so
        // StatCalculator.ComputeAttackPower can fold each equipped item's SOCKET_GEM_V2 blob -- see that
        // method's own remarks for why only AttackPower actually reads it.
        return StatCalculator.ComputeEffectiveStats(attributes, equipped, worldData.LevelsByLevel, buffs,
            legacySetNumber, pet,
            cosmetic, zone, consumable, mount,
            worldData.GemSocketsByTypeAndValue);
    }

    /// <summary>
    ///     Maps the raw <see cref="PlayerRuntimeState" /> fields onto the four <see cref="StatCalculator" />
    ///     input contexts. Only fields that already have a runtime source are populated; inputs the contract
    ///     enumerates that have no <see cref="PlayerRuntimeState" /> field yet (ornament gold/silver time,
    ///     rage gauge, drunk state, HP-boost/warrior-pill flags, the resolved mount grade and absorb magnitude)
    ///     stay at their neutral default and are a tracked follow-up -- see each context's own XML doc. The
    ///     costume-enchant <c>cs</c> magnitude (workstream B6) is no longer one of those gaps:
    ///     <see cref="PlayerRuntimeState.CostumeIndex" />/<see cref="PlayerRuntimeState.CostumeDate" /> are
    ///     decoded via <see cref="StatCalculator.DecodeCostumeEnchantCs" /> below. A null state returns four
    ///     neutral defaults, identical to the equipment-only computation.
    ///     <para>
    ///         The potion-event cap/tribe and the four B4G opt-in flags are deliberately NOT in the
    ///         "tracked follow-up" list above -- workstream B7-fourguild-eventtier-source (wave14) confirmed
    ///         these have no <see cref="PlayerRuntimeState" /> field to add in the first place: every one of
    ///         their six backing legacy fields is provably always at its constructor-default/reset value in the
    ///         compiled legacy build (no live write site anywhere in any of the 9 executables, and no DB
    ///         persistence path for the B4G flags either). See <c>ConsumableContext.MaxPotionEventNum</c>'s own
    ///         remarks and <c>StatCalculator.FourGuildElixirContribution.cs</c>'s "MISSING INPUTS" note for the
    ///         full citation trail -- closing this further needs a product decision, not more legacy research.
    ///     </para>
    /// </summary>
    /// <param name="state">The character's runtime state, or null for the equipment-only computation.</param>
    /// <param name="worldData">
    ///     Needed (workstream B6) to resolve <see cref="CosmeticContext.CostumeValue" /> from the worn costume's
    ///     <c>world.Items</c> row -- <see cref="StatCalculator" /> itself holds no item catalog, so this
    ///     assembly point is where the lookup happens.
    /// </param>
    private static (CosmeticContext Cosmetic, ZoneContext Zone, ConsumableContext Consumable, MountContext Mount)
        AssembleStatContexts(PlayerRuntimeState? state, WorldDataCache worldData)
    {
        if (state is null)
            return (default, default, default, default);

        var costumeFound = worldData.ItemsById.TryGetValue(state.CostumeNumber, out var costumeItem);
        var costumeValue = StatCalculator.ComputeCostumeBaseStatBlock(
            state.CostumeNumber, costumeFound,
            costumeItem?.Item.Vitality ?? 0, costumeItem?.Item.Strength ?? 0,
            costumeItem?.Item.Intelligent ?? 0, costumeItem?.Item.Dexterity ?? 0);

        var cosmetic = new CosmeticContext(
            state.RuneSystem,
            state.RuneSystemStat,
            state.CostumeNumber,
            state.CostumeState,
            state.StellarCoreNumber,
            costumeValue,
            // B6: aCostumeIndex/aCostumeDate now both have PlayerRuntimeState sources (CostumeIndex
            // pre-existing, CostumeDate added by this workstream) -- decode the live enchant magnitude exactly
            // as MyFactor::SetAvatar's GetEnchantCostumeValue1 call does (function.h:2141-2163): zero whenever
            // the costume isn't in the worn index range (10-19), which is also CostumeIndex's -1 "nothing
            // worn"/"nothing selected" default, so an untouched character sees no change from before this
            // wiring landed.
            StatCalculator.DecodeCostumeEnchantCs(state.CostumeIndex, state.CostumeDate.AsSpan()));

        var zone = new ZoneContext(
            state.MapId,
            state.UseOrnament,
            RankBuffType: state.RankBuffType,
            TribeRole: state.TribeRole,
            GuildBuffActive: state.GuildBuffActive,
            GuildId: state.GuildId ?? 0);

        var consumable = new ConsumableContext(
            state.EatLifePotion,
            state.EatManaPotion,
            state.EatStrPotion,
            state.EatDexPotion,
            state.EatElePotion);

        var mount = new MountContext(
            state.AnimalNumber,
            AbsorbActive: state.AnimalAbsorbState != 0,
            RuntimeAttributes: state.MountRolledAttributes);

        return (cosmetic, zone, consumable, mount);
    }
}
