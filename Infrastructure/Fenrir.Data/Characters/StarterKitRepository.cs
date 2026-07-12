using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record StarterKitRepository(ICaeriusNetDbContext Db) : IStarterKitRepository
{
    // Deliberately NOT cached via .AddInMemoryCache (caching-opportunities-audit, moyenne, flagged this read
    // as "free and correct to cache" -- verified this session that claim is false for this call shape).
    // CaeriusNet 11.1.1 (pinned in Directory.Packages.props) never routes a QueryMultipleReadOnlyCollectionAsync
    // call through CacheHelper.TryRetrieveFromCache/StoreInCache -- confirmed by decompiling CaeriusNet.dll's
    // IL: every arity's entry point dispatches into a shared InstrumentMultiResultSetAsync helper whose body
    // only wires OpenTelemetry Activity/metric instrumentation (StartStoredProcedureActivity, BuildMetricTags,
    // Stopwatch timing, RecordSuccess/RecordError), never the cache. CaeriusNet.xml's own doc comments
    // corroborate this from the outside: every QueryMultiple*Async entry lists only spParameters/cancellationToken,
    // omitting the CacheKey/CacheExpiration/CacheType bullets every single-result-set/scalar command's doc lists
    // explicitly. Chaining .AddInMemoryCache(...) here would compile and look like a fix but be entirely inert.
    // Restructuring this into 5 independently-cacheable single-result-set procedures was considered and
    // rejected: world.StarterKitEquipment/Inventory/Skills/Hotkeys and world.Zones are tiny seed-only tables,
    // this call only fires on human-triggered avatar creation (not a tick/connection hot path), and the
    // procedure's own header comment records its one-round-trip shape as a deliberate choice for
    // CreateAvatarHandler's op17 kit assembly -- trading that for 5 round trips on every cache miss just to
    // enable caching on an already-cheap, low-frequency read is a net-negative trade, not a free win.
    public async ValueTask<StarterKitBundle> GetByPreviousTribeAsync(byte previousTribe, short mapId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_StarterKit_GetByPreviousTribe", 64)
            .AddParameter("PreviousTribe", previousTribe, SqlDbType.TinyInt)
            .AddParameter("MapId", mapId, SqlDbType.SmallInt)
            .Build();

        var (equipment, inventory, skills, hotkeys, spawn) = await Db
            .QueryMultipleReadOnlyCollectionAsync<StarterKitEquipmentRowDto, StarterKitInventoryRowDto,
                StarterKitSkillRowDto, StarterKitHotkeyRowDto, StarterKitSpawnRowDto>(sp, ct);

        return new StarterKitBundle(equipment, inventory, skills, hotkeys,
            spawn.Count > 0 ? spawn[0] : null);
    }
}
