using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Regression coverage for the B11-zone126-escalation contract's correction of
///     <see cref="AutoHuntTickSystem" />'s zone-126-type flag: it now resolves through the canonical,
///     config-driven <see cref="Zone.IsZone126TypeZone" /> (<see cref="GameServerOptions.Zone126TypeMapIds" />)
///     rather than a hardcoded <c>MapId == 126</c> literal. These tests are deliberately independent of
///     <see cref="AutoHuntTickSystemBotUpkeepTests" /> (own minimal setup) so they exercise the config wiring
///     itself, not just the escalation arithmetic already covered there.
/// </summary>
public class AutoHuntTickSystemZone126ConfigTests
{
    private const short Literal126MapId = 126;
    private const short ArbitraryConfiguredMapId = 500;

    private static (Zone Zone, PlayerRuntimeState State, AutoHuntTickSystem System) SetUp(short mapId,
        ISet<short>? zone126TypeMapIds = null)
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(9999) }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var dirtyTracker = new DirtyTracker<int>();
        var opts = ZoneTestKit.Options();
        if (zone126TypeMapIds is not null)
            opts.Zone126TypeMapIds = zone126TypeMapIds;

        var system = new AutoHuntTickSystem(worldData, dirtyTracker, Options.Create(opts));
        var zone = ZoneTestKit.CreateZone(mapId, opts, dirtyTracker, [], worldData);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drains Enter; too short to run any system

        Assert.True(zone.TryGetPlayer(10, out var state));
        ZoneTestKit.DrainOutbound(pipe);

        state!.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        return (zone, state, system);
    }

    private static AutoHunt Config(params int[] buffStorePairs)
    {
        var buffStore = new int[16];
        Array.Copy(buffStorePairs, buffStore, buffStorePairs.Length);
        return new AutoHunt
        {
            BuffType = 0, BuffStore = buffStore, HuntType = 0, AttackType = new int[4],
            MonNum = 0, ItemType = 0, InvenCmd = 0, DeathCmd = 0, AnimalPreyCmd = 0, AnimalFoodCmd = 0
        };
    }

    // Holy Shield (82): no weapon requirement, slot 9, unaffordable mana cost (9999).
    private static SkillDefinition HolyShieldSkill(short manaUse)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, 10, 1, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty,
            [HolyShieldGrade(0, manaUse), HolyShieldGrade(1, manaUse)]);
    }

    private static SkillGradeRowDto HolyShieldGrade(byte gradeIndex, short manaUse)
    {
        return new SkillGradeRowDto(82, gradeIndex, manaUse, 0,
            0, 0, 0, 0, 0, 0,
            0, 40, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 20, 0, 0, 0,
            0, 0);
    }

    [Fact]
    public void MapId126_NotInConfiguredZone126Set_NeverEscalates()
    {
        // Before the B11-zone126-escalation correction this passed the hardcoded `MapId == 126` guess -- now the
        // flag is operator-configured (empty by default, same posture as IsZone241TypeZone/IsZone039TypeZone), so
        // a bare literal-126 map id with no configuration must NOT escalate.
        var (zone, state, system) = SetUp(Literal126MapId, zone126TypeMapIds: new HashSet<short>());

        for (var i = 0; i < 5; i++)
            system.Simulate(zone, 1);

        Assert.Equal(0, state.NoManaCount);
    }

    [Fact]
    public void ConfiguredNonLiteralMapId_Escalates()
    {
        // The flag must generalize to any operator-configured map id, not just the literal number 126 -- proves
        // the fix is genuinely config-driven (Zone.IsZone126TypeZone / GameServerOptions.Zone126TypeMapIds), not a
        // renamed hardcoded constant.
        var (zone, state, system) = SetUp(ArbitraryConfiguredMapId,
            zone126TypeMapIds: new HashSet<short> { ArbitraryConfiguredMapId });

        system.Simulate(zone, 1);

        Assert.Equal(1, state.NoManaCount);
    }

    [Fact]
    public void MapId126_ConfiguredInZone126Set_Escalates()
    {
        // The literal map id 126 remains a valid choice once an operator actually configures it -- this is the
        // "real deployment classifies map 126 as zone-126-type" case, distinct from the first test's bare-literal
        // case.
        var (zone, state, system) = SetUp(Literal126MapId,
            zone126TypeMapIds: new HashSet<short> { Literal126MapId });

        system.Simulate(zone, 1);

        Assert.Equal(1, state.NoManaCount);
    }
}
