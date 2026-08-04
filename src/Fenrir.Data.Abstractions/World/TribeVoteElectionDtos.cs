using System.Collections.Immutable;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record TribeVoteElectionTribeStateDto(
    byte TribeId,
    Guid? CycleId,
    byte Phase,
    DateTime UpdatedAtUtc);

[GenerateDto]
public sealed partial record TribeVoteElectionCandidateDto(
    Guid CycleId,
    byte TribeId,
    byte SlotIndex,
    int CandidateCharacterId,
    short CandidateLevel,
    int KillOtherTribeCount,
    int VotePoint,
    DateTime RegisteredAtUtc);

public sealed record TribeVoteElectionSnapshot(
    ImmutableArray<TribeVoteElectionTribeStateDto> TribeStates,
    ImmutableArray<TribeVoteElectionCandidateDto> Candidates);
