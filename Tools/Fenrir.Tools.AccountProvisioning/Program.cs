using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Microsoft.Extensions.DependencyInjection;

// The legacy client (BuildEU33) has no sign-up screen, so this CLI is the only way to provision an account.
// "grant-gm" exists because, until it was added, there was no path anywhere in this repo (no seed script,
// no other tool, nothing documented) to set AccountGrade (legacy uUserSort) above 0 -- making the GM
// command tier work (Application/Fenrir.Application.Game.Services/Gm/*) unreachable/untestable end-to-end.
//
// "allow-gm-ip" exists for the same reason, one layer down: LoginService.LoginAsync's "# GM Enable Login IP #"
// gate (Server/ts25login/S04_MyWork02.cpp:192-201) additionally requires any AccountGrade>=1 account to log in
// from an IP listed in admin.GmAllowlist -- deliberately left unseeded (see admin.GmAllowlist.sql's own remarks:
// the legacy dump's only row, 127.0.0.1, was NOT ported as seed data). Without this command, granting GM on an
// account whose IP isn't already allowlisted silently locks that account out of login entirely (LoginService
// logs "GM-tier account ... attempted login from non-allowlisted IP" at Warning, but nothing surfaces that to
// whoever just ran grant-gm) -- run this right after grant-gm for any account/IP you intend to actually log in
// with, not just to unlock GM commands once already in-world.

var command = args.Length > 0 ? args[0] : null;

if (string.Equals(command, "create", StringComparison.OrdinalIgnoreCase) && args.Length == 3)
    return await RunCreateAsync(args[1], args[2]);

if (string.Equals(command, "grant-gm", StringComparison.OrdinalIgnoreCase) && args.Length == 3)
    return await RunGrantGmAsync(args[1], args[2]);

if (string.Equals(command, "allow-gm-ip", StringComparison.OrdinalIgnoreCase) && args.Length == 2)
    return await RunAllowGmIpAsync(args[1]);

Console.Error.WriteLine("Usage: Fenrir.Tools.AccountProvisioning create <loginName> <password>");
Console.Error.WriteLine(
    "       Fenrir.Tools.AccountProvisioning grant-gm <loginName> <grade>   (grade: 1=Basic, 10=Elevated, 100=Admin, 0=revoke)");
Console.Error.WriteLine(
    "       Fenrir.Tools.AccountProvisioning allow-gm-ip <ipAddress>       (required for ANY grade>=1 account to log in at all)");
return 1;

async Task<int> RunCreateAsync(string loginName, string password)
{
    if (string.IsNullOrWhiteSpace(password))
    {
        Console.Error.WriteLine("Password must not be empty.");
        return 1;
    }

    // LoginRequest.Password is [FixedString(33)] on the wire (null-terminated Latin1 char[33]): 32 usable characters max.
    const int maxPasswordLength = 32;
    if (password.Length > maxPasswordLength)
    {
        Console.Error.WriteLine(
            $"Password must be at most {maxPasswordLength} characters (legacy client wire limit); got {password.Length}.");
        return 1;
    }

    var repository = CreateRepository();
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
        // usp_Account_Create raises THROW 50101 on duplicate LoginName.
        Console.Error.WriteLine($"Could not create account '{loginName}': {ex.Message}");
        return 1;
    }
}

async Task<int> RunGrantGmAsync(string loginName, string gradeText)
{
    if (!short.TryParse(gradeText, out var grade) || grade < 0)
    {
        Console.Error.WriteLine("Grade must be a non-negative integer (0=revoke, 1=Basic, 10=Elevated, 100=Admin).");
        return 1;
    }

    var repository = CreateRepository();
    if (repository is null)
        return 1;

    try
    {
        await repository.SetGradeAsync(loginName, grade, CancellationToken.None);
        Console.WriteLine($"AccountGrade for '{loginName}' set to {grade}.");
        if (grade >= 1)
            Console.WriteLine(
                "Note: any grade>=1 account can only log in from an IP on admin.GmAllowlist -- run " +
                "'allow-gm-ip <yourIp>' now, or this account will be rejected at login.");
        return 0;
    }
    catch (Exception ex)
    {
        // usp_Account_SetGrade raises THROW 50102 if LoginName does not exist.
        Console.Error.WriteLine($"Could not set AccountGrade for '{loginName}': {ex.Message}");
        return 1;
    }
}

async Task<int> RunAllowGmIpAsync(string ipAddress)
{
    var repository = CreateGmAllowlistRepository();
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
        // usp_GmAllowlist_Add raises THROW 50304 if the IP is already allowlisted.
        Console.Error.WriteLine($"Could not add IP '{ipAddress}' to the GM allowlist: {ex.Message}");
        return 1;
    }
}

AccountRepository? CreateRepository()
{
    var db = CreateDbContextOrNull();
    return db is null ? null : new AccountRepository(db);
}

GmAllowlistRepository? CreateGmAllowlistRepository()
{
    var db = CreateDbContextOrNull();
    return db is null ? null : new GmAllowlistRepository(db);
}

ICaeriusNetDbContext? CreateDbContextOrNull()
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
