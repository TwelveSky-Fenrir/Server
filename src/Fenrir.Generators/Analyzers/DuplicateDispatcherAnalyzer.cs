using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateDispatcherAnalyzer : DiagnosticAnalyzer
{
    private static readonly FenrirServer[] AllServers =
        [FenrirServer.Login, FenrirServer.Zone];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(FenrirDiagnostics.DuplicateDispatcherInClosure);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(OnCompilation);
    }

    private static void OnCompilation(CompilationAnalysisContext context)
    {
        var assemblyName = context.Compilation.AssemblyName ?? "(unknown)";

        foreach (var server in AllServers)
        {
            var dispatcherName = EmittedNames.MessageDispatcher(server);
            var emitters = new List<string>();

            foreach (var referenced in context.Compilation.SourceModule.ReferencedAssemblySymbols)
                if (referenced.TypeNames.Contains(dispatcherName))
                    emitters.Add(referenced.Name);

            if (emitters.Count < 2)
                continue;

            emitters.Sort(StringComparer.Ordinal);

            for (var i = 1; i < emitters.Count; i++)
                context.ReportDiagnostic(Diagnostic.Create(
                    FenrirDiagnostics.DuplicateDispatcherInClosure,
                    Location.None,
                    assemblyName,
                    emitters[0],
                    emitters[i],
                    dispatcherName));
        }
    }
}
