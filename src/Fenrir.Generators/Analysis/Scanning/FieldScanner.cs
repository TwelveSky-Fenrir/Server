using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fenrir.Generators.Analysis.Scanning;

internal static class FieldScanner
{
    public static ImmutableArray<FieldModel> Scan(
        INamedTypeSymbol typeSymbol,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        out int totalSize)
    {
        var visiting = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { typeSymbol };
        return ScanCore(typeSymbol, compilation, diagnostics, visiting, out totalSize);
    }

    private static ImmutableArray<FieldModel> ScanCore(
        INamedTypeSymbol typeSymbol,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        HashSet<INamedTypeSymbol> visiting,
        out int totalSize)
    {
        var properties = GetOrderedProperties(typeSymbol, compilation);
        var fields = ImmutableArray.CreateBuilder<FieldModel>(properties.Count);
        var offset = 0;

        foreach (var property in properties)
        {
            var field = BuildField(property, compilation, diagnostics, visiting);
            if (field is null)
                continue;

            offset += field.ReservedBefore + field.OwnSize;
            fields.Add(field);
        }

        totalSize = offset;
        return fields.ToImmutable();
    }

    private static List<IPropertySymbol> GetOrderedProperties(INamedTypeSymbol typeSymbol, Compilation compilation)
    {
        var result = new List<IPropertySymbol>();

        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
                continue;

            var semanticModel = compilation.GetSemanticModel(syntaxReference.SyntaxTree);

            foreach (var member in typeDeclaration.Members)
            {
                if (member is not PropertyDeclarationSyntax propertySyntax)
                    continue;

                if (semanticModel.GetDeclaredSymbol(propertySyntax) is IPropertySymbol
                    {
                        IsStatic: false
                    } propertySymbol)
                    result.Add(propertySymbol);
            }
        }

