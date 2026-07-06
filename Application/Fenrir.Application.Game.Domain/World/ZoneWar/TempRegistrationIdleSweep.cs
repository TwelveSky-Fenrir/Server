using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Periodic background check: a connection that completed TEMP_REGISTER_SEND (op11/ZoneHandshake) but never
///     followed up with avatar-selection/ready keeps its counted tribe slot until this sweep forcibly
///     disconnects it, three minutes after its registration timestamp -- only then is the slot freed for
///     others (<see cref="TribeQuotaRegistry.Release" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1963-1990 (periodic background cleanup: a not-yet-ready
///     connection is dropped after three minutes since its registration timestamp).
/// </remarks>
public sealed class TempRegistrationIdleSweep(TribeQuotaRegistry registry, ILogger<TempRegistrationIdleSweep> logger)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    ///     Disconnects every connection <see cref="TribeQuotaRegistry.SnapshotIdle" /> reports as abandoned as of
    ///     <paramref name="nowUtc" />.
    /// </summary>
    public void Sweep(DateTimeOffset nowUtc)
    {
        foreach (var entry in registry.SnapshotIdle(IdleTimeout, nowUtc))
        {
            logger.LogInformation(
                "TEMP_REGISTER_SEND idle timeout: disconnecting session {SessionId} (account {AccountId}, character {CharacterId}, tribe {Tribe}) -- registered at {RegisteredAtUtc:O} without completing avatar-selection/ready",
                entry.Session.SessionId, entry.AccountId, entry.CharacterId, entry.Tribe, entry.RegisteredAtUtc);

            entry.Session.Abort(DisconnectReason.IdleTimeout);

            // Defensive/redundant with the generic disconnect-teardown path (which also calls Release
            // unconditionally) -- Abort() only requests teardown, it does not run it synchronously, so this
            // sweep frees the slot immediately rather than waiting for that path to catch up.
            registry.Release(entry.Session.SessionId);
        }
    }
}
