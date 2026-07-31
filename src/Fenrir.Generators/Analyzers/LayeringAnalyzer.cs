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

        populatedFolders[folder] = 0;

        if (!project.HasEdgesFrom(folder))
            return;

        if (syntaxTree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root)
            return;

        foreach (var usingDirective in root.Usings)
            AnalyzeUsing(context, project, projectDirectory, sourceAssembly, folder, usingDirective);

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

        private static HashSet<string> ResolveDeclaringFolders(
        ISymbol? target,
        IAssemblySymbol sourceAssembly,
        string projectDirectory)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        switch (target)
        {
            case INamespaceSymbol namespaceSymbol:
                if (!ContributesToSourceAssembly(namespaceSymbol, sourceAssembly))
                    break;

                foreach (var type in namespaceSymbol.GetTypeMembers())
                    AddDeclaringFolders(type, sourceAssembly, projectDirectory, folders);

                break;

            case INamedTypeSymbol typeSymbol:
                AddDeclaringFolders(typeSymbol, sourceAssembly, projectDirectory, folders);
                break;
        }

        return folders;
    }

    private static bool ContributesToSourceAssembly(INamespaceSymbol namespaceSymbol, IAssemblySymbol sourceAssembly)
    {
        var constituents = namespaceSymbol.ConstituentNamespaces;

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
