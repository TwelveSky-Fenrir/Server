using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     Process-wide, thread-safe, hot-reloadable snapshot of the cash-shop (world.ItemMallProducts) and
///     blood-exchange (world.BloodExchangeCatalog) catalogs. Distinct from <see cref="WorldDataCache" />'s own
///     <c>CashCatalog</c>/<c>CashCatalogVersion</c>/<c>BloodExchangeCatalog</c> fields, which remain that
///     type's one-shot boot snapshot per its own documented "never mutated afterwards" contract -- every
///     *live* reader (purchase resolution, the CZ_GET_CASH_ITEM_INFO_SEND/CZ_DEMAND_BLOOD_MARK_SEND handlers,
///     the per-tick stale-notify system) must read this cache instead. Kept warm by
///     <c>Fenrir.Application.Game.Hosting.Commerce.CommerceCatalogRefreshHost</c>'s ~1 s periodic pass; the
///     client-notify half of this behavior lives in
///     <see cref="Fenrir.Application.Game.Domain.Simulation.CashCatalogStaleNotifySystem" /> (runs on every
///     500 ms zone legacy tick -- twice the reload cadence, matching production).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25extra/S07_MyGame01.cpp:9-16 (<c>MyGame::Init</c> zeroes the shared struct's
///     version/CRC/blood-version fields before the one synchronous startup load) ; :88-131 (the per-tick call
///     site -- the once-per-10-ticks guard immediately above this call is present in source but commented
///     out, unlike the still-active guard a few lines below for the unrelated four-guild refresh, so this
///     check genuinely runs every tick, not every 10th) ; :116-131 <c>InitItemMall</c> and
///     Server/ts25extra/S08_MyDB.cpp:140-235/237-251/253-267 (<c>GetItemMall</c>/<c>GetItemMallVersion</c>/
///     <c>GetClientCRC</c>) for the cash side ; S07_MyGame01.cpp:133-146 <c>InitBloodShop</c> and
///     S08_MyDB.cpp:269-299/301-315 (<c>GetBloodShop</c>/<c>GetBloodShopVersion</c>) for the blood side.
/// </remarks>
/// <remarks>
///     Data-layer simplification, left as an explicit implementer decision by the originating behavior
///     contract: rather than adding a second, lightweight version/CRC-only stored-procedure round trip
///     mirroring legacy's separate <c>GetItemMallVersion</c>/<c>GetClientCRC</c> queries, Fenrir re-fetches and
///     re-derives the whole table every tick via the existing <c>usp_ItemMallProduct_GetAll</c>/
///     <c>usp_BloodExchangeCatalog_GetAll</c> procedures already used for the full reload, then resolves
///     version/CRC locally from that same result set (<see cref="CashCatalogBuilder.ResolveVersion" />/
///     <see cref="CashCatalogBuilder.ResolveCrc" />/<see cref="BloodShopBuilder.ResolveVersion" />). One
///     consequence: a query failure now fails version+CRC+catalog together (old state simply stays in place,
///     retried next tick) rather than legacy's shape where a lightweight-query failure alone defaults just
///     that one value to zero (a difference that is strictly safer, never a parity regression a client could
///     observe). The CRC-written-before-the-reload-is-attempted-and-never-rolled-back ordering (see
///     <see cref="RefreshCashCatalogAsync" />) is still preserved for the one failure mode that *can* still
///     happen after a successful fetch: <see cref="GameData.CashCatalogBuilder.Build" /> throwing while
///     parsing the already-fetched rows. That path is not reachable today -- <c>CashCatalogBuilder.Build</c>
///     is defensive-by-design and never throws, since row-validation is explicitly out of scope for the
///     originating contract -- but is kept so a future row-validation hardening pass still gets the legacy
///     "old catalog/version stay, already-written CRC does not roll back" semantics for free.
/// </remarks>
public sealed class CommerceCatalogCache
{
    /// <summary>
    ///     Matches legacy <c>MyGame::Init</c>'s own zeroing of the shared struct's version/CRC/blood-version
    ///     fields before the first synchronous load -- never a real version number as long as seed data
    ///     reserves a nonzero sentinel value (it does today: see <see cref="CashCatalogBuilder.ResolveVersion" />/
    ///     <see cref="BloodShopBuilder.ResolveVersion" />'s own remarks).
    /// </summary>
    public const int InitialVersion = 0;

    private readonly Lock _lock = new();

    private readonly ILogger<CommerceCatalogCache> _logger;

    private ImmutableArray<BloodExchangeCatalogRowDto> _bloodCatalog = [];
    private int _bloodCatalogVersion = InitialVersion;

    private CashCatalogBuilder.CashCatalog _cashCatalog = CashCatalogBuilder.Build([]);
    private int _cashCatalogCrc = InitialVersion;
    private int _cashCatalogVersion = InitialVersion;
    private bool _cashShopSellEnabled = true;

    public CommerceCatalogCache(ILogger<CommerceCatalogCache> logger)
    {
        _logger = logger;
    }

    public CashCatalogBuilder.CashCatalog CashCatalog
    {
        get
        {
            lock (_lock)
            {
                return _cashCatalog;
            }
        }
    }

    public int CashCatalogVersion
    {
        get
        {
            lock (_lock)
            {
                return _cashCatalogVersion;
            }
        }
    }

