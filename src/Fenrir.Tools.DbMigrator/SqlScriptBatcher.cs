using System.Text;
using System.Text.RegularExpressions;

namespace Fenrir.Tools.DbMigrator;

public static partial class SqlScriptBatcher
{
    // Recognizes "GO" and "GO <n>" (the T-SQL repeat-count form -- SSMS/sqlcmd re-execute the
    // preceding batch n times). No live script in this repo uses the repeat form today, but a
    // migration/seed script authored to bulk-generate rows could reasonably want it.
    [GeneratedRegex(@"^GO(?:\s+(?<count>\d+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex GoLinePattern();

    public static IEnumerable<string> SplitBatches(string script)
    {
        var batch = new StringBuilder();
        var inBlockComment = false;
        var inLineComment = false;
        char? stringDelimiter = null;

        foreach (var rawLine in script.Replace("\r\n", "\n").Split('\n'))
        {
            inLineComment = false;
            var isGoLine = !inBlockComment && !stringDelimiter.HasValue && IsStandaloneGoLine(rawLine);

            if (isGoLine)
            {
                var match = GoLinePattern().Match(rawLine.Trim());
                var repeatCount = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value) : 1;
                var batchText = batch.ToString().Trim();
                batch.Clear();

                for (var i = 0; i < repeatCount; i++)
                    yield return batchText;

                continue;
            }

            batch.AppendLine(rawLine);
            ScanLineForCommentsAndStrings(rawLine, ref inBlockComment, ref inLineComment, ref stringDelimiter);
        }

        if (batch.Length > 0)
            yield return batch.ToString().Trim();
    }

    // A "GO" line only counts as a batch separator when it isn't inside a block comment, a string
    // literal carried over from a previous line, or (defensively) itself preceded by an unterminated
    // line comment on the same physical line -- e.g. "-- GO" must never split a batch.
    private static bool IsStandaloneGoLine(string rawLine)
    {
        var trimmed = rawLine.TrimStart();
        if (trimmed.StartsWith("--", StringComparison.Ordinal))
            return false;

        return GoLinePattern().IsMatch(rawLine.Trim());
    }

    // Cheap same-line state tracker: only needs to know whether a block comment or string literal is
    // still open when the NEXT line begins, so a "GO" appearing on a continuation line of either is
    // correctly ignored. Does not need to be a full T-SQL parser -- it only has to protect the one
    // decision IsStandaloneGoLine makes.
    private static void ScanLineForCommentsAndStrings(string line, ref bool inBlockComment, ref bool inLineComment,
        ref char? stringDelimiter)
    {
        var i = 0;
        while (i < line.Length)
        {
            if (inLineComment)
                return;

            if (inBlockComment)
            {
                var close = line.IndexOf("*/", i, StringComparison.Ordinal);
                if (close == -1) return;
                inBlockComment = false;
                i = close + 2;
                continue;
            }

            if (stringDelimiter is { } delim)
            {
                var close = line.IndexOf(delim, i);
                if (close == -1) return;
                if (close + 1 < line.Length && line[close + 1] == delim)
                {
                    i = close + 2;
                    continue;
                }

                stringDelimiter = null;
                i = close + 1;
                continue;
            }

            if (i + 1 < line.Length && line[i] == '-' && line[i + 1] == '-')
            {
                inLineComment = true;
                return;
            }

            if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
            {
                inBlockComment = true;
                i += 2;
                continue;
            }

            if (line[i] == '\'')
            {
                stringDelimiter = '\'';
                i++;
                continue;
            }

            i++;
        }
    }
}
