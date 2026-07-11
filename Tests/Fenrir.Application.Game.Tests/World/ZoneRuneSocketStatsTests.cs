using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.ApplyRuneSocketCommand</c> directly -- the follow-up confirmation-pass fix closing the
///     "rune-socket stat recompute still deferred" gap. Before this fix, <see cref="RuneSocketZoneCommand.UpdatedStats" />
///     was always null from every real caller (<c>RuneSocketService.InsertAsync</c>/<c>RemoveAsync</c>), so a
///     rune-socket mutation never touched <see cref="PlayerRuntimeState.Stats" /> even though workstreams B5/B6
///     made the rune arrays a live <see cref="EquipmentService.RecomputeStats" /> input (via
///     <c>AssembleStatContexts</c> -&gt; <c>CosmeticContext</c>, consumed by <c>StatCalculator.PrimaryAttributes.cs</c>'s
///     Rune{Vitality,Strength,Ki,Wisdom}Bonus getters).
/// </summary>
public class ZoneRuneSocketStatsTests
{
    private static PlayerRuntimeState EnterPlayer(Zone zone, int characterId = 10, short level = 42)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, 1, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    /// <summary>
    ///     Same assembly shape <c>Zone.ApplyRuneSocketCommand</c> itself now uses, reused here only to build an
    ///     independent expected value from <paramref name="state" />'s CURRENT rune arrays -- never to
    ///     hand-derive a magnitude. Called once before and once after the rune mutation against the very same
    ///     <paramref name="state" /> instance, so every other live context field (zone/tribe-role/rank-buff/
    ///     costume/etc.) is identically whatever it already is on that player, isolating the rune arrays as the
    ///     only thing that can differ between the two calls.
    /// </summary>
    private static EffectiveStats RecomputeExpected(PlayerRuntimeState state)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, runtimeState: state);
    }

    [Fact]
    public void Insert_NoUpdatedStatsOverride_RecomputesStatsWithLiveRuneContribution()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = EnterPlayer(zone);

        // The in-memory HandleEnter path (unlike the DB-backed EnterWorldService) never seeds Stats -- this
        // fix is what makes it non-null for the first time, on the very first rune-socket command.
        Assert.Null(state.Stats);
        var beforeExpected = RecomputeExpected(state); // fresh character: RuneSystem/RuneSystemStat still [0,0,0,0]

        // Refine (ItemValueCodec's 3rd upgrade byte, byte2) decodes to RuneStatDecoder.Vitality -- see that
        // decoder's byte-mapping remarks. 60 is a plain positive-in-sbyte-range magnitude, not a legacy-cited
        // constant.
        var packedStat = ItemValueCodec.Encode(0, 0, 60, 0);
        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 2, 93516, packedStat, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(93516, state.RuneSystem[2]);
        Assert.Equal(packedStat, state.RuneSystemStat[2]);
        Assert.NotNull(state.Stats);

        var afterExpected = RecomputeExpected(state);

        // The fix actually threads runtimeState: state through -- the socketed rune measurably changed the
        // recompute (every other live context field is untouched between the two RecomputeExpected calls).
        Assert.NotEqual(beforeExpected.MaxLife, afterExpected.MaxLife);
        Assert.Equal(afterExpected, state.Stats!.Value);

        // ComputeMaxLife's vitality term is a direct, unwrapped `vitality * 20` (StatCalculator.Life.cs) -- 60
        // decoded Vitality points is +1200 MaxLife over the no-rune baseline; nothing else changes since the
        // socketed rune's other 3 bytes (Enchant/Combine/Socket) are all 0.
        Assert.Equal(beforeExpected.MaxLife + 1200, afterExpected.MaxLife);
        Assert.Equal(beforeExpected.MaxMana, afterExpected.MaxMana);
        Assert.Equal(beforeExpected.AttackPower, afterExpected.AttackPower);
    }

    [Fact]
    public void Remove_ClearsRuneStatContributionOnNextRecompute()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = EnterPlayer(zone);

        var packedStat = ItemValueCodec.Encode(0, 0, 60, 0);
        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 2, 93516, packedStat, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        var withRune = state.Stats!.Value;

        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 2, null, null, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.RuneSystem[2]);
        Assert.Equal(0, state.RuneSystemStat[2]);
        Assert.Equal(withRune.MaxLife - 1200, state.Stats!.Value.MaxLife);
    }

    [Fact]
    public void ExplicitUpdatedStatsOverride_StillWinsLastOverTheRecompute()
    {
        // No real caller supplies UpdatedStats today (see RuneSocketService's own remarks), but the
        // field/precedence stays honored for a future caller -- same "poster-supplied wins last" posture
        // Zone.ApplyAvatarBuffCommand gives its own UpdatedStats.
        var zone = ZoneTestKit.CreateZone(1);
        var state = EnterPlayer(zone);

        var overrideStats = new EffectiveStats(999999, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 0, 93514,
            ItemValueCodec.Encode(0, 0, 0, 0), overrideStats));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(overrideStats, state.Stats);
    }
}
