using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Fenrir.Generators.Analyzers;

internal sealed class ForbiddenImport
{
    public ForbiddenImport(string fromFolder, string namespacePrefix, string reason)
    {
        FromFolder = fromFolder;
        NamespacePrefix = namespacePrefix;
        Reason = reason;
    }

    public string FromFolder { get; }

    public string NamespacePrefix { get; }

    public string Reason { get; }
}

internal sealed class ForbiddenImportProject
{
    public ForbiddenImportProject(string assemblyName, ImmutableArray<ForbiddenImport> imports)
    {
        AssemblyName = assemblyName;
        Imports = imports;

        var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var import in imports)
            folders.Add(import.FromFolder);

        RequiredFolders = folders.ToImmutableArray();
    }

    public string AssemblyName { get; }

    public ImmutableArray<ForbiddenImport> Imports { get; }

    public ImmutableArray<string> RequiredFolders { get; }
}

internal static class ForbiddenImportRules
{
    private const string Domain = "Domain";

    private static readonly ImmutableArray<ForbiddenImport> GameCoreImports = ImmutableArray.Create(
        new ForbiddenImport(Domain, "Fenrir.Network",
            "les regles de jeu ne construisent pas de trames et ne connaissent pas de session concrete ; " +
            "passer par IPacketSession (Fenrir.Core.Abstractions) et FrameWriter (Fenrir.Core.Wire)"),
        new ForbiddenImport(Domain, "Fenrir.Protocol.Login",
            "Login et Game sont deux protocoles distincts dont les opcodes se recouvrent ; une regle de jeu " +
            "qui compile un paquet Login peut en construire un par accident et le mettre sur le fil"));

    private static readonly ImmutableArray<ForbiddenImportProject> Projects = ImmutableArray.Create(
        new ForbiddenImportProject("Fenrir.Application.Game", GameCoreImports));

    public static ForbiddenImportProject? ForAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return null;

        foreach (var project in Projects)
            if (string.Equals(project.AssemblyName, assemblyName, StringComparison.Ordinal))
                return project;

        return null;
    }
}
