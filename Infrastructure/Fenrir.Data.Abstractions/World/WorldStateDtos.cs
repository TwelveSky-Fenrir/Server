using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

/// <summary>game.usp_WorldState_Get's 1st result set -- the game.WorldState singleton row.</summary>
[GenerateDto]
public sealed partial record WorldStateRowDto(
    byte Id,
    byte? Zone038WinTribe,
    int? Zone038WinTribeTime,
    bool TribeSymbolBattle,
    byte? MonsterSymbol,
    int? MonsterSymbolEndTime,
    byte? HighTribe,
    short UpdateTribePoint,
    DateTime UpdatedAtUtc);

/// <summary>game.usp_WorldState_Get's 2nd result set, ordered by TribeId -- one row per tribe (0-3).</summary>
[GenerateDto]
public sealed partial record WorldStateTribeDto(
    byte TribeId,
    DateTime? SymbolDate,
    bool HasSymbol,
    int Points,
    bool IsClosed);

/// <summary>game.usp_WorldState_Get's 3rd result set, ordered by (FromTribeId, ToTribeId).</summary>
[GenerateDto]
public sealed partial record WorldStateAllianceOfferDto(
    byte FromTribeId,
    byte ToTribeId,
    bool IsAccepted);

/// <summary>game.usp_TribeVote_GetByTribe -- one occupied Force Leader candidate slot, ordered by VotePoint DESC.</summary>
[GenerateDto]
public sealed partial record TribeVoteDto(
    byte TribeId,
    byte SlotIndex,
    int CandidateCharacterId,
    short CandidateLevel,
    int KillOtherTribeCount,
    int VotePoint,
    DateTime RegisteredAtUtc);
