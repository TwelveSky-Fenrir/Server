using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class IndexedViewArithabortDiagnostics
{
    public static async Task ReportAsync(SqlConnection connection)
    {
        try
        {
            await using var command = new SqlCommand(
                "SELECT CASE WHEN (64 & @@OPTIONS) = 64 THEN 1 ELSE 0 END, " +
                "(SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID());", connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return;

            var arithabortOn = reader.GetInt32(0) == 1;
            var compatibilityLevel = reader.GetByte(1);

            Console.WriteLine(arithabortOn
                ? $"ARITHABORT effectively ON (compatibility level {compatibilityLevel}) -- world.vw_ItemMallCatalog / game.vw_OfflineShopListing's indexes are eligible for optimizer use on this connection type."
                : $"WARNING: ARITHABORT effectively OFF (compatibility level {compatibilityLevel}). For read-only queries this only degrades performance (the optimizer falls back to querying world.vw_ItemMallCatalog / game.vw_OfflineShopListing's base tables directly). For writes against game.OfflineShops/game.OfflineShopItems (actively written by live gameplay procedures), this setting makes the write fail outright with an error instead. Expected ON via the OLE DB/ODBC ANSI_WARNINGS default plus compatibility level >= 90; check whether COMPATIBILITY_LEVEL was explicitly lowered below 90.");
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"Could not verify ARITHABORT/compatibility level (non-fatal, diagnostic only): {ex.Message}");
        }
    }
}
