using System.Data;
using Dapper;

namespace DiscordAssistant;

public static class Global
{
    static string? _connectionString;
    public static void SetConnectionString(string? connstr)
    {
        _connectionString = connstr;
        if (!string.IsNullOrEmpty(_connectionString))
        {
            using (var conn = GetConnection())
            {
                conn?.Execute(Utilities.GetDatabaseInitializeSql());
            }
        }
    }
    public static IDbConnection? GetConnection()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return null;
        return new Npgsql.NpgsqlConnection(_connectionString);
    }
}
