using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterEntityTests
{
    private static MonsterEntity CreateEntity(int life = 100)
    {
        var template = WorldDataTestRows.Monster(500) with { Life = life };
        return MonsterEntity.Create(1, 1u, template, 1, 0, 0, 0, 50);
    }

    [Fact]
    public void Create_SeedsLifeAndPositionFromHome()
    {
        var monster = CreateEntity(250);

        Assert.Equal(250, monster.Life);
        Assert.Equal(250, monster.MaxLife);
        Assert.Equal(0, monster.PosX);
        Assert.Equal(MonsterAiState.Spawning, monster.AiState);
    }

    [Fact]
    public void TakeDamage_ReducesLife_AndNeverGoesBelowZero()
    {
        var monster = CreateEntity();

        var died = monster.TakeDamage(30, out var remaining);

        Assert.False(died);
        Assert.Equal(70, remaining);
        Assert.Equal(70, monster.Life);
    }

    [Fact]
    public void TakeDamage_OverkillClampsToZero_NeverNegative()
    {
        var monster = CreateEntity(20);

        monster.TakeDamage(500, out var remaining);

        Assert.Equal(0, remaining);
        Assert.Equal(0, monster.Life);
    }

    [Fact]
    public void TakeDamage_ExactlyLethal_ReturnsTrueOnce()
    {
        var monster = CreateEntity(50);

        var died = monster.TakeDamage(50, out var remaining);

        Assert.True(died);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void TakeDamage_AfterDeath_IsANoOp_NeverReturnsTrueTwice()
    {
        var monster = CreateEntity(10);

        var firstKill = monster.TakeDamage(10, out _);
        var secondHit = monster.TakeDamage(10, out var remaining);

        Assert.True(firstKill);
        Assert.False(secondHit); // must never re-trigger death-processing for the same monster
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void TakeDamage_NegativeAmount_ContributesNoDamage()
    {
        var monster = CreateEntity(40);

        monster.TakeDamage(-100, out var remaining);

        Assert.Equal(40, remaining);
    }

    [Fact]
    public void TakeDamage_ConcurrentAttackers_ExactlyOneClaimsTheKill()
    {
        // Simulates two attackers racing to deliver the killing blow -- only one may ever get died=true.
        var monster = CreateEntity();

        var results = new bool[50];
        Parallel.For(0, 50, i => results[i] = monster.TakeDamage(2, out _));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(0, monster.Life);
    }
}
