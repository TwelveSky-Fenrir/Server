using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Quests;

public sealed class QuestProgressHandler(
    IQuestProgressService questProgressService,
    ILogger<QuestProgressHandler> logger)
    : IAsyncPacketHandler<QuestProgressRequest>
{
    public async ValueTask HandleAsync(QuestProgressRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: QuestProgressRequest (op36) received for character {CharacterId}, sort {Sort}, page1 {Page1} index1 {Index1}",
            session.SessionId, characterId, packet.Sort, packet.Page1, packet.Index1);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, zoneSession, zone, state, characterId, accountId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(QuestProgressRequest packet, IZoneSession zoneSession, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken ct)
    {
        QuestActionResult? result = packet.Sort switch
        {
            1 => await questProgressService.AcceptAsync(packet, state, zone, characterId, ct),
            2 => await questProgressService.CompleteAsync(packet, state, zone, characterId, accountId, ct),
            3 => await questProgressService.ReceiveAsync(packet, state, zone, characterId, ct),
            4 => await questProgressService.ExchangeAsync(packet, state, zone, characterId, ct),
            5 => await questProgressService.AbandonAsync(packet, state, zone, characterId, ct),
            _ => null
        };

        if (result is not { Success: true })
        {
            logger.LogWarning(
                "Quest action rejected for character {CharacterId}: sort {Sort} (1=Accept,2=Complete,3=Receive,4=Exchange,5=Abandon) returned no result or failure",
                characterId, packet.Sort);
            return;
        }

        SendEcho(zoneSession, packet);
    }

    private static void SendEcho(IPacketSession session, QuestProgressRequest packet)
    {
        session.Send(new QuestProgressResponse
        {
            Sort = packet.Sort, Page = packet.Page1, Index = packet.Index1, XPost = packet.XPost,
            YPost = packet.YPost
        });
    }
}
