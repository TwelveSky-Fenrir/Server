using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Support;

/// <summary>Reads positional/named argument values from <see cref="AttributeData" />.</summary>
internal static class AttributeValueReader
{
    public static AttributeData? Find(this ImmutableArray<AttributeData> attributes, string fullyQualifiedMetadataName)
    {
        return attributes.FirstOrDefault(a => SymbolNameHelpers.IsAttribute(a, fullyQualifiedMetadataName));
    }

    public static int GetCtorInt32(this AttributeData attribute, int index)
    {
        if (index >= attribute.ConstructorArguments.Length)
            return 0;

        return attribute.ConstructorArguments[index].Value switch
        {
            int i => i,
            uint u => unchecked((int)u),
            byte b => b,
            sbyte sb => sb,
            short sh => sh,
            ushort ush => ush,
            long l => unchecked((int)l),
            _ => 0
        };
    }

    public static bool GetNamedBool(this AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var pair in attribute.NamedArguments)
            if (pair.Key == name && pair.Value.Value is bool b)
                return b;

        return defaultValue;
    }

    public static int GetNamedInt32(this AttributeData attribute, string name, int defaultValue)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key != name)
                continue;

            return pair.Value.Value switch
            {
                int i => i,
                byte b => b,
                _ => defaultValue
            };
        }

        return defaultValue;
    }

    public static byte GetNamedByteEnum(this AttributeData attribute, string name, byte defaultValue)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key != name)
                continue;

            return pair.Value.Value switch
            {
                byte b => b,
                int i => unchecked((byte)i),
                _ => defaultValue
            };
        }

        return defaultValue;
    }

    public static ImmutableArray<byte> GetNamedByteArray(this AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key != name || pair.Value.Kind != TypedConstantKind.Array)
                continue;

            var values = pair.Value.Values;
            var builder = ImmutableArray.CreateBuilder<byte>(values.Length);
            foreach (var value in values)
                if (value.Value is byte b)
                    builder.Add(b);

            return builder.ToImmutable();
        }

        return ImmutableArray<byte>.Empty;
    }
}
