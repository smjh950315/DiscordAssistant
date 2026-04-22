namespace DiscordAssistant.Workers;

public interface IWorker
{
    void Start();
    Task StopAsync();
}