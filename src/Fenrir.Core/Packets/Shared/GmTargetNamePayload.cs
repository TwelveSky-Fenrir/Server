using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GmTargetNamePayload : IFenrirWireType<GmTargetNamePayload>
{
    [FixedString(13)] public required string TargetName { get; init; }
}
