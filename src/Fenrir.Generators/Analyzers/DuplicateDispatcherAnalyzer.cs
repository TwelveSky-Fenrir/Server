using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

// Ferme l'angle mort documente de FEN104. Celui-ci ne rapporte que sur les types declares dans la
// compilation courante : deux assemblies SOEURS qui emettent chacune le dispatcher d'un meme serveur, sans
// se referencer l'une l'autre, lui sont mutuellement invisibles. Seul l'executable qui les COMPOSE les voit
// toutes les deux — d'ou une regle qui compte les assemblies REFERENCEES exposant le type.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateDispatcherAnalyzer : DiagnosticAnalyzer
{
    private static readonly FenrirServer[] AllServers =
        [FenrirServer.Login, FenrirServer.Zone, FenrirServer.Center];

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

            emitters.Sort(System.StringComparer.Ordinal);

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
