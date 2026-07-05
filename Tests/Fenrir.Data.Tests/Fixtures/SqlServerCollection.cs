namespace Fenrir.Data.Tests.Fixtures;

// Shares one SqlServerFixture (and container) across every [Collection("SqlServer")] test class.
[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
