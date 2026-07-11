using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record RvrSiegeEventRelayEntry(
    byte SourceShardId,
    int Sort,
    byte[] Data);

[GenerateDto]
public sealed partial record RvrSiegeEventRelayDto(
    long RelayId,
    int Sort,
    byte[] Data);
