using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

// Ordinal-mapped: ctor order must match usp_GameServer_GetDirectory's SELECT order.
[GenerateDto]
public sealed partial record ShardDirectoryEntryDto(
    byte ShardId,
    string Host,
    int Port,
    int Ccu,
    int Capacity,
    float TickP99Ms);
