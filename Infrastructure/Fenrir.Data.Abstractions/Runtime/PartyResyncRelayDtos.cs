using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum PartyResyncRelaySort : byte
{

        Request = 108,

        PartyInfo = 108,

        PartyBreak = 109
}

public sealed record PartyResyncRelayEntry(
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName);

[GenerateDto]
public sealed partial record PartyResyncRelayDto(
    long RelayId,
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName);
