namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The RvR zones whose per-slot type-state the CenterServer authors. Scalar zones (Zone194, FFA/335) use
///     index 0. Zone175 and the tribe-guard matrix are 2D and have their own dedicated setters.
/// </summary>
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

/// <summary>The Zone194 per-tribe ratio-bonus families (op33 case 301). All ratio kinds scale the raw value by 1/10.</summary>
public enum TribeRatioBonusKind : byte
{
    GeneralExperienceUp,
    ItemDropUp,
    ItemDropUpForMyoung
}

/// <summary>
///     Center-&gt;Zone world-event sort codes emitted by the cadence hosts (not received by the op33 ingester).
///     Reimplemented from Server/ts25center/S07_MyGame01.cpp:235-263 and S04_MyWork02.cpp:1151-1157.
/// </summary>
public static class CenterWorldEventSorts
{
    /// <summary>Daily-reset notice, zeroed payload (S07_MyGame01.cpp:236).</summary>
    public const int DailyReset = 1600;

    /// <summary>FFA/Zone335 refresh notice emitted alongside the op33 4000 echo (S04_MyWork02.cpp:1152).</summary>
    public const int FfaRefresh = 1601;

    /// <summary>Tribe-point sync, payload is the four current tribe scores (S07_MyGame01.cpp:261).</summary>
    public const int TribePointSync = 1234;

    /// <summary>The op33 world-event payload width (MAX_BROADCAST_DATA_SIZE under USE_ITEM_LINK_V2).</summary>
    public const int PayloadSize = 130;
}

/// <summary>The persisted world-row subset the Center authors and flushes (mirrors <c>game.WorldState</c>).</summary>
public sealed record CenterWorldRow(
    byte? Zone038WinTribe,
    int? Zone038WinTribeTime,
    bool TribeSymbolBattle,
    byte? MonsterSymbol,
    int? MonsterSymbolEndTime,
    byte? HighTribe,
    short UpdateTribePoint);

/// <summary>One persisted tribe row (mirrors <c>game.WorldStateTribes</c>).</summary>
public sealed record CenterTribeRow(
    byte TribeId,
    DateTime? SymbolDate,
    bool HasSymbol,
    int Points,
    bool IsClosed);

/// <summary>One persisted alliance offer (mirrors <c>game.WorldStateAllianceOffers</c>).</summary>
public readonly record struct CenterAllianceOffer(byte FromTribeId, byte ToTribeId, bool IsAccepted);
