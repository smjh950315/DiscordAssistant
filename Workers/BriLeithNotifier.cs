using Discord;

namespace DiscordAssistant.Workers;

class BriLeithNotifier
{
    CancellationToken _cancellationToken;
    Task? _task;
    IMessageChannel? _channel;
    IUserMessage? _message;
    ulong _channelId;
    int _hour;
    int _minute;
    int _minParticipants;

    ulong _specificMessageId;

    Discord.WebSocket.DiscordSocketClient _client;

    public BriLeithNotifier(Discord.WebSocket.DiscordSocketClient client,
        ulong channelId,
        int hour, int minute, int minParticipants)
    {
        this._client = client;
        this._channelId = channelId;
        this._hour = hour;
        this._minute = minute;
        this._minParticipants = minParticipants;
    }

    public async Task SetSpecificMessage(ulong messageId)
    {
        _specificMessageId = messageId;
    }

    public void Start()
    {
        CancellationTokenSource cts = new();
        _cancellationToken = cts.Token;
        Console.WriteLine("BriLeithNotifier is starting...");
        _task = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            this._channel = _client.GetChannel(_channelId) as IMessageChannel ?? throw new ArgumentException($"Channel with ID {_channelId} not found.");
            if (_specificMessageId != 0)
            {
                _message = await _channel.GetMessageAsync(_specificMessageId) as IUserMessage;
            }
            while (!_cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var targetTime = new DateTime(new DateOnly(now.Year, now.Month, now.Day), new TimeOnly(_hour, _minute));
                if (now.Hour == targetTime.Hour && now.Minute == targetTime.Minute) // Check if it's the specified time
                {
                    _message = await _channel.SendMessageAsync("[測試] <@&1427248003806396467> G27 報名，請在此訊息下方點選表情符號以示參加！");
                }

                if (_message != null)
                {
                    Console.WriteLine("[" + now.ToString() + "] _message is not null");
                    if (now.Hour == 23 && now.Minute == 59)
                    {
                        await _message.DeleteAsync();
                        _message = null;
                    }
                    else if (now > targetTime 
                    //&& now.Hour == 21 && now.Minute == 30
                    )
                    {
                        _message = await _client.FreshMessageAsync(_message) as IUserMessage;
                        if (_message != null)
                        {
                            var users = await _message.GetReactionUsers();
                            Console.WriteLine("[" + now.ToString() + "] user count is " + users.Count());
                            if (users.Count() >= _minParticipants)
                            {
                                await _channel.TagUsers(users.Select(u => u.Id), mentions => $"[測試] 當前報名人數為 {users.Count()} 已達出團標準! {mentions} 請準備上車!");
                                //await _message.DeleteAsync();
                                _message = null;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[" + now.ToString() + "] _message is null");
                }
                await Task.Delay(TimeSpan.FromSeconds(60));
            }
        }, _cancellationToken);
    }

    public async Task StopAsync()
    {
        if (_task != null)
        {
            Console.WriteLine("BriLeithNotifier is stopping...");
            _cancellationToken.ThrowIfCancellationRequested();
            await _task;
        }
    }
}