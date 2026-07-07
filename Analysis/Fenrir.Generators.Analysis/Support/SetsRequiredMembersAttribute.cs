namespace System.Diagnostics.CodeAnalysis;

/// <summary>
///     Polyfill alongside <c>RequiredMemberAttribute</c>/<c>CompilerFeatureRequiredAttribute</c>: needed the
///     moment a type with <c>required</c> members also becomes a <c>record</c>/<c>record struct</c>, since the
///     compiler decorates the synthesized copy ("clone") constructor with this attribute instead of re-checking
///     <c>required</c> assignment at every call site.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
internal sealed class SetsRequiredMembersAttribute : Attribute;
