using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fenrir.Generators.Analysis.Scanning;

/// <summary>
///     Builds a <see cref="TypeModel" /> (+ diagnostics) from the Roslyn context of a <c>[FenrirPacket]</c>/
///     <c>[FenrirWireType]</c>.
/// </summary>
internal static class TypeModelBuilder
{
    public static GeneratedTypeResult BuildPacket(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || !CheckShape(context, typeSymbol, diagnostics))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

        var attribute = context.Attributes[0];
        var server = (FenrirServer)attribute.GetCtorInt32(0);
        var direction = (FenrirDirection)attribute.GetCtorInt32(1);
        var opcode = (byte)attribute.GetCtorInt32(2);
        var obfuscation = (WireObfuscationMode)attribute.GetNamedByteEnum("Obfuscation", 0);
        var compressed = attribute.GetNamedBool("Compressed", false);
        var expectedSize = attribute.GetNamedInt32("ExpectedSize", -1);
        var allowedStates = attribute.GetNamedByteArray("AllowedStates");

        if (direction == FenrirDirection.Outgoing && allowedStates.Length > 0)
            diagnostics.Add(Diagnostic.Create(
                FenrirDiagnostics.AllowedStatesOnOutgoingPacket,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));

        var fieldDiagnostics = new List<Diagnostic>();
        var fields = FieldScanner.Scan(typeSymbol, context.SemanticModel.Compilation, fieldDiagnostics,
            out var fieldsSize);
        diagnostics.AddRange(fieldDiagnostics);

        var headerSize = direction == FenrirDirection.Incoming
            ? WireHeaderSizes.ClientPacketSize
            : WireHeaderSizes.DefaultPacketSize;
        var computedTotal = headerSize + fieldsSize;

        if (expectedSize != -1 && expectedSize != computedTotal)
            diagnostics.Add(Diagnostic.Create(
                FenrirDiagnostics.ExpectedSizeMismatch,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                expectedSize,
                computedTotal));

        if (fieldDiagnostics.Count > 0 || (expectedSize != -1 && expectedSize != computedTotal))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

        var implementsIncoming =
            typeSymbol.AllInterfaces.Any(i => SymbolNameHelpers.IsClosedGenericOf(i, WellKnownNames.IIncomingPacket));
        var implementsOutgoing =
            typeSymbol.AllInterfaces.Any(i => SymbolNameHelpers.Is(i, WellKnownNames.IOutgoingPacket));

        var model = new TypeModel
        {
            Namespace = SymbolNameHelpers.GetFullNamespace(typeSymbol.ContainingNamespace),
            TypeName = typeSymbol.Name,
            FullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsPacket = true,
            Server = server,
            Direction = direction,
            Opcode = opcode,
            Obfuscation = obfuscation,
            Compressed = compressed,
            ExpectedSize = expectedSize,
            AllowedStates = allowedStates,
            ImplementsIncoming = implementsIncoming,
            ImplementsOutgoing = implementsOutgoing,
            Fields = fields,
            FieldsSize = fieldsSize,
            Location = typeSymbol.Locations.FirstOrDefault() ?? Location.None
        };

        return new GeneratedTypeResult { Model = model, Diagnostics = diagnostics.ToImmutable() };
    }

    public static GeneratedTypeResult BuildWireType(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || !CheckShape(context, typeSymbol, diagnostics))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

        var attribute = context.Attributes[0];
        var expectedSize = attribute.GetCtorInt32(0);

        var fieldDiagnostics = new List<Diagnostic>();
        var fields = FieldScanner.Scan(typeSymbol, context.SemanticModel.Compilation, fieldDiagnostics,
            out var fieldsSize);
        diagnostics.AddRange(fieldDiagnostics);

        if (expectedSize != -1 && expectedSize != fieldsSize)
            diagnostics.Add(Diagnostic.Create(
                FenrirDiagnostics.ExpectedSizeMismatch,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                expectedSize,
                fieldsSize));

        if (fieldDiagnostics.Count > 0 || (expectedSize != -1 && expectedSize != fieldsSize))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

        var model = new TypeModel
        {
            Namespace = SymbolNameHelpers.GetFullNamespace(typeSymbol.ContainingNamespace),
            TypeName = typeSymbol.Name,
            FullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsPacket = false,
            ExpectedSize = expectedSize,
            ImplementsIncoming = false,
            ImplementsOutgoing = false,
            Fields = fields,
            FieldsSize = fieldsSize,
            Location = typeSymbol.Locations.FirstOrDefault() ?? Location.None
        };

        return new GeneratedTypeResult { Model = model, Diagnostics = diagnostics.ToImmutable() };
    }

    private static bool CheckShape(GeneratorAttributeSyntaxContext context, INamedTypeSymbol typeSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var isPartial = context.TargetNode is TypeDeclarationSyntax typeDeclaration &&
                        typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        if (typeSymbol is { TypeKind: TypeKind.Struct, IsRecord: true, IsReadOnly: true } && isPartial)
            return true;

        diagnostics.Add(Diagnostic.Create(FenrirDiagnostics.NotReadonlyPartialRecordStruct,
            typeSymbol.Locations.FirstOrDefault(), typeSymbol.Name));
        return false;
    }
}
