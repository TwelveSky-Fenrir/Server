using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.GemSockets row -- ordinal contract of world.usp_GemSocket_GetAll (2,891 rows; GemSocketId is
///     the legacy 1-based array slot position). Constructor order must track the SELECT column order exactly
///     (invariant I-04); [GenerateDto] maps by position, not by name.
/// </summary>
[GenerateDto]
public sealed partial record GemSocketRowDto(
    int GemSocketId,
    int Type,
    int Value01,
    int Value02,
    int Value03,
    int Value04);
