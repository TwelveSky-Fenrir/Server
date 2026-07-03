using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_FRIEND_FIND_RECV (ZONE.h:732-736). <c>ZoneNumber=-1</c> when the friend is offline OR its tribe no longer
///     matches (S04_MyWork02.cpp:9277-9280).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendLocate, ExpectedSize = 9)]
public readonly partial record struct FriendLocateResponse : IOutgoingPacket
{
    public required int Index { get; init; }
    public required int ZoneNumber { get; init; }
}
