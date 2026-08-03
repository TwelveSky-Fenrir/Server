using Fenrir.Tools.DbMigrator;
using Fenrir.Tools.DbMigrator.Accounts;

if (args.Length > 0)
{
    var command = args[0];
    var commandArgs = args[1..];

    if (command == AccountCommands.CreateKeyword)
    {
        if (commandArgs.Length != 2)
        {
            AccountCommands.PrintUsage();
            return 1;
        }

        return await AccountCommands.CreateAsync(commandArgs[0], commandArgs[1]);
    }

    if (command == AccountCommands.GrantGmKeyword)
    {
        if (commandArgs.Length != 2)
        {
            AccountCommands.PrintUsage();
            return 1;
        }

        return await AccountCommands.GrantGmAsync(commandArgs[0], commandArgs[1]);
    }

    if (command == AccountCommands.AllowGmIpKeyword)
    {
        if (commandArgs.Length != 1)
        {
            AccountCommands.PrintUsage();
            return 1;
        }

        return await AccountCommands.AllowGmIpAsync(commandArgs[0]);
    }

    if (command == AccountCommands.ClearIpBlockKeyword)
    {
        if (commandArgs.Length != 1)
        {
            AccountCommands.PrintUsage();
            return 1;
        }

        return await AccountCommands.ClearIpBlockAsync(commandArgs[0]);
    }

    if (command == LegacyImportCommand.Keyword)
        return LegacyImportCommand.Run(commandArgs);
}

var options = MigratorOptions.FromEnvironment(args);

if (options is null)
{
    Console.Error.WriteLine(
        "No connection string. Set ConnectionStrings__FenrirDb (Aspire convention), FENRIR_DB_CONNECTION_STRING, or pass one as the first argument.");
    return 1;
}

return await MigrationRunner.RunAsync(options);
