namespace Fenrir.IntegrationTests.Fixtures;

// Shares one FenrirEnvironmentFixture (container + both live server processes) across every
// [Collection("FenrirEnvironment")] test class -- booting it is heavyweight enough that it must happen once.
[CollectionDefinition("FenrirEnvironment")]
public sealed class FenrirEnvironmentCollection : ICollectionFixture<FenrirEnvironmentFixture>
{
}
