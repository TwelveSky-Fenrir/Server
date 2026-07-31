using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analyzers;

/// <summary>Une flèche de dépendance interdite entre deux dossiers de tête d'un même projet.</summary>
internal sealed class LayerEdge
{
    public LayerEdge(string fromFolder, string toFolder, DiagnosticDescriptor descriptor)
    {
        FromFolder = fromFolder;
        ToFolder = toFolder;
        Descriptor = descriptor;
    }

    public string FromFolder { get; }

    public string ToFolder { get; }

    public DiagnosticDescriptor Descriptor { get; }
}

/// <summary>Le jeu de règles de couche appliqué à une assembly donnée.</summary>
internal sealed class LayeredProject
{
    public LayeredProject(string assemblyName, ImmutableArray<LayerEdge> edges)
    {
        AssemblyName = assemblyName;
        Edges = edges;

        // Les dossiers surveillés par FEN113 sont DÉDUITS des flèches, jamais listés à la main : ajouter une
        // flèche étend automatiquement le garde-fou, si bien que la liste ne peut pas dériver du jeu de règles.
        var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            folders.Add(edge.FromFolder);
            folders.Add(edge.ToFolder);
        }

        RequiredFolders = folders.ToImmutableArray();
    }

    public string AssemblyName { get; }

    public ImmutableArray<LayerEdge> Edges { get; }

    /// <summary>Dossiers qui DOIVENT exister pour que les règles pilotées par chemin soient effectivement armées.</summary>
    public ImmutableArray<string> RequiredFolders { get; }

    public bool HasEdgesFrom(string folder)
    {
        foreach (var edge in Edges)
            if (string.Equals(edge.FromFolder, folder, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}

/// <summary>
/// Table des projets soumis aux règles de couche pilotées par chemin.
/// </summary>
/// <remarks>
/// Codée en dur plutôt que configurée : le dépôt n'a pas de projet d'analyseurs séparé et le périmètre
/// d'écriture des règles est ce projet-ci. Une entrée en trop (assembly renommée, dossier déplacé) ne dégrade
/// pas silencieusement — <see cref="FenrirDiagnostics.LayerRuleMatchedNothing" /> (FEN113) casse le build.
/// </remarks>
internal static class LayerRules
{
    private const string Abstractions = "Abstractions";
    private const string Services = "Services";
    private const string Handlers = "Handlers";
    private const string Hosting = "Hosting";

    private static readonly ImmutableArray<LayerEdge> ApplicationEdges = ImmutableArray.Create(
        // FEN111 : un handler passe par Abstractions/, jamais par l'implémentation concrète de Services/.
        new LayerEdge(Handlers, Services, FenrirDiagnostics.HandlerDependsOnServiceImplementation),
        // FEN112 : Hosting/ est la racine de composition — flèche sortante uniquement.
        new LayerEdge(Abstractions, Hosting, FenrirDiagnostics.LayerDependsOnHosting),
        new LayerEdge(Services, Hosting, FenrirDiagnostics.LayerDependsOnHosting),
        new LayerEdge(Handlers, Hosting, FenrirDiagnostics.LayerDependsOnHosting));

    private static readonly ImmutableArray<LayeredProject> Projects = ImmutableArray.Create(
        new LayeredProject("Fenrir.Application.Game", ApplicationEdges),
        new LayeredProject("Fenrir.Application.Login", ApplicationEdges));

    public static LayeredProject? ForAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return null;

        foreach (var project in Projects)
            if (string.Equals(project.AssemblyName, assemblyName, StringComparison.Ordinal))
                return project;

        return null;
    }
}
