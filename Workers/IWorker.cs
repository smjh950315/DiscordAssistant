using Discord;

namespace DiscordAssistant.Workers;

public interface IWorker
{
    Task MessageEventListener(IUserMessage message, int? timeout = null);
    void Start();
    Task StopAsync();
    bool IsFinished();
    bool IsTerminated();
}