using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Microsoft.Extensions.DependencyInjection;


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
