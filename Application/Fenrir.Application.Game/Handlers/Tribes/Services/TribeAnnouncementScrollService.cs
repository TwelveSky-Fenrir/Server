using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Tribes.Services;

/// <summary>Business logic behind CZ_TRIBE_NOTIFY_SEND (opcode 112), extracted out of <see cref="TribeAnnouncementScrollHandler" />.</summary>
public interface ITribeAnnouncementScrollService
{
    /// <summary>
    ///     Consumes one <see cref="PlayerRuntimeState.TribeNotifyScrollCount" /> charge and relays same-tribe,
    ///     across every zone of this process (this build's <c>LNW33</c> branch; the "send to everyone" branch
    ///     is dead code here) -- unlike TribeAnnouncementHandler (op 80), there is no role gate at all. Sends
    ///     the sender's own <see cref="AvatarStatUpdateResponse" /> charge-count echo BEFORE the tribe-wide
    ///     broadcast (whose own same-tribe fan-out also reaches the sender) -- callers must not reorder these
    ///     two, wire-observable sends. Returns false (nothing sent) if the sender has no charge left.
    /// </summary>
    bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session, string content);
}

/// <summary>See <see cref="ITribeAnnouncementScrollService" />.</summary>
public sealed class TribeAnnouncementScrollService(ZoneRegistry zones) : ITribeAnnouncementScrollService
{
    public bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session,
        string content)
    {
        if (sender.TribeNotifyScrollCount < 1)
            return false;

        var newCount = sender.TribeNotifyScrollCount - 1;

        session.Send(new AvatarStatUpdateResponse { Sort = 69, Value = newCount, Value2 = 0 });

        var response = new TribeAnnouncementScrollResponse
            { TribeRole = sender.Tribe, AvatarName = sender.Name, Content = content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
                recipient.Session.Send(response);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: newCount));

        return true;
    }
}
