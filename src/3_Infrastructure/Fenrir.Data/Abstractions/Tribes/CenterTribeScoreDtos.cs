using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Tribes;

/// <summary>
///     One tribe's raw stat-sum row from <c>game.usp_WorldState_RecomputeTribeScores</c> (the CenterServer
///     6-beat tribe-score recompute). The 1000 baseline and tribe 3's flat +800 are applied Fenrir-side by
///     <c>TribeScoreRecompute</c>, not carried here. Lives in the data assembly (not Fenrir.Cluster) because the
///     CaeriusNet DTO source generator only runs where the CaeriusNet package is referenced.
/// </summary>
[GenerateDto]
public sealed partial record CenterTribeStatAggregateRowDto(
    byte TribeId,
    long StatSum);
