using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Scanning;
using Fenrir.Generators.Analysis.Support;
using Fenrir.Generators.Protocol.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fenrir.Generators.Protocol;

[Generator(LanguageNames.CSharp)]
public sealed class ProtocolIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var packetResults = context.SyntaxProvider.ForAttributeWithMetadataName(
            WellKnownNames.FenrirPacketAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => TypeModelBuilder.BuildPacket(ctx));

        var wireTypeResults = context.SyntaxProvider.ForAttributeWithMetadataName(
            WellKnownNames.FenrirWireTypeAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => TypeModelBuilder.BuildWireType(ctx));

        var nestedSizes = wireTypeResults
            .Collect()
            .Select(static (results, _) => NestedSizeResolver.BuildTable(results))
            .WithTrackingName(TrackingNames.NestedSizes);

        var resolvedPacketResults = packetResults
            .Combine(nestedSizes)
            .Select(static (pair, _) => NestedSizeResolver.Resolve(pair.Left, pair.Right))
            .WithTrackingName(TrackingNames.ResolvedPacketResults);

        var resolvedWireTypeResults = wireTypeResults
            .Combine(nestedSizes)
            .Select(static (pair, _) => NestedSizeResolver.Resolve(pair.Left, pair.Right))
            .WithTrackingName(TrackingNames.ResolvedWireTypeResults);

        context.RegisterSourceOutput(resolvedPacketResults, static (spc, result) => EmitTypeResult(spc, result));
        context.RegisterSourceOutput(resolvedWireTypeResults, static (spc, result) => EmitTypeResult(spc, result));

        var packetModels = resolvedPacketResults
            .Select(static (r, _) => r.Model)
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .WithTrackingName(TrackingNames.PacketModels);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName)
            .WithTrackingName(TrackingNames.AssemblyName);

        context.RegisterSourceOutput(
            packetModels.Collect().Combine(assemblyName),
            static (spc, pair) => EmitAggregates(spc, pair.Left, pair.Right));
    }

    private static void EmitTypeResult(SourceProductionContext context, GeneratedTypeResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
            context.ReportDiagnostic(diagnostic.ToDiagnostic());

        if (result.Model is null)
            return;

        var source = PacketEmitter.Emit(result.Model);
        context.AddSource($"{result.Model.Namespace}.{result.Model.TypeName}.g.cs", source);
    }

    private static void EmitAggregates(
        SourceProductionContext context,
        ImmutableArray<TypeModel> packets,
        string? assemblyName)
    {
        var (deduplicated, collisionDiagnostics) = OpcodeCollisionChecker.Check(packets);

        foreach (var diagnostic in collisionDiagnostics)
            context.ReportDiagnostic(diagnostic.ToDiagnostic());

        if (deduplicated.IsEmpty)
            return;

        var servers = deduplicated.Select(p => p.Server).Distinct().OrderBy(s => s).ToImmutableArray();
        if (servers.Length > 1)
        {
            var emitted = deduplicated[0].Server;
            foreach (var stray in deduplicated.Where(p => p.Server != emitted))
                context.ReportDiagnostic(DiagnosticInfo.Create(
                    FenrirDiagnostics.MultipleServersInCompilation,
                    stray.Location,
                    assemblyName ?? "(unknown)",
                    string.Join(", ", servers),
                    emitted).ToDiagnostic());
            return;
        }

        var namespaceName = EmittedNames.NamespaceFor(assemblyName);

        context.AddSource(OpcodeRegistryEmitter.HintName, OpcodeRegistryEmitter.Emit(deduplicated, namespaceName));
        context.AddSource(SessionStateGateEmitter.HintName, SessionStateGateEmitter.Emit(deduplicated, namespaceName));
    }
}
