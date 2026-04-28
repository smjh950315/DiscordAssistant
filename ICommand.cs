using System.Reflection;
using Discord;
using Discord.WebSocket;
using DiscordAssistant.Attributes;

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
    static object? CreateDefaultValue_<T>() { return default; }
    static object? CreateDefaultValue(Type type)=>typeof(CommandBase).GetMethod(nameof(CreateDefaultValue_), BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(type).Invoke(null,[]);
    public Task ExecuteAsync(SocketSlashCommand command)
    {
        var parameters = HandlerMethod.GetParameters();
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType == typeof(SocketSlashCommand))
            {
                args[i] = command;
            }
            else
            {
                var option = command.Data.Options.FirstOrDefault(o => o.Name.Equals(parameters[i].Name, StringComparison.CurrentCultureIgnoreCase));
                var attr = parameters[i].GetCustomAttribute<CommandParameterAttribute>();
                bool isRequired = attr?.IsRequire ?? true;
                if (option == null && isRequired)
                {
                    if (isRequired)
                        throw new ArgumentException($"Missing required option {parameters[i].Name} for command {Name}.");
                    args[i] = CreateDefaultValue(parameterType);
                    continue;
                }
                if (option?.Value == null)
                {
                    if (isRequired)
                        throw new ArgumentException($"Option {parameters[i].Name} for command {Name} cannot be null.");
                    args[i] = CreateDefaultValue(parameterType);
                    continue;
                }
                if (option.Value.GetType() != parameterType)
                {
                    args[i] = Convert.ChangeType(option.Value, parameterType);
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

        var descAttribe = method.GetCustomAttribute<CommandDescriptionAttribute>();
        if (descAttribe != null)
        {
            description = descAttribe.DescriptionText;
        }

        var parameters = method.GetParameters();
        if (parameters.Length < 1 || parameters[0].ParameterType != typeof(SocketSlashCommand))
        {
            throw new ArgumentException($"Method {method.Name} must have a SocketSlashCommand as the first parameter.");
        }
        var builder = new SlashCommandBuilder().WithName(commandName).WithDescription(description);
        for (int i = 1; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramAttribute = param.GetCustomAttribute<CommandParameterAttribute>();
            var optionBuilder = new SlashCommandOptionBuilder()
                .WithName(paramAttribute?.Name ?? param.Name!.ToLower())
                .WithDescription(paramAttribute?.Description ?? $"The {param.Name} parameter.")
                .WithRequired(paramAttribute?.IsRequire ?? Nullable.GetUnderlyingType(param.ParameterType) == null);

            if (param.ParameterType == typeof(int) || param.ParameterType == typeof(long))
            {
                optionBuilder.WithType(ApplicationCommandOptionType.Integer);
            }
            else if (param.ParameterType == typeof(string))
            {
                optionBuilder.WithType(ApplicationCommandOptionType.String);
            }
            else if (param.ParameterType == typeof(bool))
            {
                optionBuilder.WithType(ApplicationCommandOptionType.Boolean);
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
