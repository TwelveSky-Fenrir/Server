using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DispatchOwnershipAnalyzer : DiagnosticAnalyzer
{
    private static readonly FenrirServer[] AllServers =
        [FenrirServer.Login, FenrirServer.Zone, FenrirServer.Center];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(FenrirDiagnostics.DispatchOwnershipConflict);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var alreadyDispatched = FindDispatchersInReferencedAssemblies(context.Compilation);

        if (alreadyDispatched.Count == 0)
            return;

        var assemblyName = context.Compilation.AssemblyName ?? "(unknown)";

        context.RegisterSymbolAction(
            symbolContext => AnalyzeHandlerType(symbolContext, assemblyName, alreadyDispatched),
            SymbolKind.NamedType);
    }

    private static ImmutableDictionary<FenrirServer, string> FindDispatchersInReferencedAssemblies(
        Compilation compilation)
    {
        var builder = ImmutableDictionary.CreateBuilder<FenrirServer, string>();

        foreach (var server in AllServers)
        {
            var dispatcherName = EmittedNames.MessageDispatcher(server);

            foreach (var referenced in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (!referenced.TypeNames.Contains(dispatcherName))
                    continue;

                builder[server] = referenced.Name;
                break;
            }
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeHandlerType(
        SymbolAnalysisContext context,
        string assemblyName,
        ImmutableDictionary<FenrirServer, string> alreadyDispatched)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol || typeSymbol.IsAbstract ||
            typeSymbol.TypeKind == TypeKind.Interface)
            return;

        foreach (var candidateInterface in typeSymbol.AllInterfaces)
        {
            if (candidateInterface.TypeArguments.Length != 1)
                continue;

            if (!SymbolNameHelpers.IsClosedGenericOf(candidateInterface, WellKnownNames.IInlinePacketHandler) &&
                !SymbolNameHelpers.IsClosedGenericOf(candidateInterface, WellKnownNames.IAsyncPacketHandler))
                continue;

            if (candidateInterface.TypeArguments[0] is not INamedTypeSymbol packetType)
                continue;

            var packetAttribute = packetType.GetAttributes().Find(WellKnownNames.FenrirPacketAttribute);
            if (packetAttribute is null)
                continue;

            var server = (FenrirServer)packetAttribute.GetCtorInt32(0);
            if (!alreadyDispatched.TryGetValue(server, out var owningAssembly))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                FenrirDiagnostics.DispatchOwnershipConflict,
                typeSymbol.Locations.FirstOrDefault(),
                assemblyName,
                EmittedNames.PrefixOf(server),
                owningAssembly,
                EmittedNames.MessageDispatcher(server)));

            return;
        }
    }
}
