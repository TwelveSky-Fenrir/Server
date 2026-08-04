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

internal static class TypeModelBuilder
{
    public static GeneratedTypeResult BuildPacket(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || !CheckShape(context, typeSymbol, diagnostics))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

        var attribute = context.Attributes[0];
        var server = (FenrirServer)attribute.GetCtorInt32(0);
        var direction = (FenrirDirection)attribute.GetCtorInt32(1);
        var opcode = (byte)attribute.GetCtorInt32(2);
        var obfuscation = (WireObfuscationMode)attribute.GetNamedByteEnum("Obfuscation", 0);
        var compressed = attribute.GetNamedBool("Compressed", false);
        var expectedSize = attribute.GetNamedInt32("ExpectedSize", -1);
        var allowedStates = attribute.GetNamedByteArray("AllowedStates", out var unreadableAllowedState);
        var implementsIncoming = ImplementsIncomingPacket(typeSymbol);
        var implementsAnyIncoming = typeSymbol.AllInterfaces.Any(i =>
            SymbolNameHelpers.IsClosedGenericOf(i, WellKnownNames.IIncomingPacket));
        var implementsOutgoing = typeSymbol.AllInterfaces.Any(i =>
            SymbolNameHelpers.Is(i, WellKnownNames.IOutgoingPacket));
        var hasDirectionInterfaceMismatch = direction switch
        {
            FenrirDirection.Incoming => !implementsIncoming || implementsOutgoing,
            FenrirDirection.Outgoing => !implementsOutgoing || implementsAnyIncoming,
            _ => true
        };

        if (hasDirectionInterfaceMismatch)
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.PacketDirectionInterfaceMismatch,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                direction));

        var incomingTransform = direction == FenrirDirection.Incoming &&
                                (compressed || obfuscation != WireObfuscationMode.None);
        if (incomingTransform)
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.IncomingPacketTransform,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                DescribeIncomingTransforms(compressed, obfuscation)));

        if (unreadableAllowedState)
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.UnreadableAllowedStatesElement,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));

        if (direction == FenrirDirection.Outgoing && allowedStates.Length > 0)
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.AllowedStatesOnOutgoingPacket,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));

        var fieldDiagnostics = new List<DiagnosticInfo>();
        var fields = FieldScanner.Scan(typeSymbol, context.SemanticModel.Compilation, fieldDiagnostics,
            out var fieldsSize);
        diagnostics.AddRange(fieldDiagnostics);

        var headerSize = direction == FenrirDirection.Incoming
            ? WireHeaderSizes.ClientPacketSize
            : WireHeaderSizes.DefaultPacketSize;
        var computedTotal = headerSize + fieldsSize;

        if (expectedSize != -1 && expectedSize != computedTotal)
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.ExpectedSizeMismatch,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                expectedSize,
                computedTotal));

        if (hasDirectionInterfaceMismatch || incomingTransform || fieldDiagnostics.Count > 0 ||
            (expectedSize != -1 && expectedSize != computedTotal))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

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
            Location = LocationInfo.From(typeSymbol.Locations.FirstOrDefault())
        };

        return new GeneratedTypeResult { Model = model, Diagnostics = diagnostics.ToImmutable() };
    }

    private static bool ImplementsIncomingPacket(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            SymbolNameHelpers.IsClosedGenericOf(i, WellKnownNames.IIncomingPacket) &&
            i.TypeArguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], typeSymbol));
    }

    private static string DescribeIncomingTransforms(bool compressed, WireObfuscationMode obfuscation)
    {
        if (compressed && obfuscation != WireObfuscationMode.None)
            return $"Compressed = true and Obfuscation = {obfuscation}";

        return compressed ? "Compressed = true" : $"Obfuscation = {obfuscation}";
    }

    public static GeneratedTypeResult BuildWireType(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || !CheckShape(context, typeSymbol, diagnostics))
            return new GeneratedTypeResult { Model = null, Diagnostics = diagnostics.ToImmutable() };

        var attribute = context.Attributes[0];
        var expectedSize = attribute.GetCtorInt32(0);

        if (expectedSize < 0)
            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.WireTypeMissingExpectedSize,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));

        var fieldDiagnostics = new List<DiagnosticInfo>();
        var fields = FieldScanner.Scan(typeSymbol, context.SemanticModel.Compilation, fieldDiagnostics,
            out var fieldsSize);
        diagnostics.AddRange(fieldDiagnostics);

        if (expectedSize != -1 && expectedSize != fieldsSize)
            diagnostics.Add(DiagnosticInfo.Create(
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
            Location = LocationInfo.From(typeSymbol.Locations.FirstOrDefault())
        };

        return new GeneratedTypeResult { Model = model, Diagnostics = diagnostics.ToImmutable() };
    }

    private static bool CheckShape(GeneratorAttributeSyntaxContext context, INamedTypeSymbol typeSymbol,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var isPartial = context.TargetNode is TypeDeclarationSyntax typeDeclaration &&
                        typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        if (typeSymbol is not { TypeKind: TypeKind.Struct, IsRecord: true, IsReadOnly: true } || !isPartial)
        {
            diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.NotReadonlyPartialRecordStruct,
                typeSymbol.Locations.FirstOrDefault(), typeSymbol.Name));
            return false;
        }

        if (context.TargetNode is RecordDeclarationSyntax { ParameterList.Parameters.Count: > 0 })
        {
            diagnostics.Add(DiagnosticInfo.Create(FenrirDiagnostics.PositionalRecordStructUnsupported,
                typeSymbol.Locations.FirstOrDefault(), typeSymbol.Name));
            return false;
        }

        return true;
    }
}
