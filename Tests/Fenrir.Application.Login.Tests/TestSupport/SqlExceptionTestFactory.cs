using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     Builds a real <see cref="SqlException" /> carrying a caller-chosen <see cref="SqlException.Number" />, for
///     tests that exercise a <c>catch (SqlException ex) when (ex.Number == ...)</c> branch (e.g.
///     ClaimGiftService's translation of usp_Gift_ClaimIntoVault's THROWn 50220/50274). <see cref="SqlException" />
///     has no public constructor and is only ever created by SqlClient itself from a real server round trip, so
///     this reflects into SqlClient's own internal <c>SqlError</c>/<c>SqlErrorCollection</c>/
///     <c>SqlException.CreateException</c> plumbing rather than fabricating an unrelated fake type -- the
///     resulting exception is a genuine <see cref="SqlException" /> instance, just built without a live server.
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
internal static class SqlExceptionTestFactory
{
    public static SqlException WithNumber(int number, string message = "Simulated SQL error")
    {
        var sqlClientAssembly = typeof(SqlException).Assembly;
        var sqlErrorType = sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlError", throwOnError: true)!;
        var sqlErrorCollectionType =
            sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlErrorCollection", throwOnError: true)!;

        var errorCtor = sqlErrorType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            [
                typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string),
                typeof(int), typeof(Exception)
            ])!;
        var sqlError = errorCtor.Invoke([number, (byte)1, (byte)16, "fenrir-test-server", message,
            "usp_Gift_ClaimIntoVault", 0, null]);

        var errors = Activator.CreateInstance(sqlErrorCollectionType, nonPublic: true)!;
        var addMethod = sqlErrorCollectionType.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!;
        addMethod.Invoke(errors, [sqlError]);

        var createException = typeof(SqlException).GetMethod("CreateException",
            BindingFlags.NonPublic | BindingFlags.Static,
            [sqlErrorCollectionType, typeof(string)])!;

        return (SqlException)createException.Invoke(null, [errors, "7.0"])!;
    }
}
