using System.Data;
using Discord;
using Discord.WebSocket;

namespace DiscordAssistant.Workers;

public class BrileithRecruitWorker : ScheduleWorker, IWorker
{
    int _minParticipant;
    public BrileithRecruitWorker(DiscordSocketClient client, long scheduleId, int minParticipant) : base(client, scheduleId)
    {
        _client = client;
        _minParticipant = minParticipant;
    }

    public override async Task MessageEventListener(IUserMessage message)
    {
        if (_cancellationTokenSource == null)
        {
            await base.MessageEventListener(message);
            return;
        }
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (await _client.FreshMessageAsync(message) is IUserMessage userMessage)
            {
                var channel = userMessage.Channel;
                if (channel == null)
                    break;
                var ru = await userMessage.GetReactionUsers();
                if (ru.Count() >= _minParticipant)
                {
                    await channel
                    .TagUsers(ru.Select(u => u.Id),
                        mentions => $"[通知] 當前報名人數為 {ru.Count()} 已達出團標準! {mentions} 請準備上車!");
                    break;
                }
            }
            else
            {
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
        await base.MessageEventListener(message);
    }

}