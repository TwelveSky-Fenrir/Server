using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record ChatCrossShardWhisperEntry(
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    string Content,
    byte SenderAuthType);

[GenerateDto]
public sealed partial record ChatCrossShardWhisperDto(
    long RelayId,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    string Content,
    byte SenderAuthType);
