using System.Reflection;
using DbUp;

var connectionString =
    args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONN")
    ?? throw new InvalidOperationException(
        "Connection string não informada. Passe como argumento ou defina POSTGRES_CONN.");

EnsureDatabase.For.PostgresqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();

if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Migração falhou: {result.Error}");
    Console.ResetColor();
    return -1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Migrações aplicadas com sucesso.");
Console.ResetColor();
return 0;
