using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_GUILD_FIND_SEND (opcode 78, doc 10 §0, verified S04_MyWork02.cpp:10787-10797). Requires the
///     caller to already be in a guild (silent return otherwise, matching the legacy's own bare
///     <c>return;</c> with no Quit and no response). Resolved process-wide (the legacy relays to
///     ts25playuser's cross-SERVER directory; Fenrir's cross-ZONE <see cref="ZoneRegistry" /> is the
///     equivalent single-process mapping) -- same "one of the two directory-backed lookups" posture as
///     <see cref="FriendLocateHandler" />'s own remarks (whisper is the other). <c>Result = -1</c> when the
///     named avatar is not currently online anywhere on this shard -- same not-found sentinel convention as
///     <see cref="FriendLocateHandler" />; the legacy's own ts25playuser "not found" value was not
///     independently re-derived byte-for-byte (open issue), this follows the established local convention.
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
