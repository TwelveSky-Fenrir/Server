using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClosureAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(FenrirDiagnostics.ForbiddenAssemblyInClosure);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(OnCompilation);
    }

    private static void OnCompilation(CompilationAnalysisContext context)
    {
        var assemblyName = context.Compilation.AssemblyName;
        var entries = ClosureRules.ForAssembly(assemblyName);
        if (entries.IsEmpty)
            return;

        var resolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var referenced in context.Compilation.SourceModule.ReferencedAssemblySymbols)
            resolved.Add(referenced.Name);

        foreach (var entry in entries)
        {
            if (!resolved.Contains(entry.ForbiddenAssembly))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.ForbiddenAssemblyInClosure,
                Location.None,
                assemblyName,
                entry.ForbiddenAssembly,
                entry.Reason));
        }
    }
}
