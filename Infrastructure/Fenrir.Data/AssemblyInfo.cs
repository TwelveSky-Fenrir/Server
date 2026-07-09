using System.Runtime.CompilerServices;

// MacRestrictionRepository.SelectRestriction is internal; Fenrir.Application.Login.Tests regression-tests its
// MachineGuid-vs-MacAddress matching semantics directly (pure function, no database needed) rather than only
// through Fenrir.Data.Tests' Docker-backed MacRestrictionProcTests.
[assembly: InternalsVisibleTo("Fenrir.Application.Login.Tests")]
