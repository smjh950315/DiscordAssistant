using System.Data;
using System.Reflection;
using System.Text;
using Dapper;
using Discord.WebSocket;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Commands;

public static class BasicCommands
{
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
