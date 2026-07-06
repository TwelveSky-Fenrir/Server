namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Port of the health-value-gain half of <c>MyUtil::ProcessForExperience</c> (S07_MyGame03.cpp:304-317):
///     independent of both character-experience and pet-experience crediting, gated only on the avatar
///     currently having positive health.
/// </summary>
public static class MonsterKillHealthGainCalculator
{
    /// <summary>One hundredth of the killed monster's configured life/health value.</summary>
    public static int ComputeHealthValueGain(int monsterLifeValue)
    {
        return monsterLifeValue / 100;
    }

    /// <summary>
    ///     The new health value: unchanged if <paramref name="currentLife" /> is already non-positive (a dead
    ///     avatar is never healed by this path) or if the clamped gain works out to zero/negative; otherwise
    ///     <paramref name="currentLife" /> plus <paramref name="healthValueGain" />, never exceeding
    ///     <paramref name="maxLife" />.
    /// </summary>
    public static int ComputeNewLife(int currentLife, int maxLife, int healthValueGain)
    {
        if (currentLife <= 0)
            return currentLife;

        var gain = currentLife + healthValueGain > maxLife ? maxLife - currentLife : healthValueGain;
        return gain > 0 ? currentLife + gain : currentLife;
    }
}
