namespace Fenrir.Generators.Analysis.Model;

internal readonly record struct NestedSizeEntry
{
    public required string TypeFullName { get; init; }

    public required int Size { get; init; }
}
