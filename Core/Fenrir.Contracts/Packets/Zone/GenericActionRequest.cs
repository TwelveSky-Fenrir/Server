using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_PROCESS_DATA_SEND (CLIENT.h:203-207) — catch-all level-2 channel: the handler dispatches on
///     <see cref="Sort" /> (pickup 201, skills 202-206, container moves 208-232/240-256/3000, GM
///     501-528, scripted duel 598-603, pet 700-701...). Unknown <c>tSort</c> → Quit (anti-fuzzing).
///     <c>Data</c> is re-read by the handler as <see cref="DefaultPData" /> (28 bytes, at payload offset
///     4) for "container move" sorts; the remaining bytes are ignored in those cases. Modeled here as a
///     raw blob — the typed re-read layer belongs to the handler, not the contract.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GenericAction, ExpectedSize = 143,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GenericActionRequest : IIncomingPacket<GenericActionRequest>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
