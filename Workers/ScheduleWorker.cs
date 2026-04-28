using Dapper;
using Discord;
using Discord.WebSocket;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Workers;

public class ScheduleWorker : IWorker
{
    protected CancellationTokenSource? _cancellationTokenSource { get; set; }
    protected DiscordSocketClient _client { get; set; }
    protected long _scheduleId { get; set; }
    Task? _loop { get; set; }
    public ScheduleWorker(DiscordSocketClient client, long schedule_id)
    {
        _client = client;
        _scheduleId = schedule_id;
    }

    public virtual async Task TaskLoop(CancellationToken cancelationToken)
    {
        while (!cancelationToken.IsCancellationRequested)
        {
            string? name;
            string? cronRegex;
            string? messageTemplate;
            long? channelId;
            using (var conn = Utilities.GetConnection())
            {
                if (conn == null)
                    break;
                Schedule? sc = conn?.QueryFirstOrDefault<Schedule>("select * from schedule where id = @_scheduleId", new
                {
                    _scheduleId
                });
                if (sc == null)
                    break;
                name = sc.name;
                channelId = sc.channel_id;
                cronRegex = sc.cron_expression;
                messageTemplate = sc.message_template;
            }

            if (channelId == null)
                break;
            if (string.IsNullOrEmpty(cronRegex) || string.IsNullOrEmpty(messageTemplate))
                break;

            var now = DateTimeOffset.Now;
            if (Utilities.CronMatches(cronRegex, now))
            {
                var channel = await _client.GetChannelAsync((ulong)channelId);
                if (channel is IMessageChannel messageChannel)
                {
                    var sentMessage = await messageChannel.SendMessageAsync(Utilities.CreateMessageText($"[{name}] {messageTemplate}", []));
                    await MessageEventListener(sentMessage);
                }
                else
                {
                    break;
                }
                if (now.Second <= 30)
                {
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                continue;
            }
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    public virtual async Task MessageEventListener(IUserMessage message)
    {
    }

    public void Start()
    {
        CancellationTokenSource cts = new();
        _cancellationTokenSource = cts;
        var cancelationToken = cts.Token;
        var task = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            try
            {
                await this.TaskLoop(cancelationToken);
            }
            catch
            {
            }
        });
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
        }
    }

    public bool IsFinished()
    {
        if (_cancellationTokenSource == null || _loop == null)
            return true;
        return _loop.Status != TaskStatus.Running;
    }
}
