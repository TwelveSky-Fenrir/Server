using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Legacy leaves GuildInfo as uninitialized stack garbage on failure (and on tSort 1001 success) — must zero-fill here.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildAction, ExpectedSize = 1397)]
public readonly partial record struct GuildActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required GuildInfo GuildInfo { get; init; }
}
