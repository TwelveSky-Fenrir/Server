using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Only ever sent on the "gate passed, target not found (or self-targeted)" path, with
///     <see cref="Result" />=1 (legacy's own default-initialized <c>tResult</c>,
///     Server/ts25zone/S04_MyWork04.cpp:305-306). Two other outcomes never produce this packet at all: an
///     unauthorized (non-GM) caller is disconnected outright with no reply, and a successful block is
///     legacy's own real oddity -- case 519 <c>return</c>s immediately, bypassing the shared epilogue every
///     sibling sub-command reaches via <c>break</c> (e.g. case 518 KICK), so the acting GM gets zero
///     acknowledgment that the ban actually happened (Server/ts25zone/S04_MyWork04.cpp:1487-1515). Silence is
///     the "it worked" signal on that path -- do not add a success reply here to "complete" the shape.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GmBlockAvatar, ExpectedSize = 5)]
public readonly partial record struct GmBlockAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
