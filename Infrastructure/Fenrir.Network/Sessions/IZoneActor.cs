namespace Fenrir.Network.Sessions;

/// <summary>
///     Deliberately empty marker for the per-map zone actor a session currently belongs to. The concrete actor
///     (<c>Fenrir.Application.Game.World.Zone</c>) lives ABOVE this assembly in the dependency graph, but
///     <see cref="ZoneClientSession" /> must carry a reference to its current zone so packet handlers route
///     commands without a per-packet registry lookup and without Fenrir.Network ever referencing the
///     Application layer — handlers pattern-match this back to the concrete <c>Zone</c> on their side of the
///     boundary.
/// </summary>
public interface IZoneActor;
