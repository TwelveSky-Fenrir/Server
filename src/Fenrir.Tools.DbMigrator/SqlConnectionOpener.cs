using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class SqlConnectionOpener
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task<bool> TryOpenAsync(SqlConnection connection)
    {
        for (var attempt = 1;; attempt++)
            try
            {
                await connection.OpenAsync();
                return true;
            }
            catch (SqlException ex) when (attempt < MaxAttempts)
            {
                Console.WriteLine(
                    $"Connection attempt {attempt}/{MaxAttempts} failed: {ex.Message}. Retrying in {RetryDelay.TotalSeconds:0}s...");
                await Task.Delay(RetryDelay);
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"Could not connect after {MaxAttempts} attempts: {ex.Message}");
                return false;
            }
    }
}
