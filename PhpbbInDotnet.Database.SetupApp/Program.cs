using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SetupApp
{
    class Program
    {
        static Regex NAME_REGEX = new Regex("^[^\\/?%*:|\"<>. ;]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
                    await connection.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS {dbName}");
                    Console.WriteLine("Done!");
                    Console.WriteLine();
                }

                await connection.ChangeDataBaseAsync(dbName);

                Console.WriteLine($"{(newInstall ? "Creating" : "Updating")} tables...");
                var upsertTables = await File.ReadAllTextAsync(Path.Combine("InstallScripts", newInstall ? "CreateTables.sql" : "UpdateTables.sql"));
                await connection.ExecuteAsync(upsertTables);
                Console.WriteLine("Done!");
                Console.WriteLine();

                Console.WriteLine("Creating stored procedures... ");
                foreach (var spFile in Directory.GetFiles("StoredProcedures"))
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
                    using var utils = new CommonUtils(null, null, null, null);
                    var users = await connection.QueryAsync("SELECT user_id, username, username_clean FROM phpbb_users");
                    foreach (var user in users)
                    {
                        await connection.ExecuteAsync("UPDATE phpbb_users SET username_clean = @clean WHERE user_id = @id", new { clean = utils.CleanString(user.username), id = user.user_id });
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

        static string ReadInput(string message, string[] allowedValues = null, Regex regex = null)
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
            if (regex != null && !regex.IsMatch(input))
            {
                return ReadInput(message, null, regex);
            }
            else if (!(allowedValues?.Contains(input, StringComparer.InvariantCultureIgnoreCase) ?? true))
            {
                return ReadInput(message, allowedValues, null);
            }
            return input;
        }
    }
}
