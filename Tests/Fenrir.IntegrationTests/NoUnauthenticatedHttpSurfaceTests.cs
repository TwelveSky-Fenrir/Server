namespace Fenrir.IntegrationTests;

/// <summary>
///     Standing regression guard for the single worst legacy anti-pattern found in the security audits of
///     <c>Server/ts25center</c>: an unauthenticated <c>cpp-httplib</c> dashboard bound to
///     <c>127.0.0.1:2499</c>, with zero authentication anywhere in <c>set_pre_routing_handler</c> and a
///     side-effecting <c>GET /Shutdown</c> that arms a cluster-wide kill switch
///     (<c>Server/ts25center/S02_MyServer.cpp:56-82,215-253</c>; see
///     <c>ServerDocs/10_ts25center/03_HTTP_Dashboard_NonAuth_CRITIQUE.md</c> and
///     <c>ServerDocs/04_SECURITE_ET_DETTE_TECHNIQUE.md</c> finding #2).
/// </summary>
/// <remarks>
///     Fenrir.LoginServer and Fenrir.GameServer currently have NO HTTP surface at all -- both are
///     <c>Microsoft.NET.Sdk.Worker</c> projects (<c>Host.CreateApplicationBuilder</c>, never
///     <c>WebApplication</c>), and <c>Orchestration/Fenrir.ServiceDefaults</c> deliberately omits any
///     <c>Microsoft.AspNetCore.App</c> reference precisely so neither binary can accidentally grow a Kestrel
///     pipeline (see the comments in <c>Fenrir.ServiceDefaults.csproj</c> and
///     <c>Fenrir.ServiceDefaults/Extensions.cs</c>: "these are TCP-socket-only servers"). There is therefore
///     nothing at the wire-endpoint level to unit test yet -- this test instead pins down the structural
///     precondition that makes an accidental HTTP surface impossible in the first place: neither server
///     project may reference the ASP.NET Core web SDK/framework/hosting packages. If this test ever starts
///     failing, it means someone is adding real HTTP hosting capability to one of these two executables --
///     which is fine, but from that moment on any route it exposes needs its own explicit authentication and
///     authorization story from line one (never inherited from <c>ClientSession</c>/game-session auth, never
///     a state-mutating action behind a plain <c>GET</c>), per the standing rule documented at the top of
///     both <c>Program.cs</c> files and in the <c>ts25-security-findings-catalog</c> skill.
/// </remarks>
public sealed class NoUnauthenticatedHttpSurfaceTests
{
    // Substrings that would only ever appear in a .csproj once something pulls in ASP.NET Core hosting --
    // Microsoft.NET.Sdk.Worker projects (what both servers use today) never contain any of these on their own.
    private static readonly string[] ForbiddenCsprojMarkers =
    [
        "Microsoft.NET.Sdk.Web",
        "Microsoft.AspNetCore",
        "FrameworkReference Include=\"Microsoft.AspNetCore.App\""
    ];

    [Theory]
    [InlineData("Servers", "Fenrir.LoginServer", "Fenrir.LoginServer.csproj")]
    [InlineData("Servers", "Fenrir.GameServer", "Fenrir.GameServer.csproj")]
    public void ServerProject_DoesNotReferenceAspNetCore(params string[] relativePathParts)
    {
        var csprojPath = Path.Combine(FindRepoRoot(), Path.Combine(relativePathParts));
        Assert.True(File.Exists(csprojPath), $"Expected to find {csprojPath}.");

        var contents = File.ReadAllText(csprojPath);

        foreach (var marker in ForbiddenCsprojMarkers)
            Assert.DoesNotContain(marker, contents, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Servers", "Fenrir.LoginServer", "Program.cs")]
    [InlineData("Servers", "Fenrir.GameServer", "Program.cs")]
    public void ServerEntryPoint_DoesNotBuildAWebApplication(params string[] relativePathParts)
    {
        var programPath = Path.Combine(FindRepoRoot(), Path.Combine(relativePathParts));
        Assert.True(File.Exists(programPath), $"Expected to find {programPath}.");

        var contents = File.ReadAllText(programPath);

        // Checks for the actual construction calls, not the bare word "WebApplication" -- this file's own
        // guardrail comment (above) legitimately mentions that type by name as a documented anti-pattern.
        Assert.DoesNotContain("WebApplication.CreateBuilder", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("WebApplication.Create(", contents, StringComparison.Ordinal);
        Assert.Contains("Host.CreateApplicationBuilder", contents, StringComparison.Ordinal);
    }

    // No repo-root-walking helper exists elsewhere in the test suite (other fixtures copy files next to the
    // test host via CopyToOutputDirectory instead), so this walks up from the test assembly's own build
    // output until it finds the .slnx that anchors the repo -- the two server projects being asserted on are
    // source files, not build outputs, so they were never going to be next to AppContext.BaseDirectory anyway.
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0)
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                $"Could not locate the repo root (a *.slnx file) above {AppContext.BaseDirectory}.");

        return dir.FullName;
    }
}
