using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

// Layout LC_CHANGE_MASTER_RECV Server/Header/Protocol/LOGIN.h:229-233 (struct autonome declarant son propre tProtocol) ; mort en M33/LNW33 : le handler entrant BEGIN_CL(CHANGE_MASTER_SEND) a un corps vide (ts25login/S04_MyWork02.cpp:1643-1646), l'octet 27 n'est jamais emis.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ChangeMaster,
    ExpectedSize = 5)]
public readonly partial record struct ChangeMasterResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
