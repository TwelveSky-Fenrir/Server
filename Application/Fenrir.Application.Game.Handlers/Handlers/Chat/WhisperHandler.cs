using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_SECRET_CHAT_SEND (opcode 39). Cross-tribe gating is commented out in this fork -- inter-tribe
///     whispers pass. Target resolved process-wide (unlike duel/trade/friend/mentor/party's same-zone-only
///     lookup, see <see cref="ZoneRegistry.TryGetPlayerAndZoneByName" />), falling back to the cross-shard
///     character-location directory on a same-shard miss. No mute gate applies here. Async (not inline):
///     the cross-shard fallback is an awaited DB call on the miss branch, and both handler kinds already run
///     on the per-connection session loop, never the zone tick (<c>SessionLoop.ProcessBufferAsync</c>).
/// </summary>
public sealed class WhisperHandler(IWhisperService whisperService, ILogger<WhisperHandler> logger)
    : IAsyncPacketHandler<WhisperRequest>
{
    // Socket is a reference-type array; a bare `default` would leave it null and crash the wire writer.
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public async ValueTask HandleAsync(WhisperRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content) || string.IsNullOrEmpty(packet.AvatarName))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        var resolution = await whisperService.ResolveAsync(sender, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);

        switch (resolution.Outcome)
        {
            case WhisperOutcome.SelfWhisper:
                return;

            case WhisperOutcome.TargetNotFound:
                // AuthType is always the fixed non-elevated value here, never the sender's own flag -- legacy's
                // "target not found" branch passes a literal 0, not uAuthInfo.AuthType (Server/ts25zone/S04_MyWork02.cpp:8036).
                session.Send(new WhisperResponse
                {
                    Result = 1,
                    ZoneNumber = 0,
                    AvatarName = packet.AvatarName,
                    Content = "",
                    AuthType = 0,
                    Link = EmptyLink
                });
                return;

            case WhisperOutcome.TargetOnAnotherShard:
                // No inter-shard relay exists yet (see IWhisperService's own remarks) -- the M1 wire protocol
                // has no distinct "can't deliver yet" Result value, so this degrades to the same code the
                // client already understands as "target not found," but is logged distinctly here so the gap
                // stays visible in telemetry instead of looking identical to a genuinely offline target.
                logger.LogInformation(
                    "Whisper from {SenderName} to {TargetName} could not be delivered: target is on shard {ShardId} (map {MapId}), and no inter-shard relay exists yet",
                    sender.Name, packet.AvatarName, resolution.OtherShardId, resolution.OtherMapId);
                session.Send(new WhisperResponse
                {
                    Result = 1,
                    ZoneNumber = 0,
                    AvatarName = packet.AvatarName,
                    Content = "",
                    AuthType = 0,
                    Link = EmptyLink
                });
                return;

            case WhisperOutcome.Delivered:
                var target = resolution.Target!;
                var targetZone = resolution.TargetZone!;

                // Echo to the sender (Result=0) before delivering to the target (Result=3) -- legacy ordering.
                // The two responses do NOT share the same AuthType: the direct sender-echo (Result=0) hardcodes
                // a literal 0 in legacy (Server/ts25zone/S04_MyWork02.cpp:8046, B_SECRET_CHAT_RECV(0, ..., 0, ...)
                // -- never uUserSort/AuthInfo-derived), so even a GM whispering sees AuthType=0 on their own
                // echo. Only the actual delivery-to-target packet (Result=3), reconstructed from the inter-zone
                // relay message, carries the sender's real elevated-status flag (RELAY_SECRET_CHAT_SEND's
                // `tAuth = tUserInfo->mPlayInfo->uAuthInfo.AuthType`, S04_MyWork02.cpp:8067, forwarded verbatim
                // by S04_MyWork04.cpp:39-40's B_SECRET_CHAT_RECV(3, ..., tAuthType, ...)) -- so only that one
                // response uses the sender's own IsGm flag.
                session.Send(new WhisperResponse
                {
                    Result = 0,
                    ZoneNumber = targetZone.MapId,
                    AvatarName = target.Name,
                    Content = packet.Content,
                    AuthType = 0,
                    Link = packet.Link
                });

                target.Session.Send(new WhisperResponse
                {
                    Result = 3,
                    ZoneNumber = 0,
                    AvatarName = sender.Name,
                    Content = packet.Content,
                    AuthType = zoneSession.IsGm ? 1 : 0,
                    Link = packet.Link
                });
                return;
        }
    }
}
