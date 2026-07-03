using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_CHANGE_MASTER_SEND (CLIENT.h l.111-115): registered-but-DEAD legacy opcode — W_CHANGE_MASTER_SEND is a
///     100% empty body (S04_MyWork02.cpp l.1655-1658): no read, no reply (LCP 27 is never emitted), no Quit, no
///     state check whatsoever. The contract exists so the frame decoder knows the 62-byte size and never
///     desynchronizes/disconnects a client that sends it; <c>AllowedStates</c> stays empty (= legal in any state)
///     because the legacy handler performs zero validation.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMaster,
    ExpectedSize = 62)]
public readonly partial record struct ChangeMasterRequest : IIncomingPacket<ChangeMasterRequest>
{
    /// <summary>Never read by the legacy handler.</summary>
    public required int AvatarPost { get; init; }

    /// <summary>Never read by the legacy handler.</summary>
    [FixedString(49)]
    public required string MasterId { get; init; }
}
