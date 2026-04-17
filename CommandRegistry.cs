using Discord;
using Discord.WebSocket;

namespace DiscordAssistant;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, ICommand> commands;

    public CommandRegistry(IEnumerable<ICommand> commands)
    {
        this.commands = commands.ToDictionary(
            command => command.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public SlashCommandProperties[] BuildSlashCommands()
    {
        return commands.Values
            .Select(command => command.BuildSlashCommand())
            .ToArray();
    }

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        if (!commands.TryGetValue(command.CommandName, out var registeredCommand))
        {
            await command.RespondAsync("I do not know that command yet.", ephemeral: true);
            return;
        }

        await registeredCommand.ExecuteAsync(command);
    }
}
