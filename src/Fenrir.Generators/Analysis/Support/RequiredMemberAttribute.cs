using System;

namespace Fenrir.Generators.Analysis.Support;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute;
