using Discord;
using Discord.WebSocket;
using DiscordAssistant.Commands;

namespace DiscordAssistant;

internal sealed class Program
{
    private readonly DiscordSocketClient client;
    private readonly CommandRegistry commandRegistry;

    private Program()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
        };

        client = new DiscordSocketClient(config);
        commandRegistry = new CommandRegistry(BasicCommands.All);

        client.Log += LogAsync;
        client.Ready += ReadyAsync;
        client.SlashCommandExecuted += SlashCommandExecutedAsync;
    }

    private static async Task Main()
    {
        DotEnv.Load();

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Set DISCORD_TOKEN in your environment or .env file before running the bot.");
        }

        var program = new Program();
        await program.RunAsync(token);
    }

    private async Task RunAsync(string token)
    {
        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
        await Task.Delay(Timeout.Infinite);
    }

    private static Task LogAsync(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
        await RegisterSlashCommandsAsync();
    }

    private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
    {
        await commandRegistry.ExecuteAsync(command);
    }

    private async Task RegisterSlashCommandsAsync()
    {
        var commands = commandRegistry.BuildSlashCommands();

        var guildIdText = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID");
        if (ulong.TryParse(guildIdText, out var guildId))
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
            {
                Console.WriteLine($"Could not find guild {guildId}; syncing commands globally instead.");
                await client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
                return;
            }

            await guild.BulkOverwriteApplicationCommandAsync(commands);
            Console.WriteLine($"Synced slash commands to guild {guildId}.");
            return;
        }

        await client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
        Console.WriteLine("Synced global slash commands.");
    }
}

internal static class DotEnv
{
    public static void Load(string path = ".env")
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            var key = parts[0].Trim();
            var value = parts[1].Trim().Trim('"', '\'');

            if (!string.IsNullOrWhiteSpace(key))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
