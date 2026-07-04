using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Scanning;
using Fenrir.Generators.Analysis.Support;
using Fenrir.Generators.Protocol.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fenrir.Generators.Protocol;

/// <summary>Detects <c>[FenrirPacket]</c>/<c>[FenrirWireType]</c>, emits per-type members, then the aggregated dispatch tables.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ProtocolIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(RuntimeHelpersEmitter.HintName, RuntimeHelpersEmitter.Source));

        var packetResults = context.SyntaxProvider.ForAttributeWithMetadataName(
            WellKnownNames.FenrirPacketAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => TypeModelBuilder.BuildPacket(ctx));

        var wireTypeResults = context.SyntaxProvider.ForAttributeWithMetadataName(
            WellKnownNames.FenrirWireTypeAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => TypeModelBuilder.BuildWireType(ctx));

        context.RegisterSourceOutput(packetResults, static (spc, result) => EmitTypeResult(spc, result));
        context.RegisterSourceOutput(wireTypeResults, static (spc, result) => EmitTypeResult(spc, result));

        var packetModels = packetResults
            .Select(static (r, _) => r.Model)
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(packetModels.Collect(), static (spc, models) => EmitAggregates(spc, models));
    }

    private static void EmitTypeResult(SourceProductionContext context, GeneratedTypeResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
            context.ReportDiagnostic(diagnostic);

        if (result.Model is null)
            return;

        var source = PacketEmitter.Emit(result.Model);
        context.AddSource($"{result.Model.Namespace}.{result.Model.TypeName}.g.cs", source);
    }

    private static void EmitAggregates(SourceProductionContext context, ImmutableArray<TypeModel> packets)
    {
        var (deduplicated, collisionDiagnostics) = OpcodeCollisionChecker.Check(packets);

        foreach (var diagnostic in collisionDiagnostics)
            context.ReportDiagnostic(diagnostic);

        context.AddSource(OpcodeRegistryEmitter.HintName, OpcodeRegistryEmitter.Emit(deduplicated));
        context.AddSource(SessionStateGateEmitter.HintName, SessionStateGateEmitter.Emit(deduplicated));

        var loginFactory = MessageFactoryEmitter.Emit(FenrirServer.Login, deduplicated);
        if (loginFactory is not null)
            context.AddSource(MessageFactoryEmitter.HintNameFor(FenrirServer.Login), loginFactory);

        var zoneFactory = MessageFactoryEmitter.Emit(FenrirServer.Zone, deduplicated);
        if (zoneFactory is not null)
            context.AddSource(MessageFactoryEmitter.HintNameFor(FenrirServer.Zone), zoneFactory);
    }
}
