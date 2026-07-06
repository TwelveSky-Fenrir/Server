namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Distinguishes "this session has never explicitly requested the cash catalog" from any real version
    ///     number <see cref="Commerce.CommerceCatalogCache" /> could hand back -- excluded from
    ///     <see cref="Simulation.CashCatalogStaleNotifySystem" />'s proactive notify by design, matching
    ///     Server/ts25zone/S03_MyUser.cpp:423's own session-creation sentinel.
    /// </summary>
    public const int CashCatalogVersionUnknown = -1;

    /// <summary>
    ///     The cash-catalog version this client last knew about. Set to the live version whenever it
    ///     explicitly asks (<see cref="Handlers.Commerce.GetCashCatalogHandler" />), and reset back to
    ///     <see cref="CashCatalogVersionUnknown" /> the instant
    ///     <see cref="Simulation.CashCatalogStaleNotifySystem" /> proactively notifies it of a mismatch -- so
    ///     the same catalog bump is never re-notified twice (Server/ts25zone/S07_MyGame01.cpp:1991-1999).
    ///     Carried through an in-process zone-transfer handoff (<see cref="ZoneTransfer.CreateEnterData" />)
    ///     since the client keeps its one TCP connection across a same-cluster map transfer; a brand-new login
    ///     (never carried) correctly starts at <see cref="CashCatalogVersionUnknown" />, matching a session's
    ///     very first connection in legacy. Written directly from the request thread by
    ///     <see cref="Handlers.Commerce.GetCashCatalogHandler" /> -- an intentional, benign unsynchronized race
    ///     against this zone's own tick thread, the same posture already established by
    ///     <see cref="LastSentHeartbeat" />/<see cref="PrevSentHeartbeat" />.
    /// </summary>
    public int KnownCashCatalogVersion { get; set; } = CashCatalogVersionUnknown;
}
