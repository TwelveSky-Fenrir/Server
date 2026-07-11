using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;
using static Fenrir.Application.Game.Tests.GameData.WorldDataTestRows;

namespace Fenrir.Application.Game.Tests.GameData;

/// <summary>
///     Per-row SKILL/NPC validation, the 300-skill capacity cap, and the lowest-index boot self-test added to
///     <see cref="WorldDataCacheBuilder" /> (A13). Legacy refs: <c>Skill_CheckValidElement</c>
///     (Server/Header/S15_MyShare.cpp:1277-1497), <c>Npc_CheckValidElement</c> (:1836-1963), and the
///     <c>MyGame::Init</c> lowest-index probe (Server/ts25zone/S07_MyGame01.cpp:1611-1636). Every case here drives
///     the full <see cref="WorldDataCacheBuilder.Build" /> path, since that is where the validators run.
/// </summary>
public class WorldDataCacheBuilderSkillNpcValidationTests
{
    // ---- Positive: a fully-populated, legacy-valid skill+npc graph round-trips ----------------------------------

    [Fact]
    public void Build_Accepts_ValidSkillAndNpcRowsWithChildren()
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1), ValidSkill(2)],
            SkillDescriptions =
            [
                new SkillDescriptionRowDto(1, 0, new string('D', 50)),
                new SkillDescriptionRowDto(1, 1, "second line")
            ],
            SkillGrades =
            [
                SkillGrade(1, 0) with { ManaUse = 10000, StunAttack = 100, FastRunSpeed = 1000, RunTime = 10000 },
                SkillGrade(1, 1)
            ],
            Npcs = [ValidNpc(1), ValidNpc(2)],
            NpcMenuOptions = [new NpcMenuOptionRowDto(1, 0, 1), new NpcMenuOptionRowDto(1, 1, 2)],
            NpcShopItems = [new NpcShopItemRowDto(1, 0, 0, 99999), new NpcShopItemRowDto(1, 0, 1, null)],
            NpcSkillOffers = [new NpcSkillOfferRowDto(0, 1, 1, 0, null, null, 0, 300)],
            NpcSpeeches = [new NpcSpeechRowDto(1, 0, 0, new string('S', 50))],
            NpcGambleCosts = [new NpcGambleCostRowDto(1, 0, 0, 100_000_000)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(2, cache.SkillsById.Count);
        Assert.Equal(2, cache.SkillsById[1].Grades.Length);
        Assert.Equal(2, cache.SkillsById[1].Descriptions.Length);
        Assert.Equal(2, cache.NpcsById.Count);
        Assert.Single(cache.NpcsById[1].SkillOffers);
    }

    // ---- SKILL per-row scalar bounds ---------------------------------------------------------------------------

    [Theory]
    [InlineData(301)] // above the 300-slot capacity cap
    [InlineData(400)]
    public void Build_Throws_WhenSkillIdExceedsThe300Cap(int skillId)
    {
        // Keep SkillId 1 present via a second valid row so the lowest-index self-test isn't what trips -- this
        // isolates the 300-cap check on the offending row.
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1), ValidSkill(skillId)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Skills", exception.Message);
        Assert.Contains($"SkillId={skillId}", exception.Message);
        Assert.Contains("index cap", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenSkillNameExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { Name = new string('X', 25) }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Skills", exception.Message);
        Assert.Contains("Name", exception.Message);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)5)]
    public void Build_Throws_WhenSkillTypeIsOutOfRange(byte type)
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { Type = type }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Skills", exception.Message);
        Assert.Contains("Type=", exception.Message);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    public void Build_Throws_WhenSkillAttackTypeIsOutOfRange(byte attackType)
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { AttackType = attackType }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("AttackType=", exception.Message);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)10001)]
    public void Build_Throws_WhenSkillDataNumber2DIsOutOfRange(short dataNumber2D)
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { DataNumber2D = dataNumber2D }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("DataNumber2D=", exception.Message);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)5)]
    public void Build_Throws_WhenSkillTribeInfo1IsOutOfRange(byte tribeInfo1)
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { TribeInfo1 = tribeInfo1 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("TribeInfo1=", exception.Message);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)11)]
    public void Build_Throws_WhenSkillTribeInfo2IsOutOfRange(byte tribeInfo2)
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { TribeInfo2 = tribeInfo2 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("TribeInfo2=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenSkillLearnSkillPointIsBelowFloor()
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { LearnSkillPoint = 0 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("LearnSkillPoint=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenSkillMaxUpgradePointIsBelowFloor()
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { MaxUpgradePoint = 0 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("MaxUpgradePoint=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenSkillTotalHitNumberExceedsMax()
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { TotalHitNumber = 11 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("TotalHitNumber=", exception.Message);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)1001)]
    public void Build_Throws_WhenSkillValidRadiusIsOutOfRange(short validRadius)
    {
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(1) with { ValidRadius = validRadius }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("ValidRadius=", exception.Message);
    }

    [Fact]
    public void Build_Accepts_SkillIdZero_SkippingAllPerRowValidation()
    {
        // SkillId 0 is the empty-slot sentinel: its (deliberately out-of-range) fields are never validated.
        var rows = MinimalRows() with
        {
            Skills =
            [
                ValidSkill(1),
                new SkillRowDto(0, "Padding", 9, 9, 20000, 9, 20, 0, 0, 99, 5000)
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.True(cache.SkillsById.ContainsKey(0));
    }

    // ---- SKILL child-table bounds (descriptions, grades) -------------------------------------------------------

    [Fact]
    public void Build_Throws_WhenSkillDescriptionExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            SkillDescriptions = [new SkillDescriptionRowDto(1, 0, new string('D', 51))]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.SkillDescriptions", exception.Message);
        Assert.Contains("LineIndex=0", exception.Message);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)10001)]
    public void Build_Throws_WhenSkillGradeManaUseIsOutOfRange(short manaUse)
    {
        var rows = MinimalRows() with
        {
            SkillGrades = [SkillGrade(1, 0) with { ManaUse = manaUse }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.SkillGrades", exception.Message);
        Assert.Contains("ManaUse=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenSkillGradeStunIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            SkillGrades = [SkillGrade(1, 0) with { StunAttack = 101 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.SkillGrades", exception.Message);
        Assert.Contains("StunAttack/StunDefense", exception.Message);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)1001)]
    public void Build_Throws_WhenSkillGradeFastRunSpeedIsOutOfRange(short fastRunSpeed)
    {
        var rows = MinimalRows() with
        {
            SkillGrades = [SkillGrade(1, 0) with { FastRunSpeed = fastRunSpeed }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("FastRunSpeed=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenSkillGradeRunTimeIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            SkillGrades = [SkillGrade(1, 0) with { RunTime = 10001 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("RunTime=", exception.Message);
    }

    // ---- NPC per-row scalar bounds -----------------------------------------------------------------------------

    [Theory]
    [InlineData(501)] // above the 500-slot capacity cap
    [InlineData(600)]
    public void Build_Throws_WhenNpcIdExceedsThe500Cap(int npcId)
    {
        // Keep NpcId 1 present so the lowest-index self-test isn't what trips -- this isolates the 500-cap check.
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1), ValidNpc(npcId)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Npcs", exception.Message);
        Assert.Contains($"NpcId={npcId}", exception.Message);
        Assert.Contains("index cap", exception.Message);
    }

    [Fact]
    public void Build_Accepts_NpcIdZero_SkippingAllPerRowValidation()
    {
        // NpcId 0 is the empty-slot sentinel: its (deliberately out-of-range) fields are never validated.
        var rows = MinimalRows() with
        {
            Npcs =
            [
                ValidNpc(1),
                new NpcRowDto(0, "Padding", 9, 99, 20000, 20000, 5000, 5000, 5000)
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.True(cache.NpcsById.ContainsKey(0));
    }

    [Fact]
    public void Build_Throws_WhenNpcNameExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1) with { Name = new string('X', 28) }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Npcs", exception.Message);
        Assert.Contains("Name", exception.Message);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    public void Build_Throws_WhenNpcTribeIsOutOfRange(byte tribe)
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1) with { Tribe = tribe }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("Tribe=", exception.Message);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)18)]
    public void Build_Throws_WhenNpcTypeIsOutOfRange(byte type)
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1) with { Type = type }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("Type=", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void Build_Throws_WhenNpcDataSortNumberIsOutOfRange(int dataSort)
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1) with { DataSortNumber2D = dataSort }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("DataSortNumber", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Build_Throws_WhenNpcSizeIsOutOfRange(int size)
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1) with { Size2 = size }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("Size1/Size2/Size3", exception.Message);
    }

    // ---- NPC child-table bounds --------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Build_Throws_WhenNpcMenuOptionIsOutOfRange(int optionId)
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1)],
            NpcMenuOptions = [new NpcMenuOptionRowDto(1, 0, optionId)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.NpcMenuOptions", exception.Message);
        Assert.Contains("OptionId=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenNpcShopItemIdIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1)],
            NpcShopItems = [new NpcShopItemRowDto(1, 0, 0, 100_000)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.NpcShopItems", exception.Message);
        Assert.Contains("ItemId=", exception.Message);
    }

    [Fact]
    public void Build_Accepts_NpcShopItemWithNullItemId()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1)],
            NpcShopItems = [new NpcShopItemRowDto(1, 0, 0, null)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Null(cache.NpcsById[1].ShopItems[0].ItemId);
    }

    [Fact]
    public void Build_Throws_WhenNpcSkillOfferIdIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1)],
            NpcSkillOffers = [new NpcSkillOfferRowDto(0, 1, 1, 0, null, null, 0, 301)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.NpcSkillOffers", exception.Message);
        Assert.Contains("SkillId=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenNpcSpeechExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1)],
            NpcSpeeches = [new NpcSpeechRowDto(1, 0, 0, new string('S', 51))]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.NpcSpeeches", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000_001)]
    public void Build_Throws_WhenNpcGambleCostIsOutOfRange(int value)
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1)],
            NpcGambleCosts = [new NpcGambleCostRowDto(1, 0, 0, value)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.NpcGambleCosts", exception.Message);
        Assert.Contains("Value=", exception.Message);
    }

    // ---- Lowest-index self-test --------------------------------------------------------------------------------

    [Fact]
    public void Build_Throws_WhenCanonicalSkillOneIsMissing()
    {
        // A valid, non-empty skill table that simply lacks SkillId 1 passes per-row validation but fails the
        // lowest-index self-test.
        var rows = MinimalRows() with
        {
            Skills = [ValidSkill(2)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Skills", exception.Message);
        Assert.Contains("self-test", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenNpcsNonEmptyButCanonicalNpcOneIsMissing()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(2)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Npcs", exception.Message);
        Assert.Contains("self-test", exception.Message);
    }

    [Fact]
    public void Build_Accepts_EmptyNpcs_SelfTestSkipsTheNpcProbe()
    {
        // world.Npcs is deliberately allowed to be empty in Fenrir (not a critical dataset), unlike legacy -- the
        // NPC lowest-index probe must not fire in that case.
        var (cache, _) = WorldDataCacheBuilder.Build(MinimalRows());

        Assert.Empty(cache.NpcsById);
    }

    [Fact]
    public void Build_Accepts_NpcsThatIncludeCanonicalNpcOne()
    {
        var rows = MinimalRows() with
        {
            Npcs = [ValidNpc(1), ValidNpc(2)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(2, cache.NpcsById.Count);
    }
}
