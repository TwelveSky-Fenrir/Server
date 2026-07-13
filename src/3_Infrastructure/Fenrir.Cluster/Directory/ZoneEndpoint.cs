namespace Fenrir.Cluster.Directory;

/// <summary>
/// Adresse publique d'une zone (host:port). Équivalent moderne de <c>ZONE_CONNECTION_INFO</c> que
/// <c>ts25center</c> écrivait en mémoire partagée mono-hôte (<c>S04_MyWork02.cpp:105-108</c>) — ici porté par
/// l'annuaire <c>runtime.GameServerDirectory</c>, sans la contrainte mono-hôte.
/// </summary>
public readonly record struct ZoneEndpoint(short ZoneId, string Host, int Port);
