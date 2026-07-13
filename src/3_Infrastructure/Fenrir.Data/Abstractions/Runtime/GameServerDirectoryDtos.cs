using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateDto]
public sealed partial record ShardDirectoryEntryDto(
    byte ShardId,
    string Host,
    int Port,
    int Ccu,
    int Capacity,
    float TickP99Ms);

public static class GameServerDirectoryDefaults
{
    public const int StalenessCutoffSeconds = 15;
}
