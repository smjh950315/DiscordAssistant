using System.Data;
using System.Reflection;
using System.Text;
using Dapper;
using Discord.WebSocket;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Commands;

public static class BrileithCommands
{
    public static IEnumerable<ICommand> All
    {
        get
        {
            return typeof(BrileithCommands)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetParameters().Count() != 0 && m.GetParameters().First().ParameterType == typeof(SocketSlashCommand))
                .Select(CommandBase.BuildCommand);
        }
    }

    public static async Task brileith_edit(SocketSlashCommand command, string weekDay, string timeOfDay, string? recruitMessage)
    {
        var userId = command.User.Id;
        var channelId = command.ChannelId;
        var connection = Global.GetConnection();
        if (connection == null || channelId == null)
        {
            await command.ResponseDbConnectionFailureAsync();
            return;
        }
        var _userId = (long)userId;
        var _channelId = (long)channelId;
        var party = connection.QueryFirstOrDefault<BriLeithRecruit>($@"
        select * 
        from brileith_recruit br
        where br.channel_id = @_channelId", new
        {
            _channelId
        });

        bool useInsert = party == null;

        string timeRegex = Utilities.BuildRecruitTimeRegex(weekDay, timeOfDay);
        if (string.IsNullOrEmpty(timeRegex))
        {
            await command.RespondAsync("設定的時間無效");
            return;
        }

        if (party == null)
        {
            party = new BriLeithRecruit()
            {
                channel_id = (long)_channelId,
            };
        }
        if (recruitMessage != null)
        {
            party.recruit_message = recruitMessage;
        }

        if (string.IsNullOrEmpty(party.recruit_message))
        {
            party.recruit_message = string.Empty;
        }

        party.recruit_time_regex = timeRegex;

        if (useInsert)
        {
            connection.Execute($@"
            insert into brileith_recruit(channel_id, recruit_message, recruit_time_regex)
            values(@_cid, @_message, @_regex)", new
            {
                _cid = _channelId,
                _message = party.recruit_message,
                _regex = party.recruit_time_regex
            });
            await command.RespondAsync("新增出團設定");
        }
        else
        {
            connection.Execute($@"
            update brileith_recruit 
            set channel_id = @_cid, recruit_message = @_message, recruit_time_regex = @_regex", new
            {
                _cid = _channelId,
                _message = party.recruit_message,
                _regex = party.recruit_time_regex
            });
            await command.RespondAsync("更新出團設定");
        }
    }

    public static async Task brileith_join(SocketSlashCommand command)
    {
        var userId = command.User.Id;
        var channelId = command.ChannelId;
        var connection = Global.GetConnection();
        if (connection == null || !channelId.HasValue)
        {
            await command.ResponseDbConnectionFailureAsync();
            return;
        }
        var _userId = (long)userId;
        var _channelId = (long)channelId;
        var selfRecord = connection.QueryFirstOrDefault<long>($@"
        select brt.id 
        from brileith_recruit_target brt
        join brileith_recruit br on brt.recruit_id = br.id
        where brt.target_id = @_userId and br.channel_id = @_channelId", new
        {
            _userId,
            _channelId
        });
        if (selfRecord == 0)
        {
            var partyId = connection.QueryFirstOrDefault<long>($@"
            select br.id
            from brileith_recruit br
            where br.channel_id = @_channelId", new
            {
                _channelId
            });
            if (partyId == 0)
            {
                await command.RespondAsync("沒有出團設定");
                return;
            }
            var regex = connection.QueryFirstOrDefault<string>($@"
            select br.recruit_time_regex
            from brileith_recruit br
            where br.id = @_id", new
            {
                _id = partyId
            });
            if (string.IsNullOrEmpty(regex))
            {
                await command.RespondAsync("時間無效");
                return;
            }
            if (selfRecord == 0)
            {
                connection.Execute($@"
                insert into brileith_recruit_target(recruit_id, target_id, recruit_time_regex)
                values(@_rid, @_tid, @_regex)", new
                {
                    _rid = partyId,
                    _tid = _userId,
                    _regex = regex
                });
            }
            else
            {
                connection.Execute($@"
                update brileith_recruit_target 
                set recruit_id = @_rid, target_id = @_tid, recruit_time_regex = @_regex
                where id = @_id", new
                {
                    _rid = partyId,
                    _tid = _userId,
                    _regex = regex,
                    _id = selfRecord
                });
            }
            await command.ResponseSuccess();
            return;
        }
        await command.RespondAsync("已有對應設定，請使用/brileith_join_setweekday修改時間");
    }

    public static async Task brileith_join_setweekday(SocketSlashCommand command, string weekDays)
    {
        var userId = command.User.Id;
        var channelId = command.ChannelId;
        var connection = Global.GetConnection();
        if (connection == null || !channelId.HasValue)
        {
            await command.ResponseDbConnectionFailureAsync();
            return;
        }
        var _userId = (long)userId;
        var _channelId = (long)channelId;
        var partyId = connection.QueryFirstOrDefault<long>($@"
            select br.id
            from brileith_recruit br
            where br.channel_id = @_channelId", new
        {
            _channelId
        });
        if (partyId == 0)
        {
            await command.RespondAsync("沒有出團設定");
            return;
        }
        var regex = connection.QueryFirstOrDefault<string>($@"
            select br.recruit_time_regex
            from brileith_recruit br
            where br.id = @_id", new
        {
            _id = partyId
        });
        if (string.IsNullOrEmpty(regex))
        {
            await command.RespondAsync("出團時間無效");
            return;
        }

        regex = Utilities.OverrideCronWeekDays("* * * * *", weekDays);
        if (string.IsNullOrEmpty(regex))
        {
            await command.RespondAsync("輸入時間無效");
            return;
        }

        var selfRecord = connection.QueryFirstOrDefault<long>($@"
        select brt.id 
        from brileith_recruit_target brt
        join brileith_recruit br on brt.recruit_id = br.id
        where brt.target_id = @_userId and br.channel_id = @_channelId", new
        {
            _userId,
            _channelId
        });
        if (selfRecord == 0)
        {
            connection.Execute($@"
                insert into brileith_recruit_target(recruit_id, target_id, recruit_time_regex)
                values(@_rid, @_tid, @_regex)", new
            {
                _rid = partyId,
                _tid = _userId,
                _regex = regex
            });
        }
        else
        {
            connection.Execute($@"
                update brileith_recruit_target 
                set recruit_id = @_rid, target_id = @_tid, recruit_time_regex = @_regex
                where id = @_id", new
            {
                _rid = partyId,
                _tid = _userId,
                _regex = regex,
                _id = selfRecord
            });
        }
        await command.RespondAsync("設定成功");
    }

    public static async Task brileith_left(SocketSlashCommand command)
    {
        var userId = command.User.Id;
        var channelId = command.ChannelId;
        var connection = Global.GetConnection();
        if (connection == null || !channelId.HasValue)
        {
            await command.ResponseDbConnectionFailureAsync();
            return;
        }
        var _userId = (long)userId;
        var _channelId = (long)channelId;
        var partyId = connection.QueryFirstOrDefault<long>($@"
        select id 
        from brileith_recruit br
        where br.channel_id = @_channelId", new
        {
            _channelId
        });
        if (partyId == 0)
        {
            await command.ResponseFailure("沒有出團設定");
            return;
        }
        connection.Execute($@"
        delete from brileith_recruit_target where target_id = @_tid and recruit_id = @_rid", new
        {
            _tid = _userId,
            _rid = partyId
        });
        await command.ResponseSuccess();
    }

    public static async Task brileith_help(SocketSlashCommand command)
    {
        StringBuilder msg = new();
        msg.AppendLine("/brileith_edit: 設定出團時間");
        msg.AppendLine("/brileith_join: 出團時Tag我");
        msg.AppendLine("/brileith_join_setweekday: 出團時在指定星期Tag我, 輸入0~6 分別代表周日~周六, 用逗號分隔");
        msg.AppendLine("/brileith_left: 出團時都不要Tag我");
        await command.RespondAsync(msg.ToString());
    }

}