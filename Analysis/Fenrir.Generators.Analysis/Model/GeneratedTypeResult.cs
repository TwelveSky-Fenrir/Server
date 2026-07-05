using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>Type-analysis result; <see cref="Model" /> is null when unrecoverable (e.g. <c>FEN001</c>).</summary>
internal sealed class GeneratedTypeResult
{
    public TypeModel? Model { get; init; }

    public required ImmutableArray<Diagnostic> Diagnostics { get; init; }
}
