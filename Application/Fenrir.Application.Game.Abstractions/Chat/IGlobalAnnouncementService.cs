using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_GENERAL_NOTICE_SEND (opcode 17): the sender's <c>uUserSort&gt;=1</c> GM gate
///     (Server/ts25zone/S04_MyWork02.cpp:1924-1943, equivalent to <see cref="GmCommandTier.Basic" />) and the
///     unfiltered, cluster-wide broadcast reached only when it passes. Owns the gate itself -- the same
///     "owns every send/abort itself" posture as <c>IGmBlockAvatarService</c> -- because the failure path
///     here is uniquely silent among every GM-gated command wired up so far: legacy's <c>Quit()</c>
///     disconnect call on this specific opcode's permission-failure branch is commented out
///     (Server/ts25zone/S04_MyWork02.cpp:1926-1930), so a denied sender must observe nothing at all -- no
///     reply, and unlike <c>IGmBlockAvatarService</c>'s unauthorized-caller disconnect, no session teardown
///     either.
/// </summary>
public interface IGlobalAnnouncementService
{
    /// <summary>
    ///     Attempts to broadcast <paramref name="content" /> cluster-wide from <paramref name="zoneSession" />.
    ///     A complete no-op (no reply, no disconnect) if the sender does not meet
    ///     <see cref="GmCommandTier.Basic" />.
    /// </summary>
    public void TryAnnounce(ZoneClientSession zoneSession, string content);
}
