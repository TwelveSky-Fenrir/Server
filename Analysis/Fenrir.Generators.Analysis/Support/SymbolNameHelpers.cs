using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Support;

/// <summary>Compares symbol names structurally instead of via <c>ToDisplayString()</c>, which is format-ambiguous.</summary>
internal static class SymbolNameHelpers
{
    public static string GetFullNamespace(INamespaceSymbol? ns)
    {
        if (ns is null || ns.IsGlobalNamespace)
            return string.Empty;

        var parent = GetFullNamespace(ns.ContainingNamespace);
        return parent.Length == 0 ? ns.Name : parent + "." + ns.Name;
    }

    public static bool Is(INamedTypeSymbol? type, string fullyQualifiedMetadataName)
    {
        if (type is null)
            return false;

        var ns = GetFullNamespace(type.ContainingNamespace);
        var full = ns.Length == 0 ? type.MetadataName : ns + "." + type.MetadataName;
        return full == fullyQualifiedMetadataName;
    }

    public static bool IsAttribute(AttributeData attribute, string fullyQualifiedMetadataName)
    {
        return Is(attribute.AttributeClass, fullyQualifiedMetadataName);
    }

    public static bool IsClosedGenericOf(INamedTypeSymbol closedInterface, string openGenericMetadataName)
    {
        var definition = closedInterface.OriginalDefinition;
        var ns = GetFullNamespace(definition.ContainingNamespace);
        var full = ns.Length == 0 ? definition.MetadataName : ns + "." + definition.MetadataName;
        return full == openGenericMetadataName;
    }
}
