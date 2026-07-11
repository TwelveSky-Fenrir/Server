namespace Fenrir.Application.Game.Domain.World.WorldState;

public readonly record struct WorldRvrState(
    byte? Zone038WinTribe,
    int? Zone038WinTribeTime,
    bool TribeSymbolBattle,
    byte? MonsterSymbol,
    int? MonsterSymbolEndTime,
    byte? HighTribe,
    short UpdateTribePoint);

public readonly record struct TribeRvrState(
    byte TribeId,
    DateTime? SymbolDate,
    bool HasSymbol,
    int Points,
    bool IsClosed);

public readonly record struct AllianceOfferState(byte FromTribeId, byte ToTribeId, bool IsAccepted);
