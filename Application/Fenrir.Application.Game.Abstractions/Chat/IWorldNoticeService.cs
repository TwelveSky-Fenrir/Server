namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for the legacy <c>MyUtil::BroadcastNotice</c> helper (Server/ts25zone/S07_MyGame03.cpp:
///     767-775): a permission-less relay broadcast (<c>RELAY_GENERAL_NOTICE_SEND</c>, relay sort 102) reused
///     by several system-triggered call sites across <c>ts25zone</c> -- e.g. UpgradeCape's every-success
///     "RANKUP" notice (Server/ts25zone/S04_MyWork02.cpp:14746-14750) -- that is DISTINCT from, but produces
///     the IDENTICAL client-facing wire response as, the <c>CZ_GENERAL_NOTICE_SEND</c> (op17) GM command's
///     own <see cref="IGlobalAnnouncementService" /> (that interface owns opcode 17's own <c>uUserSort</c>
///     permission gate and its uniquely-silent denial semantics; this interface owns none of that, since a
///     system-triggered notice has no player "sender" to gate in the first place). Both ultimately relay the
///     same sort 102 (<c>RELAY_GENERAL_NOTICE_SEND</c>) and are received the identical way on each zone
///     server (<c>ProcessForRelay</c> case 102 -&gt; <c>B_GENERAL_NOTICE_RECV</c> -&gt;
///     <c>ZCP_GENERAL_NOTICE_RECV</c>, opcode 20, Server/ts25zone/S04_MyWork04.cpp:25-36), so they share the
///     exact same wire response and the exact same shard-wide fan-out shape --
///     <see cref="IGlobalAnnouncementService" />'s own remarks explain why that shard-local scope (this
///     process's own <c>ZoneRegistry</c>, not the whole cluster) is the correct, already-precedented Fenrir
///     boundary for this legacy relay mechanism (Fenrir has no <c>ts25center</c>-equivalent process to
///     re-fan-out across shards -- see <c>WhisperService</c>'s own remarks for the same, already-documented
///     limitation).
/// </summary>
public interface IWorldNoticeService
{
    /// <summary>
    ///     Broadcasts <paramref name="content" /> (client-visible as a system chat notice) to every ready,
    ///     non-mid-transfer player across every zone this shard hosts. A no-op with no error if no zone
    ///     currently has any connected player.
    /// </summary>
    public void Broadcast(string content);
}
