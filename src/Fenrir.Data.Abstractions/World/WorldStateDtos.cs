using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

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
    long Revision,
    DateTime UpdatedAtUtc);

[GenerateDto]
public sealed partial record WorldStateTribeDto(
    byte TribeId,
    DateTime? SymbolDateUtc,
    bool HasSymbol,
    int Points,
    bool IsClosed,
    byte SymbolOwnerTribeId);

[GenerateDto]
public sealed partial record WorldStateAllianceOfferDto(
    byte FromTribeId,
    byte ToTribeId,
    bool IsAccepted);

[GenerateDto]
public sealed partial record TribeVoteDto(
    byte TribeId,
    byte SlotIndex,
    int CandidateCharacterId,
    short CandidateLevel,
    int KillOtherTribeCount,
    int VotePoint,
    DateTime RegisteredAtUtc);
