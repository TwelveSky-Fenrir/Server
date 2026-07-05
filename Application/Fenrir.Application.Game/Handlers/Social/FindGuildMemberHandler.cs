using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_GUILD_FIND_SEND (opcode 78) -- lookup is process-wide via <see cref="ZoneRegistry" />.</summary>
public sealed class FindGuildMemberHandler(ZoneRegistry zones) : IInlinePacketHandler<FindGuildMemberRequest>
{
    public void Handle(in FindGuildMemberRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var asker) || asker is null)
            return;

        if (asker.GuildId is null)
            return; // No guild: silent return, not Quit (matches legacy's bare return).

        var zoneNumber = zones.TryGetPlayerByName(packet.AvatarName, out var found) ? found.MapId : -1;

        session.Send(new FindGuildMemberResponse { Result = zoneNumber });
    }
}
