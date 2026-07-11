using System.Collections.Frozen;
using System.Collections.Immutable;
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
        zone.Tick(TimeSpan.FromMilliseconds(50));

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
        var (zone, state, system) = SetUp(Literal126MapId, new HashSet<short>());

        for (var i = 0; i < 5; i++)
            system.Simulate(zone, 1);

        Assert.Equal(0, state.NoManaCount);
    }

    [Fact]
    public void ConfiguredNonLiteralMapId_Escalates()
    {
        var (zone, state, system) = SetUp(ArbitraryConfiguredMapId,
            new HashSet<short> { ArbitraryConfiguredMapId });

        system.Simulate(zone, 1);

        Assert.Equal(1, state.NoManaCount);
    }

    [Fact]
    public void MapId126_ConfiguredInZone126Set_Escalates()
    {
        var (zone, state, system) = SetUp(Literal126MapId,
            new HashSet<short> { Literal126MapId });

        system.Simulate(zone, 1);

        Assert.Equal(1, state.NoManaCount);
    }
}
