namespace Fenrir.Cluster.Directory;

public readonly record struct ZoneEndpoint(short ZoneId, string Host, int Port);
