namespace Fenrir.Application.Game.Domain.Combat;

public static class MonsterKillHealthGainCalculator
{

        public static int ComputeHealthValueGain(int monsterLifeValue)
    {
        return monsterLifeValue / 100;
    }

        public static int ComputeNewLife(int currentLife, int maxLife, int healthValueGain)
    {
        if (currentLife <= 0)
            return currentLife;

        var gain = currentLife + healthValueGain > maxLife ? maxLife - currentLife : healthValueGain;
        return gain > 0 ? currentLife + gain : currentLife;
    }
}
