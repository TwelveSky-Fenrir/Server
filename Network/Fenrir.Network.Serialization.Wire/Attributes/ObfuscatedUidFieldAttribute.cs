namespace Fenrir.Network.Serialization.Wire.Attributes;

/// <summary>
///     Extra <see cref="Fenrir.Network.Compression.WireXor.ApplyUidXor" /> pass on this field before whole-packet XOR
///     (double-XOR per
///     <c>LC_LOGIN_RECV.tID</c>); pair with <see cref="FixedStringAttribute" />.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ObfuscatedUidFieldAttribute : Attribute;
