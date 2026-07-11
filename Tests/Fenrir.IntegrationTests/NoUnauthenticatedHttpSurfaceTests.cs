namespace Fenrir.IntegrationTests;

public sealed class NoUnauthenticatedHttpSurfaceTests
{
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

        Assert.DoesNotContain("WebApplication.CreateBuilder", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("WebApplication.Create(", contents, StringComparison.Ordinal);
        Assert.Contains("Host.CreateApplicationBuilder", contents, StringComparison.Ordinal);
    }

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
