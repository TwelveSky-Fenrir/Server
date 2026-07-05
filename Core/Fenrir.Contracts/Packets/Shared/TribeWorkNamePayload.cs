using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

// Shared tData layout for tSort 2 (appoint sub-master) and tSort 3 (remove sub-master).
[FenrirWireType(13)]
public readonly partial record struct TribeWorkNamePayload : IFenrirWireType<TribeWorkNamePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
