using System.Text;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Seeding;

public static class SqlSeedWriter
{
    private const int MaxRowsPerInsert = 500;

    public static string NString(string? value)
    {
        return string.IsNullOrEmpty(value) ? "NULL" : $"N'{value.Replace("'", "''")}'";
    }

    public static string Number(int? value)
    {
        return value is null ? "NULL" : value.Value.ToString();
    }

    public static int? ZeroToNull(int value)
    {
        return value == 0 ? null : value;
    }

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
