using Dapper;
using Discord;
using Discord.WebSocket;
using DiscordAssistant.Commands;
using DiscordAssistant.Workers;

namespace DiscordAssistant;

internal sealed class Program
{
    private readonly DiscordSocketClient _client;
    private readonly CommandRegistry commandRegistry;
    private BriLeithNotifier? briLeithNotifier;
    private string? _connectionString;

    private Program()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
        };

        _client = new DiscordSocketClient(config);
        List<ICommand> commands = new();
        var basicCommands = BasicCommands.All;
        commands.AddRange(basicCommands);
        var brileithCommands = BrileithCommands.All;
        commands.AddRange(brileithCommands);
        Console.WriteLine("Register commands: " + string.Join(',', commands.Select(c => c.Name)));

        commandRegistry = new CommandRegistry(commands);
        _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (!string.IsNullOrEmpty(_connectionString))
        {
            using (var conn = new Npgsql.NpgsqlConnection(_connectionString))
            {
                conn.Execute(Utilities.GetDatabaseInitializeSql());
            }
        }
        BasicCommands._connection = new Npgsql.NpgsqlConnection(_connectionString);
        var g27ChannelIdText = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
        if (ulong.TryParse(g27ChannelIdText, out var g27ChannelId))
        {
            briLeithNotifier = new BriLeithNotifier(_client, g27ChannelId, 21, 00, 5);
        }
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.SlashCommandExecuted += SlashCommandExecutedAsync;
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
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        var w = new BrileithRecruitWorker(_client, () => new Npgsql.NpgsqlConnection(_connectionString), 1);
        w.Start();
        //await (briLeithNotifier?.SetSpecificMessage(1495771204026634471) ?? Task.CompletedTask);
        briLeithNotifier?.Start();
        await Task.Delay(Timeout.Infinite);
    }

    private static Task LogAsync(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"Logged in as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
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
            var guild = _client.GetGuild(guildId);
            if (guild is null)
            {
                Console.WriteLine($"Could not find guild {guildId}; syncing commands globally instead.");
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
                return;
            }

            await guild.BulkOverwriteApplicationCommandAsync(commands);
            Console.WriteLine($"Synced slash commands to guild {guildId}.");
            return;
        }

        await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
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
