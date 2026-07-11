using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(17)]
public readonly partial record struct GmCallPvpPayload : IFenrirWireType<GmCallPvpPayload>
{

        public required int DuelSlot { get; init; }

        [FixedString(13)]
    public required string TargetName { get; init; }
}
