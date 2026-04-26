using Dapper;
using Discord.WebSocket;
using DiscordAssistant.Attributes;

namespace DiscordAssistant.Commands;

public static class NotifyCommands
{
    public static async Task set_notifier(SocketSlashCommand command, 
        string name, int hour, int minute, string message,
        [OptionalParameter] string weekDays,
        [OptionalParameter] string? workType)
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
        using (var conn = Global.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            try
            {
                var scheduleId = conn.QueryFirstOrDefault<long>("select id from schedule where name = @_name and channel_id = @_channelId", new
                {
                    _name = name,
                    _channelId
                });
                var _cronExpr = Utilities.GenerateCRONFormat(false, true, false, weekDays, hour, minute);
                if (scheduleId == 0)
                {
                    conn.Execute("""insert into schedule(name, channel_id, cron_expression, message_template, worker_type) values(@_name, @_channelId, @_cronExpr, @_msgExpr, @_workType)""", new
                    {
                        _name = name,
                        _channelId,
                        _cronExpr,
                        _msgExpr = message,
                        _workType = workType
                    });
                }
                else
                {
                    conn.Execute("""update schedule set cron_expression = @_cronExpr, message_template = @_msgExpr, worker_type = @_workType where name = @_name and channel_id = @_channelId;""", new
                    {
                        _name = name,
                        _channelId,
                        _cronExpr,
                        _msgExpr = message,
                        _workType = workType
                    });
                }
                await command.RespondAsync("設定成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                await command.RespondAsync("設定失敗");
            }
        }
    }

    public static async Task del_notifier(SocketSlashCommand command, string name)
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
        using (var conn = Global.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            var scheduleId = conn.QueryFirstOrDefault<long>("select id from schedule where name = @_name and channel_id = @_channelId", new
            {
                _name = name,
                _channelId
            });
            if (scheduleId == 0)
            {
                await command.ResponseFailure($"找不到名為 {name} 的通知設定");
            }
            else
            {
                await command.RespondAsync($"通知設定 {name} 已刪除");
            }
        }
    }
}