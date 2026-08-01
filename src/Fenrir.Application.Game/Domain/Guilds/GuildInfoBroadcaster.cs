using System.Buffers;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildInfoBroadcaster
{
    public static void BroadcastGuildInfo(ZoneRegistry zones, int guildId, int sort, GuildInfo info,
        int excludeCharacterId = -1)
    {
        var response = new GuildActionResponse { Result = 0, Sort = sort, GuildInfo = info };

        var total = FrameWriter.FrameSizeOf<GuildActionResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var member in zone.Players)
                if (member.GuildId == guildId && member.CharacterId != excludeCharacterId &&
                    member.Session is { } clientSession)
                    clientSession.SendRaw(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
