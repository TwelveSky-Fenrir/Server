using System.Runtime.CompilerServices;

// LoginConnectionHost.ReadLivePlayerCountAsync is internal (a small testable seam inside an otherwise
// socket-plumbing host class); only the test project needs to reach across the assembly boundary for it.
[assembly: InternalsVisibleTo("Fenrir.LoginServer.Tests")]
