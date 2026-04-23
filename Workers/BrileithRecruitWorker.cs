using System.Data;
using System.Diagnostics.CodeAnalysis;
using Dapper;
using Discord.WebSocket;

namespace DiscordAssistant.Workers;

public class BrileithRecruitWorker : WorkerBase, IWorker
{
    public BrileithRecruitWorker(DiscordSocketClient client, ulong scheduleId) : base(client, scheduleId)
    {
        _client = client;
        _scheduleId = scheduleId;
    }

    class RecruitData
    {
        public long id { get; set; }
        public long channel_id { get; set; }
        public string recruit_message { get; set; }
        public string recruit_time_regex { get; set; }
    }
    class RecruitTargetData
    {
        public long target_id { get; set; }

        public string recruit_time_regex { get; set; }
    }

    RecruitData? _recruitData;

    [MemberNotNullWhen(true, nameof(_recruitData))]
    bool UpdateRecruitData()
    {
        using (var conn = Global.GetConnection())
        {
            _recruitData = conn.QueryFirstOrDefault<RecruitData>("select * from brileith_recruit where id = @_scheduleId", new { _scheduleId = (long)_scheduleId });
        }
        return _recruitData != null;
    }
    public override void Start()
    {
        CancellationTokenSource cts = new();
        _cancellationTokenSource = cts;
        var cancelationToken = cts.Token;
        var task = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            while (!cancelationToken.IsCancellationRequested)
            {
                if (!UpdateRecruitData())
                    break;

                var now = DateTimeOffset.Now;

                if (CronMatches(_recruitData.recruit_time_regex, now))
                {
                    IEnumerable<long> targetIds = [];
                    using (var conn = Global.GetConnection())
                    {
                        var notifyTargets = conn.Query<RecruitTargetData>("select target_id, recruit_time_regex from brileith_recruit_target where recruit_id = @_recruitId", new
                        {
                            _recruitId = _recruitData.id
                        });
                        notifyTargets = notifyTargets.Where(n => CronMatches(n.recruit_time_regex, now));
                        if (notifyTargets.Any())
                        {
                            targetIds = notifyTargets.Select(t => t.target_id);
                        }
                    }
                    await _client.SendToChannelAsync((ulong)_recruitData.channel_id, CreateMessageText(_recruitData.recruit_message, targetIds));
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        });
    }

    public override async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
        }
    }
}