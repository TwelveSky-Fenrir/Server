using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Per-tick personal-shop region re-validation (behavior contract C21§E): every avatar whose
///     <see cref="PlayerRuntimeState.PshopOpen" /> flag is currently set is re-checked, every legacy tick,
///     against <see cref="PersonalShopRegionPolicy.IsWithinPermittedRegion" />; one that has drifted (or been
///     moved) outside every one of the five permitted regions is disconnected outright. Independent of, and
///     in addition to, the one-time open-time gate <c>OpenShopStallService.PrepareAsync</c> already applies
///     via <see cref="ProxyShopZonePolicy.IsWithinMarketDistrict" />.
/// </summary>
/// <remarks>
///     Réf. C++ (via behavior contract C21§E, not independently re-opened this session):
///     Server/ts25zone/S07_MyGame04.cpp:333-340 (the per-tick call site, inside the general avatar update
///     routine); Server/Header/mapcheck.h:189-244,237.
///     <para>
///         <b>A strict, hard-disconnect anti-exploit re-check, not a graceful shop-close</b> (Side effects
///         E): no coded response, no repositioning, no "close the shop gracefully" state change of its own --
///         termination of the connection is the only durable effect this specific check performs.
///         <see cref="DisconnectReason.StateViolation" /> is reused here rather than adding a dedicated enum
///         member, matching <see cref="DeathGateTickSystem" />'s own <c>mProtect_ReviveHack</c> anti-abuse
///         force-quit safety valve -- the closest existing precedent in this cluster for "an in-tick,
///         no-coded-response anti-exploit disconnect" (contrast <see cref="DisconnectReason.Faulted" />, whose
///         own doc comment scopes it to connection-time precondition rejections instead). A dedicated
///         <c>PersonalShopRegionViolation</c> value could be introduced later purely for finer-grained
///         disconnect-reason metrics if that granularity is ever wanted -- not required for correctness.
///     </para>
///     <para>
///         Deliberately skips a mid-zone-transfer avatar (<see cref="PlayerRuntimeState.IsMovingZone" />) --
///         a Fenrir-only robustness addition with no located legacy equivalent (legacy has no comparable
///         concept, being single-threaded/single-process-per-zone), matching the same defensive posture
///         <see cref="HoisundoCountdownSystem" /> already applies for its own per-tick avatar re-check, so a
///         transient handoff-in-flight position can never trip this check.
///     </para>
/// </remarks>
public sealed class PersonalShopRegionEnforcementSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var state in zone.Players)
        {
            if (!state.PshopOpen || state.IsMovingZone)
                continue;

            if (PersonalShopRegionPolicy.IsWithinPermittedRegion(zone.MapId, state.Tribe, state.PosX, state.PosY,
                    state.PosZ))
                continue;

            (toDisconnect ??= []).Add(state);
        }

        if (toDisconnect is null)
            return;

        // Deferred to after the full player scan, same posture as DeathGateTickSystem/HoisundoCountdownSystem's
        // own toForceQuit/toDisconnect lists: Abort() only requests teardown (it does not synchronously remove
        // the player from zone.Players), so disconnecting mid-enumeration here would risk mutating the very
        // collection this loop is iterating.
        foreach (var state in toDisconnect)
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
    }
}
