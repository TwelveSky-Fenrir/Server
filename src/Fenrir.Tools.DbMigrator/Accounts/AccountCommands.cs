using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Fenrir.Security.Credentials;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Tools.DbMigrator.Accounts;

public static class AccountCommands
{
    public const string CreateKeyword = "create-account";
    public const string GrantGmKeyword = "grant-gm";
    public const string AllowGmIpKeyword = "allow-gm-ip";
    public const string ClearIpBlockKeyword = "clear-ip-block";

    private const int MaxPasswordLength = 32;

    public static void PrintUsage()
    {
        Console.Error.WriteLine($"usage: Fenrir.Tools.DbMigrator {CreateKeyword} <loginName> <password>");
        Console.Error.WriteLine(
            $"       Fenrir.Tools.DbMigrator {GrantGmKeyword} <loginName> <grade>   (grade: 1=Basic, 10=Elevated, 100=Admin, 0=revoke)");
        Console.Error.WriteLine(
            $"       Fenrir.Tools.DbMigrator {AllowGmIpKeyword} <ipAddress>       (required for ANY grade>=1 account to log in at all)");
        Console.Error.WriteLine(
            $"       Fenrir.Tools.DbMigrator {ClearIpBlockKeyword} <ipAddress>    (drops every admin.FirewallRules AND admin.BlockedIps row for that IP)");
    }

    public static async Task<int> CreateAsync(string loginName, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Password must not be empty.");
            return 1;
        }

        if (password.Length > MaxPasswordLength)
        {
            Console.Error.WriteLine(
                $"Password must be at most {MaxPasswordLength} characters (legacy client wire limit); got {password.Length}.");
            return 1;
        }

        var repository = CreateAccountRepositoryOrNull();
        if (repository is null)
            return 1;

        var (hash, salt) = PasswordHasher.Hash(password);

        try
        {
            var accountId = await repository.CreateAsync(loginName, hash, salt, CancellationToken.None);
            Console.WriteLine(accountId);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not create account '{loginName}': {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> GrantGmAsync(string loginName, string gradeText)
    {
        if (!short.TryParse(gradeText, out var grade) || grade < 0)
        {
            Console.Error.WriteLine(
                "Grade must be a non-negative integer (0=revoke, 1=Basic, 10=Elevated, 100=Admin).");
            return 1;
        }

        var repository = CreateAccountRepositoryOrNull();
        if (repository is null)
            return 1;

        try
        {
            await repository.SetGradeAsync(loginName, grade, CancellationToken.None);
            Console.WriteLine($"AccountGrade for '{loginName}' set to {grade}.");
            if (grade >= 1)
                Console.WriteLine(
                    "Note: any grade>=1 account can only log in from an IP on admin.GmAllowlists -- run " +
                    $"'{AllowGmIpKeyword} <yourIp>' now, or this account will be rejected at login.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not set AccountGrade for '{loginName}': {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> AllowGmIpAsync(string ipAddress)
    {
        var repository = CreateGmAllowlistRepositoryOrNull();
        if (repository is null)
            return 1;

        try
        {
            await repository.AddAsync(ipAddress, CancellationToken.None);
            Console.WriteLine($"IP '{ipAddress}' added to the GM login allowlist.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not add IP '{ipAddress}' to the GM allowlist: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ClearIpBlockAsync(string ipAddress)
    {
        var db = CreateDbContextOrNull();
        if (db is null)
            return 1;

        var firewall = await TryRemoveAsync(() =>
            new FirewallRuleRepository(db).RemoveAsync(ipAddress, CancellationToken.None));
        var blocked = await TryRemoveAsync(() =>
            new BlockedIpRepository(db).RemoveAsync(ipAddress, CancellationToken.None));

        ReportRemoval("admin.FirewallRules", ipAddress, firewall);
        ReportRemoval("admin.BlockedIps", ipAddress, blocked);

        if (firewall.Error is not null || blocked.Error is not null)
        {
            Console.Error.WriteLine(
                $"IP '{ipAddress}' is only partly cleared: every table reporting a count above really was cleared, " +
                "and every table marked FAILED is in an unknown state -- the delete may not have run at all, or may " +
                "have committed with only the reply lost. Re-run this command once the database is reachable; it is " +
                "safe to run again, and removing zero rows is not an error.");
            return 1;
        }

        if (firewall.Removed + blocked.Removed == 0)
        {
            Console.WriteLine(
                $"IP '{ipAddress}' was NOT blocked by either table, so this command changed nothing. If logins from " +
                "that IP are still refused, the cause is one of these, none of which this command touches:");
            Console.WriteLine(
                "  - the account is grade>=1 and its IP is not on admin.GmAllowlists (run " + AllowGmIpKeyword + ");");
            Console.WriteLine(
                "  - the in-memory per-IP connection cap or protocol-violation counter in a running server (both " +
                "decay on their own; a restart clears them);");
            Console.WriteLine(
                "  - the in-memory login throttle after repeated wrong passwords (decays on its own);");
            Console.WriteLine(
                "  - a ban on the account itself (admin.Bans), which is not an IP block at all.");
            Console.WriteLine(
                "Also check the spelling: rows are matched on the exact string, and IPv6 forms are not normalised.");
            return 0;
        }

        Console.WriteLine(
            "Running servers re-read admin.FirewallRules every 2s and admin.BlockedIps on every login attempt. The " +
            "in-memory per-IP connection cap is unaffected: it decays on its own as that IP's sockets close. " +
            $"Flood-guard rows also lapse by themselves {FirewallRuleRepository.AutoBlockDuration.TotalHours:0.#}h " +
            "after they are written; a rule an operator added with no TTL never lapses, and this command is the " +
            "only thing that removes it.");
        return 0;
    }

    private static async Task<(int Removed, string? Error)> TryRemoveAsync(Func<ValueTask<int>> removeAsync)
    {
        try
        {
            return (await removeAsync(), null);
        }
        catch (Exception ex)
        {
            return (0, ex.Message);
        }
    }

    private static void ReportRemoval(string table, string ipAddress, (int Removed, string? Error) outcome)
    {
        var label = $"{table + ":",-21}";

        Console.WriteLine(outcome.Error is null
            ? $"{label}removed {outcome.Removed} row(s) for IP '{ipAddress}'."
            : $"{label}FAILED, removal count unknown for IP '{ipAddress}': {outcome.Error}");
    }

    private static AccountRepository? CreateAccountRepositoryOrNull()
    {
        var db = CreateDbContextOrNull();
        return db is null ? null : new AccountRepository(db);
    }

    private static GmAllowlistRepository? CreateGmAllowlistRepositoryOrNull()
    {
        var db = CreateDbContextOrNull();
        return db is null ? null : new GmAllowlistRepository(db);
    }

    private static ICaeriusNetDbContext? CreateDbContextOrNull()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__FenrirDb") ??
            Environment.GetEnvironmentVariable("FENRIR_DB_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "No connection string. Set ConnectionStrings__FenrirDb (Aspire convention) or FENRIR_DB_CONNECTION_STRING.");
            return null;
        }

        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(connectionString)
            .Build();

        return services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
    }
}
