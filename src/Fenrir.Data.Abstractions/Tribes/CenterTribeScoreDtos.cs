using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Tribes;

[GenerateDto]
public sealed partial record CenterTribeStatAggregateRowDto(
    byte TribeId,
    long StatSum);
