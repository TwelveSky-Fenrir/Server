namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Live snapshot of the game.WorldState singleton row -- the scalar half of the legacy WORLD_INFO struct
///     that this schema persists. Zone038WinTribe/MonsterSymbol/HighTribe use the DB's own sentinel (null, not
///     -1): 0 is itself a valid TribeId here.
/// </summary>
public readonly record struct WorldRvrState(
    byte? Zone038WinTribe,
    int? Zone038WinTribeTime,
    bool TribeSymbolBattle,
    byte? MonsterSymbol,
    int? MonsterSymbolEndTime,
    byte? HighTribe,
    short UpdateTribePoint);

/// <summary>Live snapshot of one tribe's game.WorldStateTribes row.</summary>
public readonly record struct TribeRvrState(
    byte TribeId,
    DateTime? SymbolDate,
    bool HasSymbol,
    int Points,
    bool IsClosed);

/// <summary>One (from, to) tribe alliance offer pair (game.WorldStateAllianceOffers).</summary>
public readonly record struct AllianceOfferState(byte FromTribeId, byte ToTribeId, bool IsAccepted);
