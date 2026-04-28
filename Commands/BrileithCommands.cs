using System.Data;
using System.Reflection;
using System.Text;
using Dapper;
using Discord.WebSocket;
using DiscordAssistant.Attributes;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Commands;

public static class BrileithCommands
{
    public static IEnumerable<ICommand> All
    {
        get
        {
            return typeof(BrileithCommands)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetParameters().Count() != 0 && m.GetParameters().First().ParameterType == typeof(SocketSlashCommand))
                .Select(CommandBase.BuildCommand);
        }
    }

    [CommandDescription("設定招募時間與訊息")]
    public static async Task brileith_set(SocketSlashCommand command, int hour, int minute, 
        string message,
        [CommandParameter(false, "0到6 分別代表週日到周六，逗號(',')分隔或是留空表示全部")] string weekDays)
    {
        await NotifyCommands.notifier_set(command, "brileith", hour, minute, message, weekDays, "brileith");
    }
}