        return result;
    }

    private static FieldModel? BuildField(
        IPropertySymbol property,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        HashSet<INamedTypeSymbol> visiting)
    {
        var field = BuildFieldCore(property, compilation, diagnostics, visiting);
        if (field is not null)
            ValidateObfuscation(property, field, diagnostics);

        return field;
    }

    private static void ValidateObfuscation(
        IPropertySymbol property,
        FieldModel field,
        List<DiagnosticInfo> diagnostics)
    {
        var propertyAttributes = property.GetAttributes();
        var declaredXor = ReadAvatarXorKind(propertyAttributes.Find(WellKnownNames.AvatarXorKindAttribute), out _);
        var declaredUid = propertyAttributes.Find(WellKnownNames.ObfuscatedUidFieldAttribute) is not null;

        if (declaredXor != AvatarXorKind.None && field.AvatarXor == AvatarXorKind.None)
            diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.ObfuscationAttributeIgnoredOnShape,
                property.Locations.FirstOrDefault(),
                "[AvatarXorKind]", property.ContainingType.Name, property.Name, field.Shape));

        if (declaredUid && !field.IsLegacyUidField)
            diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.ObfuscationAttributeIgnoredOnShape,
                property.Locations.FirstOrDefault(),
                "[ObfuscatedUidField]", property.ContainingType.Name, property.Name, field.Shape));

        if (field.AvatarXor != AvatarXorKind.Char2)
            return;

        if (field.AvatarXorRowLength <= 0 || field.OwnSize % field.AvatarXorRowLength != 0)
            diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.InvalidAvatarXorRowLength,
                property.Locations.FirstOrDefault(),
                property.ContainingType.Name, property.Name, field.AvatarXorRowLength, field.OwnSize));
    }

    private static FieldModel? BuildFieldCore(
        IPropertySymbol property,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        HashSet<INamedTypeSymbol> visiting)
    {
        var propertyAttributes = property.GetAttributes();

        var reservedAttribute = propertyAttributes.Find(WellKnownNames.ReservedAttribute);
        var reservedBefore = 0;
        if (reservedAttribute is not null)
        {
            var length = reservedAttribute.GetCtorInt32(0);
            if (length <= 0)
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.InvalidLength,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name, property.Name, "[Reserved]"));
            else
                reservedBefore = length;
        }

        var fixedStringAttribute = propertyAttributes.Find(WellKnownNames.FixedStringAttribute);
        int? fixedStringLength = null;
        if (fixedStringAttribute is not null)
        {
            var length = fixedStringAttribute.GetCtorInt32(0);
            if (length <= 0)
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.InvalidLength,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name, property.Name, "[FixedString]"));
            fixedStringLength = length;
        }

        var fixedArrayAttribute = propertyAttributes.Find(WellKnownNames.FixedArrayAttribute);
        int? fixedArrayCount = null;
        if (fixedArrayAttribute is not null)
        {
            var count = fixedArrayAttribute.GetCtorInt32(0);
            if (count <= 0)
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.InvalidLength,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name, property.Name, "[FixedArray]"));
            fixedArrayCount = count;
        }

        var legacyUidAttribute = propertyAttributes.Find(WellKnownNames.ObfuscatedUidFieldAttribute);
        var avatarXorAttribute = propertyAttributes.Find(WellKnownNames.AvatarXorKindAttribute);
        var avatarXor = ReadAvatarXorKind(avatarXorAttribute, out var avatarXorRowLength);
        var isLegacyUidField = legacyUidAttribute is not null;

        var type = property.Type;

        if (type.SpecialType == SpecialType.System_String)
        {
            if (fixedStringLength is null)
            {
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.MissingSizeAttribute,
                    property.Locations.FirstOrDefault(), property.ContainingType.Name, property.Name,
                    type.ToDisplayString(), "[FixedString(N)]"));
                return null;
            }

            return new FieldModel
            {
                PropertyName = property.Name,
                Shape = FieldShape.FixedString,
                ReservedBefore = reservedBefore,
                StringLength = fixedStringLength.Value,
                OwnSize = fixedStringLength.Value,
                IsLegacyUidField = isLegacyUidField,
                AvatarXor = avatarXor,
                AvatarXorRowLength = avatarXorRowLength
            };
        }

        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
        {
            var elementType = arrayType.ElementType;

            if (elementType is INamedTypeSymbol { TypeKind: TypeKind.Struct } elementNamedType &&
                ImplementsWireType(elementNamedType))
            {
                if (fixedArrayCount is null)
                {
                    diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.MissingSizeAttribute,
                        property.Locations.FirstOrDefault(), property.ContainingType.Name, property.Name,
                        type.ToDisplayString(), "[FixedArray(N)]"));
                    return null;
                }

                var nestedElementSize =
                    ResolveNestedSize(elementNamedType, compilation, visiting, property, diagnostics);

                return new FieldModel
                {
                    PropertyName = property.Name,
                    Shape = FieldShape.NestedArray,
                    ReservedBefore = reservedBefore,
                    ElementCount = fixedArrayCount.Value,
                    NestedTypeFullName = elementNamedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    NestedSize = nestedElementSize,
                    OwnSize = fixedArrayCount.Value * nestedElementSize
                };
            }

            if (elementType.SpecialType == SpecialType.System_String)
            {
                if (fixedArrayCount is not null && fixedStringLength is not null)
                    return new FieldModel
                    {
                        PropertyName = property.Name,
                        Shape = FieldShape.FixedStringArray,
                        ReservedBefore = reservedBefore,
                        ElementCount = fixedArrayCount.Value,
                        StringLength = fixedStringLength.Value,
                        OwnSize = fixedArrayCount.Value * fixedStringLength.Value,
                        IsLegacyUidField = isLegacyUidField,
                        AvatarXor = avatarXor,
                        AvatarXorRowLength = avatarXorRowLength
                    };
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.MissingSizeAttribute,
                    property.Locations.FirstOrDefault(), property.ContainingType.Name, property.Name,
                    type.ToDisplayString(), "[FixedArray(R)] AND [FixedString(N)] (row width)"));
                return null;
            }

            FieldShape? arrayShape = elementType.SpecialType switch
            {
                SpecialType.System_Int32 => FieldShape.Int32Array,
                SpecialType.System_Single => FieldShape.SingleArray,
                SpecialType.System_Byte => FieldShape.ByteArray,
                _ => null
            };

            if (arrayShape is null)
            {
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.UnsupportedFieldType,
                    property.Locations.FirstOrDefault(), property.ContainingType.Name, property.Name,
                    type.ToDisplayString()));
                return null;
            }

            if (fixedArrayCount is null)
            {
                diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.MissingSizeAttribute,
                    property.Locations.FirstOrDefault(), property.ContainingType.Name, property.Name,
                    type.ToDisplayString(), "[FixedArray(N)]"));
                return null;
            }

            var elementSize = elementType.SpecialType == SpecialType.System_Byte ? 1 : 4;

            return new FieldModel
            {
                PropertyName = property.Name,
                Shape = arrayShape.Value,
                ReservedBefore = reservedBefore,
                ElementCount = fixedArrayCount.Value,
                OwnSize = fixedArrayCount.Value * elementSize,
                AvatarXor = avatarXor,
                AvatarXorRowLength = avatarXorRowLength
            };
        }

        FieldShape? scalarShape = type.SpecialType switch
        {
            SpecialType.System_Int32 => FieldShape.Int32,
            SpecialType.System_UInt32 => FieldShape.UInt32,
            SpecialType.System_Byte => FieldShape.Byte,
            SpecialType.System_Single => FieldShape.Single,
            SpecialType.System_Int64 => FieldShape.Int64,
            _ => null
        };

        if (scalarShape is not null)
        {
            var size = scalarShape switch
            {
                FieldShape.Byte => 1,
                FieldShape.Int64 => 8,
                _ => 4
            };

            return new FieldModel
            {
                PropertyName = property.Name,
                Shape = scalarShape.Value,
                ReservedBefore = reservedBefore,
                OwnSize = size,
                AvatarXor = avatarXor,
                AvatarXorRowLength = avatarXorRowLength
            };
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct } namedType)
            if (ImplementsWireType(namedType))
            {
                var nestedSize = ResolveNestedSize(namedType, compilation, visiting, property, diagnostics);

                return new FieldModel
                {
                    PropertyName = property.Name,
                    Shape = FieldShape.Nested,
                    ReservedBefore = reservedBefore,
                    NestedTypeFullName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    NestedSize = nestedSize,
                    OwnSize = nestedSize
                };
            }

        diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.UnsupportedFieldType,
            property.Locations.FirstOrDefault(),
            property.ContainingType.Name, property.Name, type.ToDisplayString()));
        return null;
    }

    private static bool ImplementsWireType(INamedTypeSymbol candidateType)
    {
        return candidateType.AllInterfaces.Any(candidate =>
            SymbolNameHelpers.IsClosedGenericOf(candidate, WellKnownNames.IFenrirWireType) &&
            candidate.TypeArguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], candidateType));
    }

    private static int ResolveNestedSize(INamedTypeSymbol nestedType, Compilation compilation,
        HashSet<INamedTypeSymbol> visiting, IPropertySymbol property, List<DiagnosticInfo> diagnostics)
    {
        var wireTypeAttribute = nestedType.GetAttributes().Find(WellKnownNames.FenrirWireTypeAttribute);
        if (wireTypeAttribute is not null)
        {
            var expected = wireTypeAttribute.GetCtorInt32(0);
            if (expected >= 0)
                return expected;
        }

        if (nestedType.DeclaringSyntaxReferences.IsEmpty)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.UnresolvableNestedSize,
                property.Locations.FirstOrDefault(),
                nestedType.ToDisplayString(),
                property.ContainingType.Name,
                property.Name));
            return 0;
        }

        if (!visiting.Add(nestedType))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.NestedWireTypeCycle,
                property.Locations.FirstOrDefault(),
                nestedType.ToDisplayString(),
                property.ContainingType.Name,
                property.Name));
            return 0;
        }

        try
        {
            var discardedDiagnostics = new List<DiagnosticInfo>();
            ScanCore(nestedType, compilation, discardedDiagnostics, visiting, out var total);
            return total;
        }
        finally
        {
            visiting.Remove(nestedType);
        }
    }

    private static AvatarXorKind ReadAvatarXorKind(AttributeData? attribute, out int rowLength)
    {
        rowLength = 0;
        if (attribute is null)
            return AvatarXorKind.None;

        var kind = (AvatarXorKind)attribute.GetCtorInt32(0);
        rowLength = attribute.GetCtorInt32(1);
        return kind;
    }
}
