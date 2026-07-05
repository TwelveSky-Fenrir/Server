using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(112)]
public readonly partial record struct ObjectForMonster : IFenrirWireType<ObjectForMonster>
{
    public required int Index { get; init; }

    public required ActionInfo Action { get; init; }

    public required int LifeValue { get; init; }
}
