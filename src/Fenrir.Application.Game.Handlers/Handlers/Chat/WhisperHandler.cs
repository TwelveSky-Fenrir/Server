using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class WhisperHandler(IWhisperService whisperService, ILogger<WhisperHandler> logger)
    : IAsyncPacketHandler<WhisperRequest>
{
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public async ValueTask HandleAsync(WhisperRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content) || string.IsNullOrEmpty(packet.AvatarName))
            return;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        var resolution = await whisperService
            .ResolveAsync(sender, packet.AvatarName, packet.Content, zoneSession.IsGm ? 1 : 0, cancellationToken)
            .ConfigureAwait(false);

        switch (resolution.Outcome)
        {
            case WhisperOutcome.SelfWhisper:
                return;

            case WhisperOutcome.TargetNotFound:
                session.Send(new WhisperResponse
                {
                    Result = 1,
                    ZoneNumber = 0,
                    AvatarName = packet.AvatarName,
                    Content = packet.Content,
                    AuthType = 0,
                    Link = EmptyLink
                });
                return;

            case WhisperOutcome.QueuedCrossShard:
                logger.LogDebug(
                    "Whisper from {SenderName} to {TargetName} queued for cross-shard delivery to shard {ShardId} (map {MapId})",
                    sender.Name, packet.AvatarName, resolution.OtherShardId, resolution.OtherMapId);
                session.Send(new WhisperResponse
                {
                    Result = 0,
                    ZoneNumber = resolution.OtherMapId ?? 0,
                    AvatarName = packet.AvatarName,
                    Content = packet.Content,
                    AuthType = 0,
                    Link = packet.Link
                });
                return;

            case WhisperOutcome.Delivered:
                var target = resolution.Target!;
                var targetZone = resolution.TargetZone!;

                session.Send(new WhisperResponse
                {
                    Result = 0,
                    ZoneNumber = targetZone.MapId,
                    AvatarName = target.Name,
                    Content = packet.Content,
                    AuthType = 0,
                    Link = packet.Link
                });

                if (!target.IsMovingZone)
                    target.Session.Send(new WhisperResponse
                    {
                        Result = 3,
                        ZoneNumber = 0,
                        AvatarName = sender.Name,
                        Content = packet.Content,
                        AuthType = zoneSession.IsGm ? 1 : 0,
                        Link = packet.Link
                    });
                else
                    logger.LogDebug(
                        "Whisper from {SenderName} to {TargetName} resolved locally but target is mid " +
                        "zone-transfer; delivery suppressed",
                        sender.Name, target.Name);

                return;
        }
    }
}
