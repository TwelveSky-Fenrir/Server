namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Boot-time, one-shot loader for <see cref="TribeConversionResolver" /> -- the missing wiring step that
///     left world.usp_TribeConversionCatalog_GetAll's equivalence data unconsumed anywhere in Fenrir (see
///     <see cref="TribeConversionResolver" />'s own remarks: it existed with a schema, seed data, and a
///     repository read, but no boot-time construction site and no DI registration, until this type). Mirrors
///     <c>Fenrir.Application.Game.GameData.WorldDataLoader</c>'s own shape exactly: an empty singleton
///     registered at DI-container-build time, populated once via an explicit <c>Fenrir.GameServer</c>
///     <c>Program.cs</c> boot step before <c>host.RunAsync()</c>, with every real consumer resolving the
///     already-built <see cref="TribeConversionResolver" /> itself through a deferred singleton factory (see
///     <c>Fenrir.Application.Game.Domain.Extensions.DomainServiceCollectionExtensions</c>'s own
///     "services.AddSingleton(static provider =&gt; provider.GetRequiredService&lt;TribeConversionCatalogLoader&gt;().Resolver)"
///     registration) rather than depending on this loader directly.
/// </summary>
public sealed class TribeConversionCatalogLoader
{
    private TribeConversionResolver? _resolver;

    /// <summary>
    ///     Throws until <see cref="InitializeAsync" /> has completed -- resolving early is a Program.cs wiring
    ///     bug, not a state to limp through (same posture as <c>WorldDataLoader.Cache</c>).
    /// </summary>
    public TribeConversionResolver Resolver => _resolver ?? throw new InvalidOperationException(
        "TribeConversionResolver is not loaded yet -- call TribeConversionCatalogLoader.InitializeAsync before accepting connections.");

    /// <summary>Loads world.usp_TribeConversionCatalog_GetAll and builds the resolver. One-shot.</summary>
    public async Task InitializeAsync(IWorldDataRepository repository, CancellationToken ct)
    {
        if (_resolver is not null)
            throw new InvalidOperationException(
                "TribeConversionCatalogLoader.InitializeAsync must only be called once, at boot.");

        var (skillEquivalences, itemEquivalences, costumeEquivalences) =
            await repository.GetTribeConversionCatalogAsync(ct);

        _resolver = new TribeConversionResolver(skillEquivalences, itemEquivalences, costumeEquivalences);
    }
}
