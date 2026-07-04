using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_GUILD_FIND_SEND (opcode 78, doc 10 §0, verified S04_MyWork02.cpp:10787-10797). Resolved
///     process-wide via <see cref="ZoneRegistry" /> (one of the two directory-backed lookups, alongside
///     <see cref="FriendLocateHandler" />'s whisper case). <c>Result = -1</c> when not found; the legacy's
///     ts25playuser "not found" sentinel was not independently re-derived (open issue) -- this follows the
///     same convention as <see cref="FriendLocateHandler" />.
/// </summary>
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
            return; // "no guild" -- silent return, not a Quit (matches the legacy's own bare `return;`)

        var zoneNumber = zones.TryGetPlayerByName(packet.AvatarName, out var found) ? found.MapId : -1;

        session.Send(new FindGuildMemberResponse { Result = zoneNumber });
    }
}
