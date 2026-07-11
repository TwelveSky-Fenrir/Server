using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Fenrir.Application.Game.Services.Commerce;
using Microsoft.Data.SqlClient;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     C21a -- <see cref="ProxyShopDeputyFailureClassifier" /> coverage. Builds real
///     <see cref="SqlException" /> instances via reflection into SqlClient's own internal
///     <c>SqlError</c>/<c>SqlErrorCollection</c>/<c>SqlException.CreateException</c> plumbing (mirroring
///     <c>Fenrir.Application.Login.Tests.TestSupport.SqlExceptionTestFactory</c>'s established approach in the
///     sibling Login test project) since <see cref="SqlException" /> has no public constructor and is only
///     ever created by SqlClient itself from a real server round trip.
/// </summary>
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
        // BuyShopItemService.ProxyListingStaleErrorNumber (private) is 50272 for the identical stored
        // procedure -- pinned here as a literal so a future divergence between the two constants is caught
        // by this test rather than silently drifting.
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
        // 50273 is the sibling ProxyBigMoneyCapExceededErrorNumber from the same stored-procedure family --
        // a different, non-stale condition that must NOT be misclassified as stale.
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
