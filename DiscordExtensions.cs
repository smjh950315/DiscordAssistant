using Discord;
using Discord.WebSocket;

namespace DiscordAssistant;

public static class DiscordExtensions
{
    public static async Task<IMessage?> SendToChannelAsync(this DiscordSocketClient client, ulong channelId, string? message)
    {
        if (string.IsNullOrEmpty(message))
            return null;
        var channel = await client.GetChannelAsync(channelId);
        if (channel is IMessageChannel messageChannel)
        {
            return await messageChannel.SendMessageAsync(message);
        }
        return null;
    }
    
    public static async Task<IMessage?> FreshMessageAsync(this DiscordSocketClient client, IMessage message)
    {
        var channel = await client.GetChannelAsync(message.Channel.Id) as IMessageChannel;
        if (channel != null)
        {
            return await channel.GetMessageAsync(message.Id);
        }
        return null;
    }

    public static async Task<IEnumerable<IUser>> GetReactionUsers(this IMessage message)
    {
        var users = new List<IUser>();
        foreach (var reaction in message.Reactions)
        {
            var reactionUsers = await message.GetReactionUsersAsync(reaction.Key, int.MaxValue).FlattenAsync();
            users.AddRange(reactionUsers);
        }
        users = users.Where(u => u != null).DistinctBy(u => u.Id).ToList();
        return users;
    }

    public static async Task TagUsers(this IMessageChannel channel, IEnumerable<ulong> userIds, Func<string, string> messageFormatter)
    {
        if (channel != null)
        {
            var mentions = string.Join(" ", userIds.Select(id => $"<@{id}>"));
            await channel.SendMessageAsync(messageFormatter(mentions));
        }
    }

    public static async Task ResponseDbConnectionFailureAsync(this SocketSlashCommand command)
    {
        await command.ResponseFailure("Cannot connect to database");
    }

    public static async Task ResponseFailure(this SocketSlashCommand command, string reason)
    {
        await command.RespondAsync($"Failure: {reason}");
    }

    public static async Task ResponseSuccess(this SocketSlashCommand command)
    {
        await command.RespondAsync("Success!");
    }
}

