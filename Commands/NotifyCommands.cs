using System.Text;
using Dapper;
using Discord.WebSocket;
using DiscordAssistant.Attributes;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Commands;

public static class NotifyCommands
{
    public static async Task notifier_set(SocketSlashCommand command,
        string name, int hour, int minute, string message,
        [CommandParameter(false, "0到6 分別代表週日到周六，逗號(',')分隔或是留空表示全部")] string? weekDays,
        [CommandParameter(false)] string? workType)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
        {
            await command.ResponseFailure("name 不可為空");
            return;
        }
        var channelId = command.ChannelId;
        if (channelId == null)
        {
            await command.ResponseFailure("無法取的頻道訊息");
            return;
        }
        var _channelId = (long)channelId;
        using (var conn = Utilities.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            try
            {
                string? _cronExpr;
                try { _cronExpr = Utilities.GenerateCRONFormat(false, true, false, weekDays ?? "0123456", hour, minute); }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    await command.ResponseFailure("時間輸入格式錯誤");
                    return;
                }
                conn.Execute("""insert into schedule(name, channel_id, cron_expression, message_template, worker_type) values(@_name, @_channelId, @_cronExpr, @_msgExpr, @_workType)""", new
                {
                    _name = name,
                    _channelId,
                    _cronExpr,
                    _msgExpr = message,
                    _workType = workType
                });
                await command.RespondAsync("設定成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                await command.RespondAsync("設定失敗");
            }
        }
    }

    public static async Task notifier_del(SocketSlashCommand command, long id)
    {
        if (id == 0)
        {
            await command.ResponseFailure("id 無效");
            return;
        }
        var channelId = command.ChannelId;
        if (channelId == null)
        {
            await command.ResponseFailure("無法取的頻道訊息");
            return;
        }
        var _channelId = (long)channelId;
        using (var conn = Utilities.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            var scheduleId = conn.QueryFirstOrDefault<long>("select id from schedule where id = @_id and channel_id = @_channelId", new
            {
                _id = id,
                _channelId
            });
            if (scheduleId == 0)
            {
                await command.ResponseFailure($"找不到id為 {id} 的通知設定");
            }
            else
            {
                conn.Execute("delete from schedule where id = @_id", new
                {
                    _id = id
                });
                await command.RespondAsync($"id為 {id} 的通知設定已刪除");
            }
        }
    }

    public static async Task notifier_get_list(SocketSlashCommand command)
    {
        var channelId = command.ChannelId;
        if (channelId == null)
        {
            await command.ResponseFailure("無法取的頻道訊息");
            return;
        }
        var _channelId = (long)channelId;
        using (var conn = Utilities.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            var allNotifier = conn.Query<Schedule>("select * from schedule where channel_id = @_channelId", new
            {
                _channelId
            });
            StringBuilder stringBuilder = new ();
            stringBuilder.AppendLine("id, name, cron_expression, message_template, worker_type");
            foreach (var notifier in allNotifier)
            {
                stringBuilder.AppendLine($"{notifier.id}, {notifier.name}, {notifier.cron_expression}, {notifier.message_template}, {notifier.worker_type}");
            }
            await command.RespondAsync(stringBuilder.ToString());
        }
    }
}