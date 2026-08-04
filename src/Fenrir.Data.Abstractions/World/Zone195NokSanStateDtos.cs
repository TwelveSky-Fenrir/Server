using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record Zone195NokSanStateRowDto(
    long Revision,
    byte OwnerSlot0,
    byte OwnerSlot2,
    byte OwnerSlot3,
    byte StonesHeld0,
    byte StonesHeld1,
    byte StonesHeld2,
    byte StonesHeld3,
    DateTime UpdatedAtUtc);

[GenerateDto]
public sealed partial record Zone195NokSanCaptureRowDto(
    short MapId,
    byte Phase,
    int CapturerCharacterId,
    byte CapturerTribe,
    string CapturerName,
    int RemainingTime,
    int PhaseAccumulatorTicks);
