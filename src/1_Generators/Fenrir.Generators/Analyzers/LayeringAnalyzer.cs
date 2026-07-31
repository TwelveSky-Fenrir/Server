using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

/// <summary>
/// FEN111 / FEN112 — règles de couche pilotées par le CHEMIN des fichiers, plus FEN113, le garde-fou qui
/// vérifie que ces règles sont encore armées.
/// </summary>
/// <remarks>
/// <para>
/// Le repli des tranches verticales dans une seule assembly met <c>Domain/</c>, <c>Services/</c>,
/// <c>Handlers/</c> et <c>Hosting/</c> côte à côte dans <c>Fenrir.Application.Game</c>. Aucune analyse fondée
/// sur les assemblies ne peut donc encore distinguer les couches : le chemin du fichier est la SEULE source
/// d'information restante, d'où <c>build_property.ProjectDir</c>.
/// </para>
/// <para>
/// Les deux extrémités de chaque flèche sont résolues par chemin : la couche d'origine vient du fichier
/// analysé, la couche cible vient du fichier où le type importé est DÉCLARÉ. Le nom de l'espace de noms n'est
/// jamais interprété — un dossier renommé sans renommer l'espace de noms (ou l'inverse) ne peut donc pas
/// donner un faux verdict silencieux.
/// </para>
/// <para>
/// FEN113 est le point le plus important de ce fichier. Une règle pilotée par chemin qui ne matche plus rien
/// reste à « 0 violation » exactement comme une règle respectée : un <c>git mv Handlers/ Dispatchers/</c>
/// décrocherait le filet sans que personne ne le voie. L'action de fin de compilation transforme cette panne
/// muette en échec de build.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LayeringAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            FenrirDiagnostics.HandlerDependsOnServiceImplementation,
            FenrirDiagnostics.LayerDependsOnHosting,
            FenrirDiagnostics.LayerRuleMatchedNothing);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var project = LayerRules.ForAssembly(context.Compilation.AssemblyName);
        if (project is null)
            return;

        var projectDirectory =
            ProjectPaths.ReadProjectDirectory(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
        var sourceAssembly = context.Compilation.Assembly;

        // Couches effectivement peuplées par un fichier analysé. ConcurrentDictionary parce que les actions
        // par fichier tournent en parallèle (EnableConcurrentExecution) et que l'action de fin les relit.
        var populatedFolders = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        context.RegisterSemanticModelAction(semanticModelContext =>
        {
            if (projectDirectory is not null)
                AnalyzeFile(semanticModelContext, project, projectDirectory, sourceAssembly, populatedFolders);
        });

        context.RegisterCompilationEndAction(endContext =>
            ReportRulesNoLongerArmed(endContext, project, projectDirectory, populatedFolders));
    }

    private static void AnalyzeFile(
        SemanticModelAnalysisContext context,
        LayeredProject project,
        string projectDirectory,
        IAssemblySymbol sourceAssembly,
        ConcurrentDictionary<string, byte> populatedFolders)
    {
        var syntaxTree = context.SemanticModel.SyntaxTree;

        var folder = ProjectPaths.TopFolderOf(syntaxTree.FilePath, projectDirectory);
        if (folder is null)
            return;

        // Signale à FEN113 que cette couche existe encore, même si le fichier ne porte aucune règle sortante.
        populatedFolders[folder] = 0;

        if (!project.HasEdgesFrom(folder))
            return;

        if (syntaxTree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root)
            return;

        foreach (var usingDirective in root.Usings)
            AnalyzeUsing(context, project, projectDirectory, sourceAssembly, folder, usingDirective);

        // Avec un espace de noms à portée de fichier, un `using` placé APRÈS `namespace X;` appartient à la
        // déclaration d'espace de noms et non à l'unité de compilation : il serait sinon invisible ici.
        foreach (var namespaceDeclaration in root.Members.OfType<BaseNamespaceDeclarationSyntax>())
        foreach (var usingDirective in namespaceDeclaration.Usings)
            AnalyzeUsing(context, project, projectDirectory, sourceAssembly, folder, usingDirective);
    }

    private static void AnalyzeUsing(
        SemanticModelAnalysisContext context,
        LayeredProject project,
        string projectDirectory,
        IAssemblySymbol sourceAssembly,
        string fromFolder,
        UsingDirectiveSyntax usingDirective)
    {
        // Null pour un alias qui ne vise pas un nom (`using Bytes = byte[];`) : rien à résoudre.
        var name = usingDirective.Name;
        if (name is null)
            return;

        var target = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
        var targetFolders = ResolveDeclaringFolders(target, sourceAssembly, projectDirectory);
        if (targetFolders.Count == 0)
            return;

        foreach (var edge in project.Edges)
        {
            if (!string.Equals(edge.FromFolder, fromFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!targetFolders.Contains(edge.ToFolder))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                edge.Descriptor,
                usingDirective.GetLocation(),
                FileNameOf(context.SemanticModel.SyntaxTree.FilePath),
                fromFolder,
                name.ToString()));
        }
    }

    /// <summary>
    /// Renvoie les dossiers de tête dans lesquels la cible d'un <c>using</c> est effectivement DÉCLARÉE,
    /// en ne considérant que les types issus du code source de cette assembly.
    /// </summary>
    private static HashSet<string> ResolveDeclaringFolders(
        ISymbol? target,
        IAssemblySymbol sourceAssembly,
        string projectDirectory)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        switch (target)
        {
            case INamespaceSymbol namespaceSymbol:
                // Court-circuite `using System;` & co. sans énumérer des milliers de types de métadonnées.
                if (!ContributesToSourceAssembly(namespaceSymbol, sourceAssembly))
                    break;

                foreach (var type in namespaceSymbol.GetTypeMembers())
                    AddDeclaringFolders(type, sourceAssembly, projectDirectory, folders);

                break;

            // `using static X.Y.Z;` et `using Alias = X.Y.Z;` visent un type, pas un espace de noms.
            case INamedTypeSymbol typeSymbol:
                AddDeclaringFolders(typeSymbol, sourceAssembly, projectDirectory, folders);
                break;
        }

        return folders;
    }

    private static bool ContributesToSourceAssembly(INamespaceSymbol namespaceSymbol, IAssemblySymbol sourceAssembly)
    {
        var constituents = namespaceSymbol.ConstituentNamespaces;

        // Cas inattendu : ne PAS éteindre la règle, payer plutôt l'énumération des types.
        if (constituents.IsDefaultOrEmpty)
            return true;

        foreach (var constituent in constituents)
            if (SymbolEqualityComparer.Default.Equals(constituent.ContainingAssembly, sourceAssembly))
                return true;

        return false;
    }

    private static void AddDeclaringFolders(
        INamedTypeSymbol type,
        IAssemblySymbol sourceAssembly,
        string projectDirectory,
        HashSet<string> folders)
    {
        if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, sourceAssembly))
            return;

        foreach (var declaration in type.DeclaringSyntaxReferences)
        {
            var folder = ProjectPaths.TopFolderOf(declaration.SyntaxTree.FilePath, projectDirectory);
            if (folder is not null)
                folders.Add(folder);
        }
    }

    /// <summary>FEN113 : vérifie que chaque dossier cité par une flèche porte encore au moins un fichier.</summary>
    private static void ReportRulesNoLongerArmed(
        CompilationAnalysisContext context,
        LayeredProject project,
        string? projectDirectory,
        ConcurrentDictionary<string, byte> populatedFolders)
    {
        if (projectDirectory is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.LayerRuleMatchedNothing,
                Location.None,
                project.AssemblyName,
                "MSBuild supplied no 'build_property.ProjectDir', so no source file could be mapped to a layer " +
                "folder at all"));
            return;
        }

        foreach (var folder in project.RequiredFolders)
        {
            if (populatedFolders.ContainsKey(folder))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.LayerRuleMatchedNothing,
                Location.None,
                project.AssemblyName,
                "no analysed source file lives under '" + folder + "/', so every rule naming that folder now " +
                "matches nothing and reports 0 violations for the wrong reason"));
        }
    }

    private static string FileNameOf(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "(unknown file)";

        var normalized = filePath!.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');

        return separator < 0 ? normalized : normalized.Substring(separator + 1);
    }
}
