using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Fenrir.Application.Game.Services.Commerce;
using Microsoft.Data.SqlClient;

namespace Fenrir.Application.Game.Tests.Commerce;

[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "Test-only helper, never published/trimmed -- reflects into SqlClient's own internal " +
                    "SqlError/SqlErrorCollection/SqlException.CreateException plumbing to build a real SqlException.")]
[UnconditionalSuppressMessage("Trimming", "IL2072",
    Justification = "Same as IL2026 above -- SqlClient ships these internal types undecorated, this test " +
                    "helper cannot add DynamicallyAccessedMembers annotations to a third-party assembly.")]
[UnconditionalSuppressMessage("Trimming", "IL2075",
    Justification = "Same as IL2026 above -- SqlClient ships these internal types undecorated, this test " +
                    "helper cannot add DynamicallyAccessedMembers annotations to a third-party assembly.")]
public class ProxyShopDeputyFailureClassifierTests
{
    [Fact]
    public void StaleListingSqlErrorNumber_MatchesEstablishedProxyListingStaleErrorNumber()
    {
        Assert.Equal(50272, ProxyShopDeputyFailureClassifier.StaleListingSqlErrorNumber);
    }

    [Fact]
    public void IsStaleListingFailure_MatchingSqlErrorNumber_ReturnsTrue()
    {
        var ex = BuildSqlException(ProxyShopDeputyFailureClassifier.StaleListingSqlErrorNumber);

        Assert.True(ProxyShopDeputyFailureClassifier.IsStaleListingFailure(ex));
    }

    [Fact]
    public void IsStaleListingFailure_DifferentSqlErrorNumber_ReturnsFalse()
    {
        var ex = BuildSqlException(50273);

        Assert.False(ProxyShopDeputyFailureClassifier.IsStaleListingFailure(ex));
    }

    [Fact]
    public void IsStaleListingFailure_NonSqlException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("not a SQL error");

        Assert.False(ProxyShopDeputyFailureClassifier.IsStaleListingFailure(ex));
    }

    private static SqlException BuildSqlException(int number, string message = "Simulated SQL error")
    {
        var sqlClientAssembly = typeof(SqlException).Assembly;
        var sqlErrorType = sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlError", true)!;
        var sqlErrorCollectionType =
            sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlErrorCollection", true)!;

        var errorCtor = sqlErrorType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            [
                typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string),
                typeof(int), typeof(Exception)
            ])!;
        var sqlError = errorCtor.Invoke([
            number, (byte)1, (byte)16, "fenrir-test-server", message,
            "usp_OfflineShop_ExecutePurchase", 0, null
        ]);

        var errors = Activator.CreateInstance(sqlErrorCollectionType, true)!;
        var addMethod = sqlErrorCollectionType.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!;
        addMethod.Invoke(errors, [sqlError]);

        var createException = typeof(SqlException).GetMethod("CreateException",
            BindingFlags.NonPublic | BindingFlags.Static,
            [sqlErrorCollectionType, typeof(string)])!;

        return (SqlException)createException.Invoke(null, [errors, "7.0"])!;
    }
}
