using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_WORK_SEND (CLIENT.h:216-220) — generic guild sub-command channel (S04_MyWork02.cpp:9969).
///     <c>Data</c> is the raw 500-byte <c>tData</c> buffer; its layout depends on <see cref="Sort" />
///     (tSort) and is interpreted by the application layer, not by this wire contract (doc 10 §1).
///     Always answered with a single ZC_GUILD_WORK_RECV to the requester.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildAction, ExpectedSize = 513,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildActionRequest : IIncomingPacket<GuildActionRequest>
{
    public required int Sort { get; init; }
    [FixedArray(500)] public required byte[] Data { get; init; }
}
