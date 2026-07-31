using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(28)]
public readonly partial record struct DefaultPData : IFenrirWireType<DefaultPData>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Quantity1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int XPost2 { get; init; }

    public required int YPost2 { get; init; }
}
