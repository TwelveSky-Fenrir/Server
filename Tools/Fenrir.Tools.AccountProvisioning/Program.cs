using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

// The legacy client (BuildEU33) has no sign-up screen, so this CLI is the only way to provision an account.

if (args.Length != 3 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: Fenrir.Tools.AccountProvisioning create <loginName> <password>");
    return 1;
}

var loginName = args[1];
var password = args[2];

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

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__FenrirDb") ??
    Environment.GetEnvironmentVariable("FENRIR_DB_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "No connection string. Set ConnectionStrings__FenrirDb (Aspire convention) or FENRIR_DB_CONNECTION_STRING.");
    return 1;
}

var services = CaeriusNetBuilder
    .Create(new ServiceCollection())
    .WithSqlServer(connectionString)
    .Build();

var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
var repository = new AccountRepository(db);

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
