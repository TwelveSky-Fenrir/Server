using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     A11 -- <see cref="MonsterEntity.Heal" />, the tower item-667 guardian-heal drain's own life mutation
///     (<see cref="Fenrir.Application.Game.Domain.Progression.TowerLifecycleSystem" />). Mirrors
///     <see cref="MonsterEntityTests" />'s own <c>CreateEntity</c> helper rather than sharing it -- kept as a
///     separate file per this task's own scope.
/// </summary>
public class MonsterEntityHealTests
{
    private static MonsterEntity CreateEntity(int life, int maxLife)
    {
        var template = WorldDataTestRows.Monster(500) with { Life = maxLife };
        var monster = MonsterEntity.Create(1, 1u, template, 1, 0, 0, 0, 50);

        if (life != maxLife)
            monster.TakeDamage(maxLife - life, out _);

        return monster;
    }

    [Fact]
    public void Heal_BelowMaxLife_IsAdditive()
    {
        var monster = CreateEntity(70, 100);

        monster.Heal(20);

        Assert.Equal(90, monster.Life);
    }

    [Fact]
    public void Heal_PastMaxLife_ClampsToMaxLife_NeverExceedsIt()
    {
        var monster = CreateEntity(95, 100);

        monster.Heal(50);

        Assert.Equal(100, monster.Life);
    }

    [Fact]
    public void Heal_AlreadyAtMaxLife_IsANoOp()
    {
        var monster = CreateEntity(100, 100);

        monster.Heal(10);

        Assert.Equal(100, monster.Life);
    }

    [Fact]
    public void Heal_ZeroAmount_IsANoOp()
    {
        var monster = CreateEntity(50, 100);

        monster.Heal(0);

        Assert.Equal(50, monster.Life);
    }

    [Fact]
    public void Heal_NegativeAmount_ContributesNoHealing()
    {
        var monster = CreateEntity(50, 100);

        monster.Heal(-30);

        Assert.Equal(50, monster.Life);
    }

    [Fact]
    public void Heal_AfterDeath_DoesNotResurrect()
    {
        var monster = CreateEntity(10, 100);
        monster.TakeDamage(10, out _);

        monster.Heal(50);

        Assert.Equal(0, monster.Life);
    }

    [Fact]
    public void Heal_TenPercentOfMaxLife_MatchesTheItem667Magnitude()
    {
        // The A11 contract's own heal magnitude (+10% of max life) -- exercised here as the exact call shape
        // TowerLifecycleSystem.ApplyPendingGuardianHeal makes: guardian.Heal(guardian.MaxLife / 10).
        var monster = CreateEntity(500, 1000);

        monster.Heal(monster.MaxLife / 10);

        Assert.Equal(600, monster.Life);
    }
}
