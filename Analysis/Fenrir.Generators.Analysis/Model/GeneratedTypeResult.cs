using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Output of a type-analysis pass: the model (null if unrecoverable, e.g. <c>FEN001</c>) plus diagnostics to
///     report, carried through to <c>RegisterSourceOutput</c>.
/// </summary>
internal sealed class GeneratedTypeResult
{
    public TypeModel? Model { get; init; }

    public required ImmutableArray<Diagnostic> Diagnostics { get; init; }
}
