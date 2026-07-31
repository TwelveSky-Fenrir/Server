using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_MESSAGE_ID_RECV Server/Header/Protocol/ZONE.h:1385-1388 ; mort en M33/LNW33 : unique appelant S04_MyWork03.cpp:1082 a la fois commente et sous #ifdef __GOD__ absent, aucune table d'identifiants de message n'existe dans Server/.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SystemMessage,
    ExpectedSize = 5)]
public readonly partial record struct SystemMessageResponse : IOutgoingPacket
{
    public required int MessageId { get; init; }
}
