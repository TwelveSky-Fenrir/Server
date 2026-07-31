using System;

// Polyfill netstandard2.0. Le namespace est IMPOSE : le compilateur C# ne reconnait ce type que
// par son nom pleinement qualifie System.Runtime.CompilerServices. Le deplacer ailleurs ne le
// renomme pas, il le desactive.
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    public string FeatureName { get; } = featureName;

    public bool IsOptional { get; set; }
}
