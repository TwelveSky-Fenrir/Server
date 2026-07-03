namespace Fenrir.Data.Tests.Fixtures;

// Binds every test class decorated with [Collection("SqlServer")] to the same SqlServerFixture instance,
// so xUnit starts the container once for the whole collection instead of once per test class.
[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
