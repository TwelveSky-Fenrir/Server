using System.Text;
using System.Text.RegularExpressions;

namespace Fenrir.Tools.DbMigrator;

public static partial class SqlScriptBatcher
{
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

    private static bool IsStandaloneGoLine(string rawLine)
    {
        var trimmed = rawLine.TrimStart();
        if (trimmed.StartsWith("--", StringComparison.Ordinal))
            return false;

        return GoLinePattern().IsMatch(rawLine.Trim());
    }

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
