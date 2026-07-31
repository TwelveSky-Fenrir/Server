using System;

// Polyfill netstandard2.0. Le namespace est IMPOSE : le compilateur C# ne reconnait ce type que
// par son nom pleinement qualifie System.Runtime.CompilerServices. Le deplacer ailleurs ne le
// renomme pas, il le desactive.
namespace System.Runtime.CompilerServices;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute;
