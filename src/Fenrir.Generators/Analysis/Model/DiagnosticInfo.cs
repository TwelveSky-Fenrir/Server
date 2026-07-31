using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

// Meme raison que LocationInfo : un Diagnostic porte un Location, donc un SyntaxTree, donc la Compilation.
// Les arguments sont figes en chaines a la construction — string.Format produit le meme texte, et des
// chaines se comparent par valeur sans travail supplementaire.
internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    private DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, EquatableArray<string> arguments)
    {
        Descriptor = descriptor;
        Location = location;
        Arguments = arguments;
    }

    public DiagnosticDescriptor Descriptor { get; }

    public LocationInfo? Location { get; }

    public EquatableArray<string> Arguments { get; }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location,
        params object?[] arguments)
    {
        return Create(descriptor, LocationInfo.From(location), arguments);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location,
        params object?[] arguments)
    {
        var builder = ImmutableArray.CreateBuilder<string>(arguments.Length);
        foreach (var argument in arguments)
            builder.Add(Stringify(argument));

        return new DiagnosticInfo(descriptor, location, new EquatableArray<string>(builder.ToImmutable()));
    }

    private static string Stringify(object? argument)
    {
        return argument switch
        {
            null => "",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => argument.ToString() ?? ""
        };
    }

    public Diagnostic ToDiagnostic()
    {
        var args = new object?[Arguments.Length];
        for (var i = 0; i < Arguments.Length; i++)
            args[i] = Arguments[i];

        return Diagnostic.Create(Descriptor, Location?.ToLocation(), args);
    }

    public bool Equals(DiagnosticInfo other)
    {
        return ReferenceEquals(Descriptor, other.Descriptor)
               && Nullable.Equals(Location, other.Location)
               && Arguments.Equals(other.Arguments);
    }

    public override bool Equals(object? obj)
    {
        return obj is DiagnosticInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Descriptor.Id.GetHashCode();
            hash = (hash * 397) ^ (Location?.GetHashCode() ?? 0);
            return (hash * 397) ^ Arguments.GetHashCode();
        }
    }
}
