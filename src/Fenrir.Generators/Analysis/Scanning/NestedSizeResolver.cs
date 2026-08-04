using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Fenrir.Generators.Analysis.Diagnostics;
using Fenrir.Generators.Analysis.Model;
using Fenrir.Generators.Analysis.Support;

namespace Fenrir.Generators.Analysis.Scanning;

internal static class NestedSizeResolver
{
    public static EquatableArray<NestedSizeEntry> BuildTable(ImmutableArray<GeneratedTypeResult> wireTypeResults)
    {
        var models = new List<TypeModel>(wireTypeResults.Length);
        foreach (var result in wireTypeResults)
            if (result.Model is not null)
                models.Add(result.Model);

        if (models.Count == 0)
            return EquatableArray<NestedSizeEntry>.Empty;

        var sizes = new Dictionary<string, int>(models.Count, StringComparer.Ordinal);
        foreach (var model in models)
            sizes[model.FullTypeName] = model.FieldsSize;

        for (var pass = 0; pass < models.Count; pass++)
        {
            var changed = false;

            foreach (var model in models)
            {
                var total = 0;
                foreach (var field in model.Fields)
                    total += field.ReservedBefore + OwnSizeOf(field, sizes);

                if (sizes[model.FullTypeName] == total)
                    continue;

                sizes[model.FullTypeName] = total;
                changed = true;
            }

            if (!changed)
                break;
        }

        var builder = ImmutableArray.CreateBuilder<NestedSizeEntry>(models.Count);
        foreach (var model in models)
            builder.Add(new NestedSizeEntry
            {
                TypeFullName = model.FullTypeName,
                Size = sizes[model.FullTypeName]
            });

        var sorted = builder.ToImmutable()
            .Sort(static (left, right) => string.CompareOrdinal(left.TypeFullName, right.TypeFullName));

        return new EquatableArray<NestedSizeEntry>(sorted);
    }

    public static GeneratedTypeResult Resolve(GeneratedTypeResult result, EquatableArray<NestedSizeEntry> table)
    {
        var model = result.Model;
        if (model is null || table.Length == 0)
            return result;

        var fields = model.Fields;
        ImmutableArray<FieldModel>.Builder? repaired = null;

        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var authoritative = AuthoritativeSizeOf(field, table);

            if (authoritative is null || authoritative.Value == field.NestedSize)
            {
                repaired?.Add(field);
                continue;
            }

            repaired ??= CopyPrefix(fields, i);
            var size = authoritative.Value;
            repaired.Add(field with
            {
                NestedSize = size,
                OwnSize = field.Shape == FieldShape.Nested ? size : field.ElementCount * size
            });
        }

        if (repaired is null)
            return result;

        var repairedFields = repaired.ToImmutable();
        var fieldsSize = 0;
        foreach (var field in repairedFields)
            fieldsSize += field.ReservedBefore + field.OwnSize;

        var headerSize = model.IsPacket
            ? model.Direction == FenrirDirection.Incoming
                ? WireHeaderSizes.ClientPacketSize
                : WireHeaderSizes.DefaultPacketSize
            : 0;
        var computedTotal = headerSize + fieldsSize;

        if (model.ExpectedSize != -1 && model.ExpectedSize != computedTotal)
        {
            var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>(result.Diagnostics.Length + 1);
            foreach (var diagnostic in result.Diagnostics)
                diagnostics.Add(diagnostic);

            diagnostics.Add(DiagnosticInfo.Create(
                FenrirDiagnostics.ExpectedSizeMismatch,
                model.Location,
                model.TypeName,
                model.ExpectedSize,
                computedTotal));

            return new GeneratedTypeResult
            {
                Model = null,
                Diagnostics = new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
            };
        }

        return result with
        {
            Model = model with
            {
                Fields = new EquatableArray<FieldModel>(repairedFields),
                FieldsSize = fieldsSize
            }
        };
    }

    private static int OwnSizeOf(FieldModel field, Dictionary<string, int> sizes)
    {
        if (field.Shape is not (FieldShape.Nested or FieldShape.NestedArray) ||
            field.NestedTypeFullName is null ||
            !sizes.TryGetValue(field.NestedTypeFullName, out var size))
            return field.OwnSize;

        return field.Shape == FieldShape.Nested ? size : field.ElementCount * size;
    }

    private static int? AuthoritativeSizeOf(FieldModel field, EquatableArray<NestedSizeEntry> table)
    {
        if (field.Shape is not (FieldShape.Nested or FieldShape.NestedArray) || field.NestedTypeFullName is null)
            return null;

        for (var i = 0; i < table.Length; i++)
            if (string.Equals(table[i].TypeFullName, field.NestedTypeFullName, StringComparison.Ordinal))
                return table[i].Size;

        return null;
    }

    private static ImmutableArray<FieldModel>.Builder CopyPrefix(EquatableArray<FieldModel> fields, int count)
    {
        var builder = ImmutableArray.CreateBuilder<FieldModel>(fields.Length);
        for (var i = 0; i < count; i++)
            builder.Add(fields[i]);

        return builder;
    }
}
