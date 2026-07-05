using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;

namespace Fenrir.Application.Game.Abstractions.Tribes;

/// <summary>
///     Business logic behind CZ_TRIBE_NOTIFY_SEND (opcode 112), extracted out of
///     <see cref="TribeAnnouncementScrollHandler" />.
/// </summary>
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
    public bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session,
        string content);
}
