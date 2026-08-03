using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class IndexedViewSetOptionDiagnostics
{
    private const int AnsiWarnings = 8;
    private const int AnsiPadding = 16;
    private const int AnsiNulls = 32;
    private const int Arithabort = 64;
    private const int QuotedIdentifier = 256;
    private const int ConcatNullYieldsNull = 4096;
    private const int NumericRoundabort = 8192;

    public static async Task ReportAsync(SqlConnection connection)
    {
        try
        {
            await using var command = new SqlCommand(
                "SELECT @@OPTIONS, (SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID());",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return;

            var options = reader.GetInt32(0);
            var compatibilityLevel = reader.GetByte(1);

            var ansiWarningsOn = (options & AnsiWarnings) != 0;
            var arithabortEffective = (options & Arithabort) != 0 || (ansiWarningsOn && compatibilityLevel >= 90);

            var missing = new List<string>();
            if (!arithabortEffective) missing.Add("ARITHABORT");
            if (!ansiWarningsOn) missing.Add("ANSI_WARNINGS");
            if ((options & AnsiPadding) == 0) missing.Add("ANSI_PADDING");
            if ((options & AnsiNulls) == 0) missing.Add("ANSI_NULLS");
            if ((options & QuotedIdentifier) == 0) missing.Add("QUOTED_IDENTIFIER");
            if ((options & ConcatNullYieldsNull) == 0) missing.Add("CONCAT_NULL_YIELDS_NULL");
            if ((options & NumericRoundabort) != 0) missing.Add("NUMERIC_ROUNDABORT (must be OFF)");

            if (missing.Count == 0)
            {
                Console.WriteLine(
                    $"Indexed-view SET options OK (compatibility level {compatibilityLevel}) -- writes to game.OfflineShops/game.OfflineShopItems and world.ItemMall base tables will pass the SET-options check, and the optimizer may use world.vw_ItemMallCatalog / game.vw_OfflineShopListing.");
                return;
            }

            Console.WriteLine(
                $"WARNING: this connection is missing SET options required to modify tables referenced by an indexed view: {string.Join(", ", missing)} (compatibility level {compatibilityLevel}). Writes against game.OfflineShops/game.OfflineShopItems and the world.ItemMall base tables will fail with error 1934. Note ARITHABORT is implicitly ON when ANSI_WARNINGS is ON at compatibility level >= 90, so the raw @@OPTIONS bit alone is not the deciding factor.");
        }
        catch (SqlException ex)
        {
            Console.WriteLine(
                $"Could not verify indexed-view SET options (non-fatal, diagnostic only): {ex.Message}");
        }
    }
}
