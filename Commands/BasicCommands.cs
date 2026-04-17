namespace DiscordAssistant.Commands;

public static class BasicCommands
{
    public static IReadOnlyCollection<ICommand> All { get; } =
    [
        PingCommand,
    ];

    public static ICommand PingCommand => CommandBase.BuildCommand(
        "ping",
        "Check whether the bot is awake.",
        async command =>
        {
            await command.RespondAsync("Pong!");
        });
}
