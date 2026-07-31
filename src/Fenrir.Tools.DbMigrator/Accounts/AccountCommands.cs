using Fenrir.Security.Credentials;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Tools.DbMigrator.Accounts;

public static class AccountCommands
{
    public const string CreateKeyword = "create-account";
    public const string GrantGmKeyword = "grant-gm";
    public const string AllowGmIpKeyword = "allow-gm-ip";

    private const int MaxPasswordLength = 32;

    public static void PrintUsage()
    {
        Console.Error.WriteLine($"usage: Fenrir.Tools.DbMigrator {CreateKeyword} <loginName> <password>");
        Console.Error.WriteLine(
            $"       Fenrir.Tools.DbMigrator {GrantGmKeyword} <loginName> <grade>   (grade: 1=Basic, 10=Elevated, 100=Admin, 0=revoke)");
        Console.Error.WriteLine(
            $"       Fenrir.Tools.DbMigrator {AllowGmIpKeyword} <ipAddress>       (required for ANY grade>=1 account to log in at all)");
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
