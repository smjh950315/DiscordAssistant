using Discord;

namespace DiscordAssistant.Workers;

public interface IWorker
{
    Task MessageEventListener(IUserMessage message);
    void Start();
    Task StopAsync();
}