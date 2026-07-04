using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

// world.usp_GemSocket_GetAll; GemSocketId is the legacy 1-based array slot index. Ordinal-mapped.
[GenerateDto]
public sealed partial record GemSocketRowDto(
    int GemSocketId,
    int Type,
    int Value01,
    int Value02,
    int Value03,
    int Value04);
