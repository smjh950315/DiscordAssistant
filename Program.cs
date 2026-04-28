using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Discord;
using Discord.WebSocket;
using DiscordAssistant.Commands;
using DiscordAssistant.DBModels;
using DiscordAssistant.Workers;

namespace DiscordAssistant;

internal sealed class Program
{
    private readonly DiscordSocketClient _client;
    private readonly CommandRegistry commandRegistry;
    private BriLeithNotifier? briLeithNotifier;
    private string? _connectionString;
    private Func<IDbConnection>? _connectionBuilder;

    IEnumerable<IWorker> LoadWorkers()
    {
        using var conn = Utilities.GetConnection();
        if (conn == null)
            return [];
        var dbSchedules = conn.Query<Schedule>("select * from schedule");
        var scGroups = dbSchedules.GroupBy(s => s.worker_type ?? "Unknown").Select(s => new
        {
            type = s.Key,
            schedules = s.ToArray()
        });
        List<IWorker> workers = [];
        foreach (var group in scGroups)
        {
            if (group.type == "Unknown")
            {
                workers.AddRange(group.schedules.Select(s => new ScheduleWorker(_client, s.id)).Select(s => (IWorker)s));
            }
            else if (group.type == "Brileth")
            {
                workers.AddRange(group.schedules.Select(s => new BrileithRecruitWorker(_client, s.id, 1)).Select(s => (IWorker)s));
            }
        }
        return workers;
    }

    private Program()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
        };

        _client = new DiscordSocketClient(config);
        List<ICommand> commands = new();
        var basicCommands = Utilities.GetCommands(typeof(BasicCommands));
        var brileithCommands = Utilities.GetCommands(typeof(BrileithCommands));
        var notifyCommands = Utilities.GetCommands(typeof(NotifyCommands));
        commands.AddRange(basicCommands);
        commands.AddRange(brileithCommands);
        commands.AddRange(notifyCommands);
        Console.WriteLine("Register commands: " + string.Join(',', commands.Select(c => c.Name)));

        commandRegistry = new CommandRegistry(commands);
        Utilities.SetConnectionString(Environment.GetEnvironmentVariable("CONNECTION_STRING"));

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
        // var w = new BrileithRecruitWorker(_client, 1);
        // w.Start();

        CancellationTokenSource cancellationTokenSource = new();
        var workerListener = Task.Run(async () =>
        {
            var token = cancellationTokenSource.Token;
            ConcurrentDictionary<long, IWorker> workers = [];
            Task? remover = null;
            while (!token.IsCancellationRequested)
            {
                using var conn = Utilities.GetConnection();
                if (conn != null)
                {
                    var workIdList = conn.Query<long>("select id from schedule");
                    var shouldStartWorkIdList = workIdList.Where(i => !workers.Keys.Contains(i)).ToArray();
                    var shouldStartWorks = conn.Query<Schedule>("select * from schedule where id = ANY(@_ids)", new
                    {
                        _ids = shouldStartWorkIdList
                    });

                    if (remover == null || remover.IsCompleted)
                    {
                        // get id not exists in db
                        var toStopWorkers = workers.Where(kvp => !workIdList.Contains(kvp.Key));
                        if (toStopWorkers.Count() != 0)
                            remover = Task.Run(async () =>
                            {
                                foreach (var work in toStopWorkers)
                                {
                                    await work.Value.StopAsync();
                                    Console.WriteLine($"[{DateTime.Now}] Stop task of schedule(id={work.Key})");
                                    workers.Remove(work.Key, out _);
                                }
                            });
                    }

                    foreach (var work in shouldStartWorks)
                    {
                        IWorker worker;
                        if (work.worker_type?.Contains("brileith", StringComparison.OrdinalIgnoreCase) ?? false)
                        {
                            worker = new BrileithRecruitWorker(_client, work.id, 1);
                            workers.TryAdd(work.id, worker);
                        }
                        else
                        {
                            worker = new ScheduleWorker(_client, work.id);
                            workers.TryAdd(work.id, worker);
                        }
                        Console.WriteLine($"[{DateTime.Now}] Start a task of schedule(id={work.id})");
                        worker.Start();
                    }
                }
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        });

        // var test = new ScheduleWorker(_client, 2);
        // test.Start();
        // briLeithNotifier?.Start();
        await Task.Delay(Timeout.Infinite);
        await cancellationTokenSource.CancelAsync();
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
    public static void Load(string path = "D:\\podman\\localshared\\DiscordAssistant\\.env")
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
