using Discord;
using Discord.WebSocket;

namespace DiscordAssistant;

public interface ICommand
{
    string Name { get; }

    SlashCommandProperties BuildSlashCommand();

    Task ExecuteAsync(SocketSlashCommand command);
}

public class CommandBase : ICommand
{
    public required string Name { get; set; }

    public required string Description { get; set; }

    public required Func<SocketSlashCommand, Task> Handler { get; set; }

    public SlashCommandProperties BuildSlashCommand()
    {
        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription(Description)
            .Build();
    }

    public Task ExecuteAsync(SocketSlashCommand command)
    {
        return Handler(command);
    }

    public static ICommand BuildCommand(
        string name,
        string description,
        Func<SocketSlashCommand, Task> executeFunc)
    {
        return new CommandBase
        {
            Name = name,
            Description = description,
            Handler = executeFunc,
        };
    }
}
