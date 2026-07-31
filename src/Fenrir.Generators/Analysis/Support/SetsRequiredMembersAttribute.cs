using System;

// Polyfill netstandard2.0. Le namespace est IMPOSE : le compilateur C# ne reconnait ce type que
// par son nom pleinement qualifie System.Runtime.CompilerServices. Le deplacer ailleurs ne le
// renomme pas, il le desactive.
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Constructor)]
internal sealed class SetsRequiredMembersAttribute : Attribute;