    /// <summary>
    ///     Written a tick ahead of the catalog contents/version it nominally describes whenever a reload
    ///     attempt fails right after the CRC was already refreshed -- see <see cref="RefreshCashCatalogAsync" />.
    ///     Not read by anything else in Fenrir today; carried only because the shared structure this contract
    ///     describes has this field.
    /// </summary>
    public int CashCatalogCrc
    {
        get
        {
            lock (_lock)
            {
                return _cashCatalogCrc;
            }
        }
    }

    /// <summary>
    ///     Mirrors legacy's <c>CASH_INFO.mIsSellCash</c> shared-memory flag -- the administrative "cash shop is
    ///     open for purchases" gate that <c>BuyCashItemService</c> consults ahead of every purchase, independent
    ///     of <see cref="CashCatalogVersion" />. Unlike the version/CRC/catalog trio above, this is refreshed on
    ///     every successful reload attempt regardless of whether the version changed -- see
    ///     <see cref="RefreshCashCatalogAsync" />. Defaults to <c>true</c> (open) before the first reload and
    ///     whenever no sentinel row is seeded, matching Fenrir's pre-existing always-open behavior; see
    ///     <see cref="CashCatalogBuilder.ResolveSellEnabled" /> for the sentinel-row convention.
    /// </summary>
    public bool CashShopSellEnabled
    {
        get
        {
            lock (_lock)
            {
                return _cashShopSellEnabled;
            }
        }
    }

    public ImmutableArray<BloodExchangeCatalogRowDto> BloodExchangeCatalog
    {
        get
        {
            lock (_lock)
            {
                return _bloodCatalog;
            }
        }
    }

    public int BloodCatalogVersion
    {
        get
        {
            lock (_lock)
            {
                return _bloodCatalogVersion;
            }
        }
    }

    /// <summary>
    ///     Reloads both catalogs. Called once, synchronously, at GameServer startup (mirroring
    ///     <c>MyGame::Init</c>'s own one-shot startup call, before any connection is accepted) and thereafter
    ///     on <c>CommerceCatalogRefreshHost</c>'s ~1 s cadence. Cash and blood are attempted independently --
    ///     a cash failure never skips the blood attempt, or vice versa.
    /// </summary>
    public async Task RefreshAllAsync(IWorldDataRepository repository, CancellationToken ct)
    {
        await RefreshCashCatalogAsync(repository, ct).ConfigureAwait(false);
        await RefreshBloodCatalogAsync(repository, ct).ConfigureAwait(false);
    }

    /// <summary>Cash-catalog half of <see cref="RefreshAllAsync" /> -- see this type's own remarks for the ordering guarantee.</summary>
    public async Task RefreshCashCatalogAsync(IWorldDataRepository repository, CancellationToken ct)
    {
        ReadOnlyCollection<ItemMallProductRowDto> rows;
        try
        {
            rows = await repository.GetItemMallProductsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Cash-catalog reload query failed -- keeping the previous catalog/version/CRC, retrying next tick");
            return;
        }

        // Refreshed on every successful fetch, independent of the version-diff gate below -- the sell-enabled
        // flag has no version concept of its own in legacy (it's a standalone CASH_INFO field), so it must
        // not be skipped just because the catalog's own version happens not to have moved this tick.
        var newSellEnabled = CashCatalogBuilder.ResolveSellEnabled(rows);
        lock (_lock)
        {
            _cashShopSellEnabled = newSellEnabled;
        }

        var newVersion = CashCatalogBuilder.ResolveVersion(rows);

        int currentVersion;
        lock (_lock)
        {
            currentVersion = _cashCatalogVersion;
        }

        if (newVersion == currentVersion)
            return;

        // Written unconditionally, before the reload attempt below is even made, and never rolled back if
        // that reload subsequently fails -- Server/ts25extra/S07_MyGame01.cpp:116-131's own ordering.
        var newCrc = CashCatalogBuilder.ResolveCrc(rows);
        lock (_lock)
        {
            _cashCatalogCrc = newCrc;
        }

        try
        {
            var newCatalog = CashCatalogBuilder.Build(rows);
            lock (_lock)
            {
                _cashCatalog = newCatalog;
                _cashCatalogVersion = newVersion;
            }
        }
        catch (Exception ex)
        {
            // Not reachable today -- CashCatalogBuilder.Build never throws (see this type's own remarks) --
            // kept so old catalog/version stay in place, and the already-updated CRC above is not rolled
            // back, if a future row-validation pass ever makes this path reachable.
            _logger.LogWarning(ex,
                "Cash-catalog reload parse failed -- keeping the previous catalog/version (CRC already updated)");
        }
    }

    /// <summary>
    ///     Blood-catalog half of <see cref="RefreshAllAsync" />. No CRC concept applies here -- see this type's own
    ///     remarks.
    /// </summary>
    public async Task RefreshBloodCatalogAsync(IWorldDataRepository repository, CancellationToken ct)
    {
        ReadOnlyCollection<BloodExchangeCatalogRowDto> rows;
        try
        {
            rows = await repository.GetBloodExchangeCatalogAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Blood-catalog reload query failed -- keeping the previous catalog/version, retrying next tick");
            return;
        }

        var newVersion = BloodShopBuilder.ResolveVersion(rows);

        int currentVersion;
        lock (_lock)
        {
            currentVersion = _bloodCatalogVersion;
        }

        if (newVersion == currentVersion)
            return;

        lock (_lock)
        {
            _bloodCatalog = [.. rows];
            _bloodCatalogVersion = newVersion;
        }
    }
}
