using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

/// <summary>
/// Traduit un chemin de fichier source en « dossier de tête » relatif au répertoire du projet
/// (<c>Handlers</c>, <c>Services</c>, ...), seule information capable de distinguer les couches
/// d'une architecture en tranches verticales repliée dans UNE SEULE assembly.
/// </summary>
/// <remarks>
/// Volontairement en manipulation de chaînes pure, sans <c>System.IO.Path</c> : les règles analyseur
/// étendues (<c>EnforceExtendedAnalyzerRules</c>, RS1035) bannissent les API d'E/S dans un analyseur, et
/// aucune résolution réelle de chemin n'est nécessaire ici — seulement un préfixe et un segment.
/// </remarks>
internal static class ProjectPaths
{
    /// <summary>Clé MSBuild rendue visible par défaut par le SDK .NET (aucun CompilerVisibleProperty requis).</summary>
    private const string ProjectDirKey = "build_property.ProjectDir";

    /// <summary>
    /// Lit <c>build_property.ProjectDir</c> et le normalise en séparateurs <c>/</c> avec un <c>/</c> final,
    /// de sorte qu'une comparaison de préfixe ne puisse pas confondre <c>.../Fenrir.Application.Game</c> avec
    /// <c>.../Fenrir.Application.GameData</c>. Renvoie <see langword="null" /> si la propriété est absente.
    /// </summary>
    public static string? ReadProjectDirectory(AnalyzerConfigOptions globalOptions)
    {
        if (!globalOptions.TryGetValue(ProjectDirKey, out var projectDir) || string.IsNullOrWhiteSpace(projectDir))
            return null;

        var normalized = projectDir.Replace('\\', '/');
        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : normalized + "/";
    }

    /// <summary>
    /// Renvoie le premier segment de <paramref name="filePath" /> sous <paramref name="normalizedProjectDir" />,
    /// ou <see langword="null" /> si le fichier est hors du projet ou posé à sa racine (donc dans aucune couche).
    /// </summary>
    public static string? TopFolderOf(string? filePath, string normalizedProjectDir)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var normalized = filePath!.Replace('\\', '/');
        if (!normalized.StartsWith(normalizedProjectDir, StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = normalized.Substring(normalizedProjectDir.Length);
        var separator = relative.IndexOf('/');

        // separator == 0 : chemin dégénéré ; separator < 0 : fichier à la racine du projet, hors couches.
        return separator <= 0 ? null : relative.Substring(0, separator);
    }
}
