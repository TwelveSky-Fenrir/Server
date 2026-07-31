using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analyzers;

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

internal sealed class LayeredProject
{
    public LayeredProject(string assemblyName, ImmutableArray<LayerEdge> edges)
    {
        AssemblyName = assemblyName;
        Edges = edges;

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

    public ImmutableArray<string> RequiredFolders { get; }

    public bool HasEdgesFrom(string folder)
    {
        foreach (var edge in Edges)
            if (string.Equals(edge.FromFolder, folder, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}

internal static class LayerRules
{
    private const string Abstractions = "Abstractions";
    private const string Services = "Services";
    private const string Handlers = "Handlers";
    private const string Hosting = "Hosting";

    private static readonly ImmutableArray<LayerEdge> LoginEdges = ImmutableArray.Create(
        new LayerEdge(Handlers, Services, FenrirDiagnostics.HandlerDependsOnServiceImplementation),
        new LayerEdge(Abstractions, Hosting, FenrirDiagnostics.LayerDependsOnHosting),
        new LayerEdge(Services, Hosting, FenrirDiagnostics.LayerDependsOnHosting),
        new LayerEdge(Handlers, Hosting, FenrirDiagnostics.LayerDependsOnHosting));

    private static readonly ImmutableArray<LayeredProject> Projects = ImmutableArray.Create(
        new LayeredProject("Fenrir.Application.Login", LoginEdges));

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
