using System.Reflection;
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

    public required MethodInfo HandlerMethod { get; set; }

    public SlashCommandProperties BuildSlashCommand()
    {
        return CommandHelper.BuildSlashCommandFromFunction(HandlerMethod);
    }

    public Task ExecuteAsync(SocketSlashCommand command)
    {
        var parameters = HandlerMethod.GetParameters();
        var args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(SocketSlashCommand))
            {
                args[i] = command;
            }
            else
            {
                var option = command.Data.Options.FirstOrDefault(o => o.Name == parameters[i].Name!.ToLower());
                if (option == null)
                {
                    throw new ArgumentException($"Missing required option {parameters[i].Name} for command {Name}.");
                }
                if (option.Value == null)
                {
                    throw new ArgumentException($"Option {parameters[i].Name} for command {Name} cannot be null.");
                }
                if (option.Value.GetType() != parameters[i].ParameterType)
                {
                    args[i] = Convert.ChangeType(option.Value, parameters[i].ParameterType);
                }
                else
                {
                    args[i] = option.Value!;
                }                
            }
        }
        return (Task)HandlerMethod.Invoke(null, args)!;
    }

    public static ICommand BuildCommand(MethodInfo method)
    {
        var commandName = method.Name.ToLower();
        var description = $"Executes the {method.Name} function.";
        return new CommandBase
        {
            Name = commandName,
            Description = description,
            HandlerMethod = method
        };
    }
}
