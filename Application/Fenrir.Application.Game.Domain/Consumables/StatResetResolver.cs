namespace Fenrir.Application.Game.Domain.Consumables;

public static class StatResetResolver
{
    public enum CleanseOutcome
    {
        Success,

                AlreadyAtFloor
    }

    public enum LevelBand
    {
        UpTo99,
        Level100To112,
        Level113PlusNoRebirth,
        Level145PlusWithRebirth
    }

        public enum StatSelector
    {
        Strength = 1,
        Dexterity = 2,
        Vitality = 3,
        Intelligence = 4
    }

    public const int StatFloor = 1;

        public static bool TryResolveLevelBand(short level, int rebirthCount, out LevelBand band)
    {
        switch (level)
        {
            case <= 99:
                band = LevelBand.UpTo99;
                return true;
            case <= 112:
                band = LevelBand.Level100To112;
                return true;
            case >= 145 when rebirthCount >= 1:
                band = LevelBand.Level145PlusWithRebirth;
                return true;
            case >= 113 when rebirthCount == 0:
                band = LevelBand.Level113PlusNoRebirth;
                return true;
            default:
                band = default;
                return false;
        }
    }

        public static ClearResult ResolveStatsClear(int statVit, int statStr, int statInt, int statDex)
    {
        var refund = AboveFloor(statVit) + AboveFloor(statStr) + AboveFloor(statInt) + AboveFloor(statDex);
        return new ClearResult(StatFloor, StatFloor, StatFloor, StatFloor, refund);
    }

        public static CleanseResult ResolveStatCleanse(int currentValue)
    {
        if (currentValue <= StatFloor)
            return new CleanseResult(CleanseOutcome.AlreadyAtFloor, currentValue, 0);

        return new CleanseResult(CleanseOutcome.Success, StatFloor, AboveFloor(currentValue));
    }

    private static int AboveFloor(int value)
    {
        return Math.Max(0, value - StatFloor);
    }

    public readonly record struct ClearResult(
        int NewStatVit,
        int NewStatStr,
        int NewStatInt,
        int NewStatDex,
        int RefundedPoints);

    public readonly record struct CleanseResult(CleanseOutcome Outcome, int NewValue, int RefundedPoints)
    {
        public bool Succeeded => Outcome == CleanseOutcome.Success;
    }
}
