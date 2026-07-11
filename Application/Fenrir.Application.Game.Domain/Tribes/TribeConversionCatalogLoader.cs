namespace Fenrir.Application.Game.Domain.Tribes;

public sealed class TribeConversionCatalogLoader
{
    private TribeConversionResolver? _resolver;

        public TribeConversionResolver Resolver => _resolver ?? throw new InvalidOperationException(
        "TribeConversionResolver is not loaded yet -- call TribeConversionCatalogLoader.InitializeAsync before accepting connections.");

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
