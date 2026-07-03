namespace Fenrir.Generators.Analysis.Model;

/// <summary>Resolved wire shape of a property, derived from its C# type + attributes (spec §1.3).</summary>
internal enum FieldShape
{
    Int32,
    UInt32,
    Byte,
    Single,
    Int64,
    FixedString,
    Int32Array,
    SingleArray,
    ByteArray,
    FixedStringArray,
    Nested,
    NestedArray
}
