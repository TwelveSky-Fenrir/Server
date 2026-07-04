using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op24 CL_CHANGE_MASTER_SEND — legacy handler body is empty (S04_MyWork02.cpp l.1655-1658), teacher/master
///     feature removed without cleanup; consumed silently, never replies.
/// </summary>
public sealed class ChangeMasterHandler : IInlinePacketHandler<ChangeMasterRequest>
{
    public void Handle(in ChangeMasterRequest packet, IPacketSession session)
    {
    }
}
