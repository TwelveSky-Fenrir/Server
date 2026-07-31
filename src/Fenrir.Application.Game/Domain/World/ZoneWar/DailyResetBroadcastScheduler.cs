namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcastScheduler
{
    private DateOnly _lastFiredLocalDate = DateOnly.MinValue;

    // Heure LOCALE du serveur, comme le legacy (Server/Header/datetime.h:41 utilise localtime). Le verrou
    // porte sur la DATE locale et non sur la minute-du-jour : au passage a l'heure d'hiver 00h01 survient
    // deux fois, a une heure de jeu d'intervalle, et la proc n'est PAS idempotente sur une table vivante
    // (toute reclamation faite entre les deux tirs serait effacee et rejouable).
    public bool IsDue(DateTimeOffset localNow)
    {
        return localNow is { Hour: 0, Minute: 1 }
               && DateOnly.FromDateTime(localNow.DateTime) != _lastFiredLocalDate;
    }

    public void MarkFired(DateTimeOffset localNow)
    {
        _lastFiredLocalDate = DateOnly.FromDateTime(localNow.DateTime);
    }
}
