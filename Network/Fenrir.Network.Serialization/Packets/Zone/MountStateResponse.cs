using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <remarks>
///     ZC_ANIMAL_STATE_RECV (op102) -- the direct confirmation echo for a successful
///     <see cref="MountStateRequest" /> Sort 1-4. Sent only to the requesting session, never broadcast
///     (Server/Header/unity.h:9-12, Server/Header/Protocol/DEFINE.h:759-762), and only after every other
///     side effect of that sort has already been applied: for Sort 3 (Mount) this means after both AOI
///     broadcasts (Server/ts25zone/S04_MyWork02.cpp:11839-11861); for Sort 4 (Dismount) after the absorb-clear
///     and state-13 notices (Server/ts25zone/S04_MyWork02.cpp:11862-11888).
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MountState, ExpectedSize = 9)]
public readonly record struct MountStateResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
