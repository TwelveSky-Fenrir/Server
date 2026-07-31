using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerWarEventCodes
{
    public const int ProvingGroundsNewWin = 751;

    public const int TowerState = 752;

    public const int TowerAttackState = 753;

    public const int TowerFirstHit = 754;

    public const int Countdown = 755;

    public const int Zone319WarBandStart = 756;

    public const int Zone319WarBandEnd = 763;

    public const int ProvingGroundsResultBandStart = 771;

    public const int ProvingGroundsResultBandEnd = 774;

    public static readonly FrozenSet<int> RecognizedInertCodes =
        BuildBand(ProvingGroundsNewWin, ProvingGroundsResultBandEnd);

    public static bool IsRecognizedInert(int eventSortCode)
    {
        return RecognizedInertCodes.Contains(eventSortCode);
    }

    private static FrozenSet<int> BuildBand(int inclusiveStart, int inclusiveEnd)
    {
        var codes = new int[inclusiveEnd - inclusiveStart + 1];
        for (var i = 0; i < codes.Length; i++)
            codes[i] = inclusiveStart + i;

        return codes.ToFrozenSet();
    }
}
