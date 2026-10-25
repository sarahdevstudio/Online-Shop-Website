using Microsoft.AspNetCore.Identity;
using Shop.Infrastructure;
using Shop.Infrastructure.Accounts;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Persistence;

namespace Shop.Api.Commands;

/// <summary>
/// <c>dotnet Shop.Api.dll create-admin --email &lt;e-mail&gt;</c>: creates the first (or another) admin account.
/// The password is NEVER a command-line argument (it would land in shell history and process listings): it comes from the
/// <c>SHOP_ADMIN_PASSWORD</c> environment variable or a hidden interactive prompt that asks twice.
/// </summary>
public static class CreateAdminCommand
{
    public const string PasswordVariable = "SHOP_ADMIN_PASSWORD";

    public const string Usage =
        "Usage: create-admin --email <e-mail>\n" +
        $"The password is read from the {PasswordVariable} environment variable, or typed at a hidden prompt. " +
        "It is never accepted as an argument. At least 12 characters.";

    /// <summary>Parses the arguments; returns an error text instead of the e-mail when they are not acceptable.</summary>
    public static (string? Email, string? Error) ParseArguments(IReadOnlyList<string> args)
    {
        string? email = null;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var name = arg.Split('=')[0].ToLowerInvariant();
            if (name is "--password" or "-p" or "--pass" or "--pwd" or "--secret")
            {
                return (null, $"Passwords are never accepted as command-line arguments. Use {PasswordVariable} or the hidden prompt.");
            }

            if (name == "--email")
            {
                email = arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : i + 1 < args.Count ? args[++i] : null;
            }
            else
            {
                return (null, $"Unknown argument '{arg}'.");
            }
        }

        return string.IsNullOrWhiteSpace(email) ? (null, "--email is required.") : (email.Trim(), null);
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var (email, error) = ParseArguments(args);
        if (email is null)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrEmpty(password))
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine($"No terminal to ask for the password: set {PasswordVariable} or run this in an interactive shell.");
                return 2;
            }

            password = ReadHidden("Password: ");
            if (password != ReadHidden("Repeat password: "))
            {
                Console.Error.WriteLine("The passwords do not match.");
                return 2;
            }
        }

        var builder = WebApplication.CreateBuilder([]);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Only the account and database services are used here; the web API's own services (authentication, current user) are
        // not registered, so the Development-time "validate everything on build" check would complain about them.
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();

        // Same startup work as the API (migrations when enabled, roles, settings row), so the Admin role exists.
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);

        var result = await AdminAccounts.CreateAdminAsync(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), email, password, TimeProvider.System);
        if (!result.IsSuccess)
        {
            var details = result.Error!.ValidationErrors is { } errors ? string.Join(" ", errors.SelectMany(e => e.Value)) : result.Error.Message;
            Console.Error.WriteLine($"Could not create the admin: {details}");
            return 1;
        }

        Console.WriteLine($"Admin account created for {email}.");
        return 0;
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
    }
}
