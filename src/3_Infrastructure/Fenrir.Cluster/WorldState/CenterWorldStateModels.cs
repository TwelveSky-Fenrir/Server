namespace Fenrir.Cluster.WorldState;

public enum WorldZoneStateKind : byte
{
    Zone049,
    Zone051,
    Zone053,
    Zone267,
    Zone241,
    Zone194,
    ZoneFfa335
}

public enum TribeRatioBonusKind : byte
{
    GeneralExperienceUp,
    ItemDropUp,
    ItemDropUpForMyoung
}

public static class CenterWorldEventSorts
{
    public const int DailyReset = 1600;

    public const int FfaRefresh = 1601;

    public const int TribePointSync = 1234;

    public const int PayloadSize = 130;
}

public sealed record CenterWorldRow(
    byte? Zone038WinTribe,
    int? Zone038WinTribeTime,
    bool TribeSymbolBattle,
    byte? MonsterSymbol,
    int? MonsterSymbolEndTime,
    byte? HighTribe,
    short UpdateTribePoint);

public sealed record CenterTribeRow(
    byte TribeId,
    DateTime? SymbolDate,
    bool HasSymbol,
    int Points,
    bool IsClosed);

public readonly record struct CenterAllianceOffer(byte FromTribeId, byte ToTribeId, bool IsAccepted);
