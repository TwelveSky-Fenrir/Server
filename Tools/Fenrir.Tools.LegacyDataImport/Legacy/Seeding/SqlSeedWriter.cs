using System.Text;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Seeding;

/// <summary>
///     Shared helpers for writing idempotent <c>70_seed/*.sql</c> scripts in the repo's established shape:
///     a single <c>IF NOT EXISTS (...) BEGIN ... END;</c> batch containing chunked <c>INSERT ... VALUES</c>
///     statements (500 rows each -- well under SQL Server's 1000-row VALUES-list cap), no <c>GO</c> inside
///     the block.
/// </summary>
public static class SqlSeedWriter
{
    private const int MaxRowsPerInsert = 500;

    /// <summary>
    ///     NVARCHAR literal: NULL for a null/empty string (the legacy fixed-char buffers decode empty
    ///     slots as <see cref="string.Empty" />, which is not meaningfully different from "no value" here),
    ///     otherwise an <c>N'...'</c> literal with single quotes doubled.
    /// </summary>
    public static string NString(string? value)
    {
        return string.IsNullOrEmpty(value) ? "NULL" : $"N'{value.Replace("'", "''")}'";
    }

    /// <summary>Plain numeric literal, NULL for a null nullable value.</summary>
    public static string Number(int? value)
    {
        return value is null ? "NULL" : value.Value.ToString();
    }

    /// <summary>Translates the legacy "0 = no reference" sentinel to NULL for a nullable FK column.</summary>
    public static int? ZeroToNull(int value)
    {
        return value == 0 ? null : value;
    }

    /// <summary>
    ///     Appends one or more <c>INSERT INTO table (columns) VALUES (...), (...);</c> statements
    ///     covering every row in <paramref name="rows" />, chunked at <see cref="MaxRowsPerInsert" /> rows each.
    ///     No-op if <paramref name="rows" /> is empty.
    /// </summary>
    public static void WriteInserts(StringBuilder sb, string table, string columns, IReadOnlyList<string> rows)
    {
        for (var offset = 0; offset < rows.Count; offset += MaxRowsPerInsert)
        {
            var chunkCount = Math.Min(MaxRowsPerInsert, rows.Count - offset);
            sb.Append("    INSERT INTO ").Append(table).Append(" (").Append(columns).Append(") VALUES\n");
            for (var i = 0; i < chunkCount; i++)
            {
                sb.Append("    (").Append(rows[offset + i]).Append(')');
                sb.Append(i == chunkCount - 1 ? ";\n\n" : ",\n");
            }
        }
    }
}
