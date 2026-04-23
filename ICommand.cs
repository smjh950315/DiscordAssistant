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
        return BuildSlashCommandFromFunction(HandlerMethod);
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

    static SlashCommandProperties BuildSlashCommandFromFunction(MethodInfo method)
    {
        var commandName = method.Name.ToLower();
        var description = $"Executes the {method.Name} function.";
        var parameters = method.GetParameters();
        if (parameters.Length < 1 || parameters[0].ParameterType != typeof(SocketSlashCommand))
        {
            throw new ArgumentException($"Method {method.Name} must have a SocketSlashCommand as the first parameter.");
        }
        var builder = new SlashCommandBuilder().WithName(commandName).WithDescription(description);
        for (int i = 1; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var optionBuilder = new SlashCommandOptionBuilder()
                .WithName(param.Name!.ToLower())
                .WithDescription($"The {param.Name} parameter.")
                .WithRequired(true);

            if (param.ParameterType == typeof(int))
            {
                optionBuilder.WithType(ApplicationCommandOptionType.Integer);
            }
            else if (param.ParameterType == typeof(string))
            {
                optionBuilder.WithType(ApplicationCommandOptionType.String);
            }
            else
            {
                throw new ArgumentException($"Unsupported parameter type {param.ParameterType} in method {method.Name}.");
            }

            builder.AddOption(optionBuilder);
        }
        return builder.Build();
    }
}
