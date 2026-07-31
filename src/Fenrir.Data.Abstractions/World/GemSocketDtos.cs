using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record GemSocketRowDto(
    int GemSocketId,
    int Type,
    int Value01,
    int Value02,
    int Value03,
    int Value04);
