using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(112)]
public readonly partial record struct ObjectForMonster : IFenrirWireType<ObjectForMonster>
{
    public required int Index { get; init; }

    public required ActionInfo Action { get; init; }

    public required int LifeValue { get; init; }
}
