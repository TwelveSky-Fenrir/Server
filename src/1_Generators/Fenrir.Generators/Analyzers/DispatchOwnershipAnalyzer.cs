using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

/// <summary>
/// FEN104 — un seul <c>MessageDispatcher</c> par serveur, dans une seule assembly.
/// </summary>
/// <remarks>
/// <para>
/// FEN008 attrape PLUSIEURS serveurs dans UNE assembly. Le cas symétrique — UN serveur réparti sur DEUX
/// assemblies — lui est structurellement invisible : chaque compilation ne voit que ses propres handlers et
/// conclut légitimement qu'elle est seule. <c>HandlerDispatchIncrementalGenerator</c> émet alors deux
/// <c>ZoneMessageDispatcher</c> dans deux espaces de noms différents. Rien ne casse à la compilation, mais
/// <c>ZoneFrameDispatcher</c> n'en appelle qu'un : la moitié des opcodes tombe dans l'arme <c>default</c> et
/// disparaît dans un LogWarning à l'exécution.
/// </para>
/// <para>
/// Détection sans configuration : si cette compilation déclare des handlers pour le serveur S alors qu'une
/// assembly RÉFÉRENCÉE expose déjà le type <c>{S}MessageDispatcher</c>, le second dispatcher est en train
/// d'être créé ici. <see cref="IAssemblySymbol.TypeNames" /> répond en O(1) et sans dépendre de l'espace de
/// noms d'émission, donc sans supposer quoi que ce soit du nom de l'assembly propriétaire.
/// </para>
/// <para>
/// Angle mort connu et assumé : deux assemblies « sœurs » qui ne se référencent pas l'une l'autre restent
/// mutuellement invisibles. Le composant qui les voit toutes les deux est l'exécutable qui les compose
/// (<c>Fenrir.GameServer</c> &amp; co.), lequel ne référence pas encore <c>Fenrir.Generators</c> en Analyzer.
/// </para>
/// </remarks>
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

        // Cas nominal de tout le dépôt aujourd'hui : aucune assembly référencée n'émet de dispatcher que
        // celle-ci pourrait dupliquer, donc la règle ne coûte rien de plus qu'une poignée de recherches.
        if (alreadyDispatched.Count == 0)
            return;

        var assemblyName = context.Compilation.AssemblyName ?? "(unknown)";

        context.RegisterSymbolAction(
            symbolContext => AnalyzeHandlerType(symbolContext, assemblyName, alreadyDispatched),
            SymbolKind.NamedType);
    }

    /// <summary>Serveur -> nom de l'assembly référencée qui expose déjà son <c>MessageDispatcher</c>.</summary>
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

        // Même découverte que HandlerDispatchIncrementalGenerator.GetHandlerModels : par interface implémentée,
        // et le serveur lu sur le [FenrirPacket] du TYPE DE PAQUET, pas sur le handler.
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

            // Un seul signalement par handler, même s'il implémente plusieurs interfaces de handler.
            return;
        }
    }
}
