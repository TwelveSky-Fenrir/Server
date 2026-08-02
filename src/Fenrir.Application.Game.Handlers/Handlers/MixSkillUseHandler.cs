using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class MixSkillUseHandler : IInlinePacketHandler<MixSkillUseRequest>
{
    public void Handle(in MixSkillUseRequest packet, IPacketSession session)
    {
    }
}
