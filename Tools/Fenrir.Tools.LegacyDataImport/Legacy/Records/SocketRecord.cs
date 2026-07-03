namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>Faithful in-memory shape of a legacy <c>SOCKET_INFO</c> record (Header/Protocol/STRUCT.h:1970-1977).</summary>
internal sealed record SocketRecord(
    int Type,
    int Value01,
    int Value02,
    int Value03,
    int Value04);
