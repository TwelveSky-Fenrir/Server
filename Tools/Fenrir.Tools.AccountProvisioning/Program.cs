using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

// Seeds a login account. The legacy client (BuildEU33) has no sign-up screen, so this CLI is the only
// way to provision an account for M1 -- it never embeds a real credential, it only accepts one as an argument.

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

// LoginRequest.Password is [FixedString(33)] on the wire: a null-terminated Latin1 char[33], so the real
// BuildEU33 client can physically send at most 32 usable characters. Anything longer hashes and stores fine
// here but produces an account the legacy client can never authenticate -- fail loudly now instead of leaving
// a silently unusable account to be discovered later at login.
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
    // usp_Account_Create raises THROW 50101 (admin.ErrorCatalog, duplicate LoginName) -- report it as a
    // clear message rather than letting the raw SqlException stack trace reach the operator's console.
    Console.Error.WriteLine($"Could not create account '{loginName}': {ex.Message}");
    return 1;
}
