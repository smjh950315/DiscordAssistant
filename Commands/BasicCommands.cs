using System.Data;
using System.Reflection;
using System.Text;
using Dapper;
using Discord.WebSocket;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Commands;

public static class BasicCommands
{
    private static readonly IReadOnlyDictionary<string, int> WeekDayLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["sun"] = 0,
        ["sunday"] = 0,
        ["0"] = 0,
        ["7"] = 0,
        ["mon"] = 1,
        ["monday"] = 1,
        ["1"] = 1,
        ["tue"] = 2,
        ["tues"] = 2,
        ["tuesday"] = 2,
        ["2"] = 2,
        ["wed"] = 3,
        ["wednesday"] = 3,
        ["3"] = 3,
        ["thu"] = 4,
        ["thur"] = 4,
        ["thurs"] = 4,
        ["thursday"] = 4,
        ["4"] = 4,
        ["fri"] = 5,
        ["friday"] = 5,
        ["5"] = 5,
        ["sat"] = 6,
        ["saturday"] = 6,
        ["6"] = 6,
    };

    public static IEnumerable<ICommand> All
    {
        get
        {
            return typeof(BasicCommands)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetParameters().Count() != 0 && m.GetParameters().First().ParameterType == typeof(SocketSlashCommand))
                .Select(CommandBase.BuildCommand);
        }
    }

    static public IDbConnection? _connection { get; set; }

    public static async Task ping(SocketSlashCommand command)
    {
        await command.RespondAsync("Pong!");
    }

    public static async Task roll(SocketSlashCommand command, int sides)
    {
        var random = new Random();
        var result = random.Next(1, sides + 1);
        await command.RespondAsync($"You rolled a {result} on a {sides}-sided die.");
    }
}
