using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

internal static class ProjectPaths
{

        private const string ProjectDirKey = "build_property.ProjectDir";

        public static string? ReadProjectDirectory(AnalyzerConfigOptions globalOptions)
    {
        if (!globalOptions.TryGetValue(ProjectDirKey, out var projectDir) || string.IsNullOrWhiteSpace(projectDir))
            return null;

        var normalized = projectDir.Replace('\\', '/');
        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : normalized + "/";
    }

        public static string? TopFolderOf(string? filePath, string normalizedProjectDir)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var normalized = filePath!.Replace('\\', '/');
        if (!normalized.StartsWith(normalizedProjectDir, StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = normalized.Substring(normalizedProjectDir.Length);
        var separator = relative.IndexOf('/');

        return separator <= 0 ? null : relative.Substring(0, separator);
    }
}
