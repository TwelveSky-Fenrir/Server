using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkLogoPayload : IFenrirWireType<GuildWorkLogoPayload>
{
    public required int Value { get; init; }
}
