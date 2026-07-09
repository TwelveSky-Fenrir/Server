using System.Buffers;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Guilds;

/// <summary>
///     Mirrors a fresh GUILD_INFO onto every currently connected member of one guild, across every hosted zone --
///     not just the character whose action triggered the mutation, who already gets their own definitive
///     GuildActionResponse (with its own Result code) via <c>GuildActionHandler.SendResult</c>. Same
///     "walk every zone's own Players, filter, Session.Send" shape as <c>GuildAnnouncementHandler</c>: reading a
///     PlayerRuntimeState.GuildId and pushing a packet down its Session is safe from any thread, unlike mutating
///     that state, which is why this needs no ZoneCommand.
/// </summary>
public static class GuildInfoBroadcaster
{
    /// <param name="excludeCharacterId">
    ///     The acting character, already served by its own SendResult reply -- pass -1 (no member ever has a
    ///     negative CharacterId) when every member, actor included, should receive the mirror.
    /// </param>
    public static void BroadcastGuildInfo(ZoneRegistry zones, int guildId, int sort, GuildInfo info,
        int excludeCharacterId = -1)
    {
        var response = new GuildActionResponse { Result = 0, Sort = sort, GuildInfo = info };

        // Rent-once/write-once/copy-N-times -- same idiom as Zone's own broadcast helpers (e.g.
        // BroadcastAvatarAction, Zone.PlayerLifecycle.cs): the frame is encoded exactly once and copied into
        // every matching guild member's own transport, instead of each member independently re-serializing
        // the identical GuildActionResponse via Session.Send.
        var total = FrameWriter.FrameSizeOf<GuildActionResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var member in zone.Players)
                if (member.GuildId == guildId && member.CharacterId != excludeCharacterId &&
                    member.Session is ClientSession clientSession)
                    clientSession.SendRaw(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
