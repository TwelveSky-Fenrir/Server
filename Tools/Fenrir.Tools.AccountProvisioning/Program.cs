using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Microsoft.Extensions.DependencyInjection;

// The legacy client (BuildEU33) has no sign-up screen, so this CLI is the only way to provision an account.
// "grant-gm" exists because, until it was added, there was no path anywhere in this repo (no seed script,
// no other tool, nothing documented) to set AccountGrade (legacy uUserSort) above 0 -- making the GM
// command tier work (Application/Fenrir.Application.Game.Services/Gm/*) unreachable/untestable end-to-end.

var command = args.Length > 0 ? args[0] : null;

if (string.Equals(command, "create", StringComparison.OrdinalIgnoreCase) && args.Length == 3)
{
    return await RunCreateAsync(args[1], args[2]);
}

if (string.Equals(command, "grant-gm", StringComparison.OrdinalIgnoreCase) && args.Length == 3)
{
    return await RunGrantGmAsync(args[1], args[2]);
}

Console.Error.WriteLine("Usage: Fenrir.Tools.AccountProvisioning create <loginName> <password>");
Console.Error.WriteLine(
    "       Fenrir.Tools.AccountProvisioning grant-gm <loginName> <grade>   (grade: 1=Basic, 10=Elevated, 100=Admin, 0=revoke)");
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
        return 0;
    }
    catch (Exception ex)
    {
        // usp_Account_SetGrade raises THROW 50102 if LoginName does not exist.
        Console.Error.WriteLine($"Could not set AccountGrade for '{loginName}': {ex.Message}");
        return 1;
    }
}

AccountRepository? CreateRepository()
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

    var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
    return new AccountRepository(db);
}
