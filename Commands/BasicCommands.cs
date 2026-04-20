using System.Reflection;
using Discord.WebSocket;

namespace DiscordAssistant.Commands;

public static class BasicCommands
{

    public static IEnumerable<ICommand> All => new ICommand[]
    {
        CommandBase.BuildCommand(typeof(BasicCommands).GetMethod(nameof(Ping), BindingFlags.Public | BindingFlags.Static)!),
        CommandBase.BuildCommand(typeof(BasicCommands).GetMethod(nameof(Roll), BindingFlags.Public | BindingFlags.Static)!),
    };

    public static async Task Ping(SocketSlashCommand command)
    {
        await command.RespondAsync("Pong!");
    }
    public static async Task Roll(SocketSlashCommand command, int sides)
    {
        var random = new Random();
        var result = random.Next(1, sides + 1);
        await command.RespondAsync($"You rolled a {result} on a {sides}-sided die.");
    }
}
