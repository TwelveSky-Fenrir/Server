using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Commerce;

public sealed class CommerceCatalogCache
{
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

    public async Task RefreshAllAsync(IWorldDataRepository repository, CancellationToken ct)
    {
        await RefreshCashCatalogAsync(repository, ct).ConfigureAwait(false);
        await RefreshBloodCatalogAsync(repository, ct).ConfigureAwait(false);
    }

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
            _logger.LogWarning(ex,
                "Cash-catalog reload parse failed -- keeping the previous catalog/version (CRC already updated)");
        }
    }

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
