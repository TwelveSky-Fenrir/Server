using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Fenrir.Generators.Analyzers;

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

    private static Location? AttributeLocation(AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
    }
}
