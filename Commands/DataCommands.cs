using Dapper;
using Discord.WebSocket;
using DiscordAssistant.Attributes;
using DiscordAssistant.DBModels;

namespace DiscordAssistant.Commands;

public static class DataCommands
{
    [CommandDescription("設定查詢字典 (邏輯部分尚待完善)")]
    public static async Task get(SocketSlashCommand command, string name)
    {
        long? _guildId = (long?)command.GuildId;
        long? _channelId = (long?)command.ChannelId;
        long? _userId = (long?)command.User.Id;
        IEnumerable<DataStorage> storages;
        using (var conn = Utilities.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            storages = conn.Query<DataStorage>($@"
            select * from data_storage 
            where name = @_name and (guild_id = @_guildId or channel_id = @_channelId or user_id = @_userId or scope_expression is null)", new
            {
                _name = name,
                _channelId,
                _guildId,
                _userId
            });
        }
        var st = storages.FirstOrDefault(s => s.channel_id == _channelId && s.guild_id == _guildId && s.user_id == _userId && s.scope_expression == "cgu");
        if (st == null)
            st = storages.FirstOrDefault(s => s.guild_id == _guildId && s.user_id == _userId && s.scope_expression == "gu");
        if (st == null)
            st = storages.FirstOrDefault(s => s.user_id == _userId && s.scope_expression == "u");

        st ??= storages.FirstOrDefault();

        if (st == null)
        {
            await command.RespondAsync("No data");
            return;
        }

        await command.RespondAsync(st.data);
    }

    [CommandDescription("查詢字典 (邏輯部分尚待完善)")]
    public static async Task set(SocketSlashCommand command, string name, string data, int isPrivate, string scope)
    {
        var connection = Utilities.GetConnection();
        if (connection == null)
        {
            await command.ResponseDbConnectionFailureAsync();
            return;
        }

        using (var conn = Utilities.GetConnection())
        {
            if (conn == null)
            {
                await command.ResponseDbConnectionFailureAsync();
                return;
            }
            System.Dynamic.ExpandoObject param = new System.Dynamic.ExpandoObject();
            param.TryAdd("_name", name);
            param.TryAdd("_data", data);
            if (isPrivate == 0)
            {
                var id = conn.QueryFirstOrDefault<long>("select id from data_storage where name = @_name", param);
                if (id != 0)
                    conn.Execute("update data_storage set data = @_data where name = @_name", param);
                else
                    conn.Execute($@"
                    insert into data_storage(name, data, guild_id, channel_id, user_id, scope_expression)
                    values(@_name, @_data, null, null, null, null)", param);
            }
            else
            {
                param.TryAdd("_userId", (long)command.User.Id);
                string? _expr = string.Empty;
                if (!string.IsNullOrEmpty(scope))
                {
                    _expr = new string(scope.Where(c => "cgu".Contains(c)).Distinct().Order().ToArray());
                }
                string sqlSelect = "select id from data_storage where name = @_name";
                string sqlInsert = "insert into data_storage(name, data, guild_id, channel_id, user_id, scope_expression) values(@_name, @_data, @_guildId, @_channelId, @_userId, @_expr)";
                string sqlUpdate = "update data_storage set data = @_data, user_id = @_userId";

                long? _channelId, _guildId;
                if (_expr.Contains('c'))
                {
                    sqlSelect += " and channel_id = @_channelId";
                    _channelId = (long?)command.ChannelId;
                    sqlUpdate += ", channel_id = @_channelId";
                }
                else
                {
                    _channelId = null;
                }

                if (_expr.Contains('g'))
                {
                    sqlSelect += "and guild_id = @_guildId";
                    _guildId = (long?)command.GuildId;
                    sqlUpdate += "guild_id = @_guildId";
                }
                else
                {
                    _guildId = null;
                }

                if (_expr.Length == 0)
                {
                    _expr = null;
                }

                param.TryAdd("_channelId", _channelId);
                param.TryAdd("_guildId", _guildId);
                param.TryAdd("_expr", _expr);

                var id = conn.QueryFirstOrDefault<long>(sqlSelect, param);
                if (id == 0)
                {
                    conn.Execute(sqlInsert, param);
                }
                else
                {
                    conn.Execute(sqlUpdate, param);
                }
            }
            await command.RespondAsync($"成功設定 {name}={data}");
        }
    }
}