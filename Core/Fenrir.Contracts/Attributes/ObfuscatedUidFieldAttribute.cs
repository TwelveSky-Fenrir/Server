namespace Fenrir.Contracts.Attributes;

/// <summary>
///     Marks the <c>tID</c> field that gets an EXTRA <see cref="Wire.WireXor.ApplyUidXor" /> pass
///     (on its null-terminated prefix) before the whole-packet obfuscation is applied to the buffer —
///     hence the double-XOR documented on <c>LC_LOGIN_RECV.tID</c> (§3.3). Only used together with
///     <see cref="FixedStringAttribute" /> on the same field.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ObfuscatedUidFieldAttribute : Attribute;
