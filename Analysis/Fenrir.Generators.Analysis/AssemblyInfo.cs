using System.Runtime.CompilerServices;

// Analysis/Model/Support types are internal by design (encapsulated analysis layer, not a public
// API); Fenrir.Generators.Protocol/Dispatch each consume them across the assembly boundary as a friend.
[assembly: InternalsVisibleTo("Fenrir.Generators.Protocol")]
[assembly: InternalsVisibleTo("Fenrir.Generators.Dispatch")]
