using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

/// <summary>
/// FEN009 — exige un <c>ExpectedSize</c> explicite sur chaque <c>[FenrirPacket]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>TypeModelBuilder.BuildPacket</c> ne compare la taille calculée que sous <c>if (expectedSize != -1)</c> :
/// un paquet qui ne déclare pas <c>ExpectedSize</c> ne déclenche donc AUCUNE vérification de taille, jamais.
/// Comme le protocole ne porte pas de préfixe de longueur, un champ mal dimensionné ne casse pas un paquet, il
/// décale tout le reste du flux d'octets de la session.
/// </para>
/// <para>
/// Implémenté en analyseur plutôt que dans <c>TypeModelBuilder</c> pour deux raisons : la sévérité reste
/// pilotable depuis <c>.editorconfig</c>, et un paquet qui aurait une raison légitime de s'en passer peut être
/// exempté localement par <c>#pragma warning disable FEN009</c> — ce qu'un diagnostic émis par un générateur
/// ne permet pas de façon fiable. Aucun appel à <c>FieldScanner</c> ici : l'analyseur tourne dans 7 projets et
/// une exception y deviendrait un AD0001, donc une erreur de build pour tout le monde.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PacketExpectedSizeAnalyzer : DiagnosticAnalyzer
{
    private const string ExpectedSizeArgument = "ExpectedSize";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(FenrirDiagnostics.PacketMissingExpectedSize);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
            return;

        var packetAttribute = typeSymbol.GetAttributes().Find(WellKnownNames.FenrirPacketAttribute);
        if (packetAttribute is null)
            return;

        foreach (var namedArgument in packetAttribute.NamedArguments)
            if (namedArgument.Key == ExpectedSizeArgument)
                return;

        context.ReportDiagnostic(Diagnostic.Create(
            FenrirDiagnostics.PacketMissingExpectedSize,
            AttributeLocation(packetAttribute) ?? typeSymbol.Locations.FirstOrDefault(),
            typeSymbol.Name));
    }

    /// <summary>Ancre la vaguelette sur <c>[FenrirPacket(...)]</c> plutôt que sur le nom du type.</summary>
    private static Location? AttributeLocation(AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
    }
}
