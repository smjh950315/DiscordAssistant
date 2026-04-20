using System.Reflection;
using Discord;
using Discord.WebSocket;

namespace DiscordAssistant;

public static class CommandHelper
{
    public static SlashCommandProperties BuildSlashCommandFromFunction(MethodInfo method)
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