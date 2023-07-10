using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Domain.Extensions;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Database.SetupApp
{
    class Program
    {
        static Regex NAME_REGEX = new("^[^\\/?%*:|\"<>. ;]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("PhpbbInDotnet database installation wizard");
                Console.WriteLine("Type your answers when prompted");
                Console.WriteLine();

                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var connectionString = config["ConnectionString"];
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("Missing DB connection string. Check appsettings.json!");
                }
                var dbType = Enum.TryParse<DatabaseType>(config["DatabaseType"], out var val) ? val : throw new InvalidOperationException("Wrong DatabaseType value. Check appsettings.json!");

                var newInstall = StringComparer.InvariantCultureIgnoreCase.Equals(ReadInput(message: "Create database (new installation), or update existing?", allowedValues: new[] { "create", "update" }), "create");
                Console.WriteLine();
                if (!newInstall)
                {
                    var hasBackup = StringComparer.InvariantCultureIgnoreCase.Equals(ReadInput("Do you have a full backup of the database?", allowedValues: new[] { "Y", "N" }), "Y");
                    if (!hasBackup)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Please create a backup of the database and try again. Program will exit.");
                        return;
                    }
                }
                Console.WriteLine();
                var dbName = ReadInput(message: "Database name? (Will be created if not existing already)", regex: NAME_REGEX);

                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                if (newInstall)
                {
                    Console.WriteLine();
                    Console.WriteLine("Creating database...");
                    await connection.ExecuteAsync(dbType switch
                    {
                        DatabaseType.SqlServer => $"IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'DataBase) CREATE DATABASE [{dbName}];",
                        DatabaseType.MySql => $"CREATE DATABASE IF NOT EXISTS {dbName};",
                        _ => throw new ArgumentException("Invalid database type in configuration.")
                    });
                    Console.WriteLine("Done!");
                    Console.WriteLine();
                }

                await connection.ChangeDataBaseAsync(dbName);

                Console.WriteLine($"{(newInstall ? "Creating" : "Updating")} tables...");
                string path;
                if (newInstall)
                {
                    path = Path.Combine("InstallScripts", dbType.ToString(), "CreateTables.sql");
                }
                else
                {
                    path = Path.Combine("InstallScripts", "UpdateTables.sql");
                }
                var upsertTables = await File.ReadAllTextAsync(path);
                await connection.ExecuteAsync(upsertTables);
                Console.WriteLine("Done!");
                Console.WriteLine();

                Console.WriteLine("Creating stored procedures... ");
                foreach (var spFile in Directory.GetFiles(Path.Combine("StoredProcedures", dbType.ToString())))
                {
                    var sp = await File.ReadAllTextAsync(spFile);
                    await connection.ExecuteAsync(sp);
                }
                Console.WriteLine("Done!");
                Console.WriteLine();

                if (newInstall)
                {
                    Console.WriteLine("Inserting initial data...");
                    var dataSQL = await File.ReadAllTextAsync(Path.Combine("InstallScripts", "InsertInitialData.sql"));
                    await connection.ExecuteAsync(dataSQL);
                    Console.WriteLine("Done!");
                }
                else
                {
                    Console.WriteLine("Updating users table...");
                    var users = await connection.QueryAsync("SELECT user_id, username, user_email FROM phpbb_users");
                    foreach (var groups in users.Indexed().GroupBy(key => key.Index / 10, element => element.Item))
                    {
                        foreach (var user in groups)
                        {
                            await connection.ExecuteAsync(
                                "UPDATE phpbb_users SET username_clean = @clean, user_email_hash = @hash WHERE user_id = @id",
                                new
                                {
                                    clean = StringUtility.CleanString(user.username),
                                    hash = HashUtility.ComputeCrc64Hash((string)user.user_email),
                                    id = user.user_id
                                });
                        }
                    }
                    Console.WriteLine("Done!");
                    Console.WriteLine();
                }

                Console.WriteLine();
                Console.WriteLine("The setup has completed successfully!");
            }
            catch (Exception ex)
            {
                using var log = new LoggerConfiguration().WriteTo.Console(outputTemplate: "[{Level}] {Message}{NewLine:l}{Exception:l}").CreateLogger();
                log.Error(ex, "An error occured");
            }
        }

        static string ReadInput(string message, string[]? allowedValues = null, Regex? regex = null)
        {
            Console.WriteLine(message.Trim());
            if (regex != null)
            {
                Console.WriteLine($"Should match pattern \"{regex}\"");
            }
            else if (allowedValues?.Any() ?? false)
            {
                Console.WriteLine($"[{string.Join(" / ", allowedValues)}]");
            }

            var input = Console.ReadLine();
            if (regex != null && !regex.IsMatch(input!))
            {
                return ReadInput(message, null, regex);
            }
            else if (!(allowedValues?.Contains(input, StringComparer.InvariantCultureIgnoreCase) ?? true))
            {
                return ReadInput(message, allowedValues, null);
            }
            return input!;
        }
    }
}
