using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class WorldChatHandler(IWorldChatService worldChatService) : IInlinePacketHandler<WorldChatRequest>
{
    public void Handle(in WorldChatRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            // Server/ts25zone/S04_MyWork02.cpp:15145-15150 -- empty content is treated as a tampered client
            // (the level-10 gate at 15151-15155 is a distinct, out-of-scope disconnect condition).
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        var outcome = worldChatService.TrySendChat(sender, packet.Content);
        if (outcome == WorldChatOutcome.LevelTooLow)
            return;
    }
}
