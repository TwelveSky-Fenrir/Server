using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum ZoneReadyOutcome
{
    Admitted,
    Rejected
}

/// <summary>
///     Business logic for CZ_CLIENT_OK_FOR_ZONE_SEND (op13)'s 3 anti-cheat guardrails
///     (S04_MyWork02.cpp:1213-1291), in the same order as the reference: heartbeat watchdog, tribe anti-tamper,
///     auto-hunt anti-hack. See <c>ZoneReadyHandler</c>'s own remarks for the full rationale.
/// </summary>
public interface IZoneReadyService
{
    public ZoneReadyOutcome Validate(PlayerRuntimeState state, int tribe, int autoState);
}
