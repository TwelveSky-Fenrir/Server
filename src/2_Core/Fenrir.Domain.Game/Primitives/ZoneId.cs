namespace Fenrir.Domain.Game.Primitives;

/// <summary>
/// Numéro de zone/map (legacy : le numéro passé en argument CLI qui différencie les N instances de
/// <c>ts25zone</c> et détermine le port <c>1100 + N</c>). Adresse aussi la zone comme endpoint TCP.
/// </summary>
public readonly record struct ZoneId(short Value);
