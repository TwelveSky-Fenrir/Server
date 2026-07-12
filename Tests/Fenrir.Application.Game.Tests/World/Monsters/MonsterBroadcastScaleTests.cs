using Fenrir.Application.Game.Domain.World.Monsters;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterBroadcastScaleTests
{
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(28)]
    public void TribeSymbolStoneSpecialTypes_ResolveToWidestScale(byte specialType)
    {
        Assert.Equal(3, MonsterBroadcastScale.ForMonster(1, specialType));
    }

    [Theory]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(18)]
    [InlineData(35)]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(38)]
    public void OtherWidestScaleSpecialTypes_ResolveToWidestScale(byte specialType)
    {
        Assert.Equal(3, MonsterBroadcastScale.ForMonster(1, specialType));
    }

    [Fact]
    public void TowerSpecialType_ResolvesToNarrowestScale()
    {
        Assert.Equal(1, MonsterBroadcastScale.ForMonster(1, 10));
    }

    [Fact]
    public void UnclassifiedSpecialType_ResolvesToNarrowestScale()
    {
        Assert.Equal(1, MonsterBroadcastScale.ForMonster(1, 99));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void TribeGuardMonsterTypes_ResolveToIntermediateScale(byte monsterType)
    {
        Assert.Equal(2, MonsterBroadcastScale.ForMonster(monsterType, 0));
    }

    [Fact]
    public void UnclassifiedMonsterType_ResolvesToNarrowestScale()
    {
        Assert.Equal(1, MonsterBroadcastScale.ForMonster(2, 11));
    }
}
