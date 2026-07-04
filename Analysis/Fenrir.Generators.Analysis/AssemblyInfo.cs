using System.Runtime.CompilerServices;

// Analysis/Model/Support types are internal; Protocol/Dispatch consume them across the assembly boundary.
[assembly: InternalsVisibleTo("Fenrir.Generators.Protocol")]
[assembly: InternalsVisibleTo("Fenrir.Generators.Dispatch")]
