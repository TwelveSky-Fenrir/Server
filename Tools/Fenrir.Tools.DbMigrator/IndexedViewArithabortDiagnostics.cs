using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class IndexedViewArithabortDiagnostics
{
    public static async Task ReportAsync(SqlConnection connection)
    {
        // world.vw_ItemMallCatalog / game.vw_OfflineShopListing are WITH SCHEMABINDING indexed views. The query
        // optimizer only substitutes an indexed view's materialized index for a session with ARITHABORT
        // effectively ON (Microsoft Learn, "Create indexed views" -- required SET options table).
        // Microsoft.Data.SqlClient/CaeriusNet negotiates OLE DB/ODBC-style session defaults, which set
        // ANSI_WARNINGS ON but leave the explicit ARITHABORT bit OFF. Per Microsoft Learn's "SET ARITHABORT
        // (Transact-SQL)" Remarks, though: "When ANSI_WARNINGS has a value of ON and the database compatibility
        // level is set to 90 or higher then ARITHABORT is implicitly ON regardless of its value setting." Fenrir
        // never sets an explicit COMPATIBILITY_LEVEL override anywhere under Database/, so this database inherits
        // the SQL Server 2025 instance's own native compat level (well above the 90 floor) -- meaning every
        // CaeriusNet connection should already satisfy the requirement without any server-wide sp_configure
        // change. This check reports the actual, observed values from the exact driver/connection type CaeriusNet
        // itself uses, on every real db-migrator run, instead of leaving that "should" as a one-off manual step
        // nobody ever gets around to running against a live instance. A wrong ARITHABORT setting on a READING
        // connection only degrades performance (the optimizer silently falls back to the base tables) -- but per
        // Microsoft Learn ("SET ARITHABORT (Transact-SQL)"), ARITHABORT OFF on a WRITING connection makes any
        // INSERT/UPDATE/DELETE against game.OfflineShops/game.OfflineShopItems (actively written by live gameplay
        // procedures) fail outright with an error, not merely degrade. This check itself only ever reports a
        // diagnostic, never fails the migration, since it cannot distinguish read-only from write-capable
        // connections from here.
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
