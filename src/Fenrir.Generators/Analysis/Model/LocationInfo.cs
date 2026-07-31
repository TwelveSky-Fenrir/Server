using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Fenrir.Generators.Analysis.Model;

// Un Location retient son SyntaxTree, donc toute la Compilation : le garder dans une valeur de pipeline
// enracine le graphe entier et, faute d'egalite structurelle, invalide le cache a chaque frappe.
internal readonly struct LocationInfo : IEquatable<LocationInfo>
{
    private LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        TextSpan = textSpan;
        LineSpan = lineSpan;
    }

    public string FilePath { get; }

    public TextSpan TextSpan { get; }

    public LinePositionSpan LineSpan { get; }

    public static LocationInfo? From(Location? location)
    {
        if (location?.SourceTree is null)
            return null;

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public Location ToLocation()
    {
        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    public bool Equals(LocationInfo other)
    {
        return FilePath == other.FilePath && TextSpan.Equals(other.TextSpan) && LineSpan.Equals(other.LineSpan);
    }

    public override bool Equals(object? obj)
    {
        return obj is LocationInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FilePath?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ TextSpan.GetHashCode();
            return (hash * 397) ^ LineSpan.GetHashCode();
        }
    }
}
