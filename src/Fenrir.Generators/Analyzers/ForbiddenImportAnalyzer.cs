using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForbiddenImportAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            FenrirDiagnostics.ForbiddenNamespaceImport,
            FenrirDiagnostics.ForbiddenImportRuleMatchedNothing);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var project = ForbiddenImportRules.ForAssembly(context.Compilation.AssemblyName);
        if (project is null)
            return;

        var projectDirectory =
            ProjectPaths.ReadProjectDirectory(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);

        var populatedFolders = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        context.RegisterSyntaxTreeAction(treeContext =>
        {
            if (projectDirectory is not null)
                AnalyzeTree(treeContext, project, projectDirectory, populatedFolders);
        });

        context.RegisterCompilationEndAction(endContext =>
            ReportRulesNoLongerArmed(endContext, project, projectDirectory, populatedFolders));
    }

    private static void AnalyzeTree(
        SyntaxTreeAnalysisContext context,
        ForbiddenImportProject project,
        string projectDirectory,
        ConcurrentDictionary<string, byte> populatedFolders)
    {
        var folder = ProjectPaths.TopFolderOf(context.Tree.FilePath, projectDirectory);
        if (folder is null)
            return;

        populatedFolders[folder] = 0;

        var applicable = project.Imports
            .Where(i => string.Equals(i.FromFolder, folder, StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

        if (applicable.IsEmpty)
            return;

        if (context.Tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root)
            return;

        foreach (var usingDirective in root.Usings)
            AnalyzeUsing(context, applicable, folder, usingDirective);

        foreach (var namespaceDeclaration in root.Members.OfType<BaseNamespaceDeclarationSyntax>())
        foreach (var usingDirective in namespaceDeclaration.Usings)
            AnalyzeUsing(context, applicable, folder, usingDirective);
    }

    private static void AnalyzeUsing(
        SyntaxTreeAnalysisContext context,
        ImmutableArray<ForbiddenImport> applicable,
        string folder,
        UsingDirectiveSyntax usingDirective)
    {
        var name = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(name))
            return;

        foreach (var import in applicable)
        {
            if (!IsUnderNamespace(name!, import.NamespacePrefix))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.ForbiddenNamespaceImport,
                usingDirective.GetLocation(),
                FileNameOf(context.Tree.FilePath),
                folder,
                name,
                import.Reason));
        }
    }

    private static bool IsUnderNamespace(string candidate, string prefix)
    {
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        return candidate.Length == prefix.Length || candidate[prefix.Length] == '.';
    }

    private static void ReportRulesNoLongerArmed(
        CompilationAnalysisContext context,
        ForbiddenImportProject project,
        string? projectDirectory,
        ConcurrentDictionary<string, byte> populatedFolders)
    {
        if (projectDirectory is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.ForbiddenImportRuleMatchedNothing,
                Location.None,
                project.AssemblyName,
                "MSBuild supplied no 'build_property.ProjectDir', so no source file could be mapped to a folder"));
            return;
        }

        foreach (var folder in project.RequiredFolders)
        {
            if (populatedFolders.ContainsKey(folder))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.ForbiddenImportRuleMatchedNothing,
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
