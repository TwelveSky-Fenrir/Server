using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class TempRegistrationIdleSweep(TribeQuotaRegistry registry, ILogger<TempRegistrationIdleSweep> logger)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(3);

        public void Sweep(DateTimeOffset nowUtc)
    {
        foreach (var entry in registry.SnapshotIdle(IdleTimeout, nowUtc))
        {
            logger.LogInformation(
                "TEMP_REGISTER_SEND idle timeout: disconnecting session {SessionId} (account {AccountId}, character {CharacterId}, tribe {Tribe}) -- registered at {RegisteredAtUtc:O} without completing avatar-selection/ready",
                entry.Session.SessionId, entry.AccountId, entry.CharacterId, entry.Tribe, entry.RegisteredAtUtc);

            entry.Session.Abort(DisconnectReason.IdleTimeout);

            registry.Release(entry.Session.SessionId);
        }
    }
}
