using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public static class MonsterDeathSequence
{
    public static MonsterKnockbackVector BeginCorpseCountdown(MonsterEntity monster, float killerX, float killerZ,
        bool isCriticalHit, IRandomSource random)
    {
        var knockback = MonsterDeathKnockback.Compute(killerX, killerZ, monster.PosX, monster.PosZ,
            monster.Template.DamageType, isCriticalHit, random);

        monster.AiState = MonsterAiState.Dead;
        monster.StateTicks = 0;
        monster.StateFrameAccumulator = 0f;

        monster.TargetLocationX = knockback.X;
        monster.TargetLocationY = 0f;
        monster.TargetLocationZ = knockback.Z;

        monster.Heading = WireHeading.Between(monster.PosX, monster.PosZ, killerX, killerZ);

        return knockback;
    }

    public static bool IsCorpseCountdownComplete(MonsterEntity monster)
    {
        return monster.StateFrameAccumulator >= Math.Max(1, (int)monster.Template.FrameInfo5);
    }
}
