using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>Type-analysis result; <see cref="Model" /> is null when unrecoverable (e.g. <c>FEN001</c>).</summary>
/// <remarks>
///     A <c>readonly record struct</c>: this is the direct output of each <c>ForAttributeWithMetadataName</c>
///     <c>Select</c> in <c>ProtocolIncrementalGenerator</c> (<c>TypeModelBuilder.BuildPacket</c>/
///     <c>BuildWireType</c>), so its equality is what <c>RegisterSourceOutput</c> diffs against the previous
///     pass to decide whether to re-emit that type's <c>.g.cs</c> at all. <see cref="Diagnostics" /> uses
///     <see cref="EquatableArray{T}" /> rather than a raw <c>ImmutableArray&lt;Diagnostic&gt;</c> for the same
///     reason as <see cref="TypeModel.Fields" />/<see cref="TypeModel.AllowedStates" /> — see that type's own
///     remarks.
/// </remarks>
internal readonly record struct GeneratedTypeResult
{
    public TypeModel? Model { get; init; }

    public required EquatableArray<Diagnostic> Diagnostics { get; init; }
}
