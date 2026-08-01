namespace Fenrir.Data.Abstractions.Runtime;

public interface ISessionTicketRepository
{
    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, short targetMapId, CancellationToken ct);

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct);

    // Annule une passation sans la consommer. Server/ts25login/S04_MyWork02.cpp:1613-1641 (op23) :
    // register_2 repasse l'emplacement en LP_UPDATE_USER, que la zone refuse (S07_MyGame01.cpp:1057-1060).
    public ValueTask RevokeAsync(int accountId, CancellationToken ct);

    public ValueTask PurgeExpiredAsync(CancellationToken ct);
}
