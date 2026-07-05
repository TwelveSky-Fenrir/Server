namespace Fenrir.Contracts.Attributes;

/// <summary>
///     Extra <see cref="Wire.WireXor.ApplyUidXor" /> pass on this field before whole-packet XOR (double-XOR per
///     <c>LC_LOGIN_RECV.tID</c>, §3.3); pair with <see cref="FixedStringAttribute" />.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ObfuscatedUidFieldAttribute : Attribute;
