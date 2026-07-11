using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

internal readonly record struct GeneratedTypeResult
{
    public TypeModel? Model { get; init; }

    public required EquatableArray<Diagnostic> Diagnostics { get; init; }
}
