using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WhisperService(
    ZoneRegistry zones,
    ICharacterShardLocationRepository characterShardLocations,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options)
    : IWhisperService
{
    public async ValueTask<WhisperResolution> ResolveAsync(PlayerRuntimeState sender, string targetAvatarName,
        string content, ItemLinkInfo link, int senderAuthType, CancellationToken cancellationToken)
    {
        if (sender.IsMuted)
            return new WhisperResolution(WhisperOutcome.SelfWhisper);

        if (string.Equals(sender.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            return new WhisperResolution(WhisperOutcome.SelfWhisper);

        if (zones.TryGetPlayerAndZoneByName(targetAvatarName, out var target, out var targetZone))
            return new WhisperResolution(WhisperOutcome.Delivered, target, targetZone);

        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
            return new WhisperResolution(WhisperOutcome.TargetNotFound);

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.Whisper,
            options.Value.ShardId,
            remote.CharacterId,
            null,
            (byte)senderAuthType,
            sender.Name,
            content,
            true,
            link.Index,
            link.Activity,
            link.Value,
            link.Socket[0],
            link.Socket[1],
            link.Socket[2])
        {
            SourceCharacterId = sender.CharacterId
        });

        return new WhisperResolution(WhisperOutcome.QueuedCrossShard, OtherShardId: remote.ShardId,
            OtherMapId: remote.MapId);
    }
}
