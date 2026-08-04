using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record WorldEventSnapshotRowDto(
    string EventKind,
    string OccurrenceKey,
    long Revision,
    string Phase,
    string CanonicalPayload,
    byte[] CanonicalPayloadHash,
    DateTime UpdatedAtUtc);
