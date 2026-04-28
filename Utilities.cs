using System.Data;
using System.Reflection;
using Dapper;
using Discord;
using Discord.WebSocket;

namespace DiscordAssistant;

public class Utilities
{
    public static string GetDatabaseInitializeSql()
    {
        return """
            CREATE TABLE IF NOT EXISTS schedule (
                id BIGSERIAL PRIMARY KEY,
                name VARCHAR(32) NOT NULL,
                channel_id BIGINT NOT NULL,
                cron_expression VARCHAR(32) NOT NULL,
                message_template VARCHAR(512) NOT NULL,
                worker_type VARCHAR(32) NULL
            );

            CREATE TABLE IF NOT EXISTS schedule_subscriber (
                id BIGSERIAL PRIMARY KEY,
                schedule_id BIGINT NOT NULL REFERENCES schedule(id) ON DELETE CASCADE,
                subscriber_id BIGINT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS data_storage (
                id BIGSERIAL PRIMARY KEY,
                name VARCHAR(128) NOT NULL,
                data VARCHAR(1024) NOT NULL,
                guild_id BIGINT NULL,
                channel_id BIGINT NULL,
                user_id BIGINT NULL,
                scope_expression VARCHAR(64) NULL
            );

            CREATE TABLE IF NOT EXISTS brileith_recruit (
                id BIGSERIAL PRIMARY KEY,
                channel_id BIGINT NOT NULL,
                recruit_message VARCHAR(512) NOT NULL,
                recruit_time_regex VARCHAR(32) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS brileith_recruit_target (
                id BIGSERIAL PRIMARY KEY,
                recruit_id BIGINT NOT NULL REFERENCES brileith_recruit(id) ON DELETE CASCADE,
                target_id BIGINT NOT NULL,
                recruit_time_regex VARCHAR(32) NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_schedule_subscriber_schedule_id
                ON schedule_subscriber(schedule_id);

            CREATE INDEX IF NOT EXISTS idx_data_storage_name_scope
                ON data_storage(name, guild_id, channel_id, user_id, scope_expression);

            CREATE INDEX IF NOT EXISTS idx_brileith_recruit_target_recruit_id
                ON brileith_recruit_target(recruit_id);
            """;
    }

    public static string BuildRecruitTimeRegex(string weekDay, string timeOfDay)
    {
        if (string.IsNullOrWhiteSpace(weekDay) || string.IsNullOrWhiteSpace(timeOfDay))
        {
            return string.Empty;
        }

        if (!TryParseTimeOfDay(timeOfDay, out var hour, out var minute))
        {
            return string.Empty;
        }

        var cronWeekDay = NormalizeCronWeekDayField(weekDay);
        if (string.IsNullOrEmpty(cronWeekDay))
        {
            return string.Empty;
        }

        return $"{minute} {hour} * * {cronWeekDay}";
    }

    private static readonly IReadOnlyDictionary<string, int> WeekDayLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["sun"] = 0,
        ["sunday"] = 0,
        ["0"] = 0,
        ["7"] = 0,
        ["mon"] = 1,
        ["monday"] = 1,
        ["1"] = 1,
        ["tue"] = 2,
        ["tues"] = 2,
        ["tuesday"] = 2,
        ["2"] = 2,
        ["wed"] = 3,
        ["wednesday"] = 3,
        ["3"] = 3,
        ["thu"] = 4,
        ["thur"] = 4,
        ["thurs"] = 4,
        ["thursday"] = 4,
        ["4"] = 4,
        ["fri"] = 5,
        ["friday"] = 5,
        ["5"] = 5,
        ["sat"] = 6,
        ["saturday"] = 6,
        ["6"] = 6,
    };


    public static bool CronMatches(string cronExpression, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            return false;
        }

        return MatchesCronField(parts[0], timestamp.Minute, 0, 59, null)
            && MatchesCronField(parts[1], timestamp.Hour, 0, 23, null)
            && MatchesCronField(parts[2], timestamp.Day, 1, 31, null)
            && MatchesCronField(parts[3], timestamp.Month, 1, 12, null)
            && MatchesCronField(
                parts[4],
                (int)timestamp.DayOfWeek,
                0,
                7,
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SUN"] = 0,
                    ["MON"] = 1,
                    ["TUE"] = 2,
                    ["WED"] = 3,
                    ["THU"] = 4,
                    ["FRI"] = 5,
                    ["SAT"] = 6,
                });
    }

    private static bool MatchesCronField(
        string expression,
        int value,
        int minValue,
        int maxValue,
        IReadOnlyDictionary<string, int>? aliases)
    {
        foreach (var token in expression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (MatchesCronToken(token, value, minValue, maxValue, aliases))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesCronToken(
        string token,
        int value,
        int minValue,
        int maxValue,
        IReadOnlyDictionary<string, int>? aliases)
    {
        if (token == "*")
        {
            return true;
        }

        var stepParts = token.Split('/', 2, StringSplitOptions.TrimEntries);
        var rangePart = stepParts[0];
        var step = stepParts.Length == 2 ? ParseCronValue(stepParts[1], aliases) : 1;
        if (step <= 0)
        {
            return false;
        }

        var (rangeStart, rangeEnd) = rangePart switch
        {
            "*" or "" => (minValue, maxValue),
            _ when rangePart.Contains('-', StringComparison.Ordinal) => ParseCronRange(rangePart, aliases),
            _ => (ParseCronValue(rangePart, aliases), ParseCronValue(rangePart, aliases)),
        };

        var candidateValues = aliases != null && maxValue == 7 && value == 0
            ? new[] { 0, 7 }
            : new[] { value };

        return candidateValues.Any(candidateValue =>
            candidateValue >= rangeStart
            && candidateValue <= rangeEnd
            && (candidateValue - rangeStart) % step == 0);
    }

    private static (int Start, int End) ParseCronRange(string rangePart, IReadOnlyDictionary<string, int>? aliases)
    {
        var bounds = rangePart.Split('-', 2, StringSplitOptions.TrimEntries);
        if (bounds.Length != 2)
        {
            throw new FormatException($"Invalid cron range '{rangePart}'.");
        }

        return (ParseCronValue(bounds[0], aliases), ParseCronValue(bounds[1], aliases));
    }

    private static int ParseCronValue(string value, IReadOnlyDictionary<string, int>? aliases)
    {
        if (aliases != null && aliases.TryGetValue(value, out var aliasValue))
        {
            return aliasValue;
        }

        return int.Parse(value);
    }

    private static DayOfWeek ParseDayOfWeek(string dayOfWeek)
    {
        if (Enum.TryParse<DayOfWeek>(dayOfWeek, true, out var parsed))
        {
            return parsed;
        }

        return dayOfWeek.Trim() switch
        {
            "0" or "7" => DayOfWeek.Sunday,
            "1" => DayOfWeek.Monday,
            "2" => DayOfWeek.Tuesday,
            "3" => DayOfWeek.Wednesday,
            "4" => DayOfWeek.Thursday,
            "5" => DayOfWeek.Friday,
            "6" => DayOfWeek.Saturday,
            _ => throw new ArgumentException($"Unsupported day of week value '{dayOfWeek}'.", nameof(dayOfWeek)),
        };
    }

    /// <summary>
    /// get string from <paramref name="messageFormater"/> which replace all {TARGETS} to <paramref name="notifyingId"/>
    /// </summary>
    /// <param name="messageFormater"></param>
    /// <param name="notifyingId"></param>
    /// <returns></returns>
    public static string CreateMessageText(string messageFormater, IEnumerable<long> notifyingId)
    {
        var notifyText = notifyingId.Distinct().Select(i => $"<@{i}>");
        return messageFormater.Replace("{TARGETS}", string.Join(',', notifyText), StringComparison.Ordinal);
    }

    public static string GenerateCRONFormat(bool isBefore, bool isEqual, bool isAfter, string dayOfWeek, int hourOfDay, int minuteOfHour)
    {
        if (!isBefore && !isEqual && !isAfter)
        {
            throw new ArgumentException("At least one time relation must be selected.", nameof(isEqual));
        }

        var parsedDayOfWeek = ParseDayOfWeek(dayOfWeek);
        var baseDate = new DateTime(2024, 1, 7, hourOfDay, minuteOfHour, 0, DateTimeKind.Unspecified)
            .AddDays((int)parsedDayOfWeek);

        var offsets = new List<int>();
        if (isBefore)
        {
            offsets.Add(-1);
        }
        if (isEqual)
        {
            offsets.Add(0);
        }
        if (isAfter)
        {
            offsets.Add(1);
        }

        return string.Join(
            "|",
            offsets
                .Select(offset => baseDate.AddMinutes(offset))
                .Select(value => $"{value.Minute} {value.Hour} * * {(int)value.DayOfWeek}")
                .Distinct(StringComparer.Ordinal));
    }


    static bool TryParseTimeOfDay(string timeOfDay, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        var normalized = timeOfDay.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        normalized = normalized.Replace('.', ':');
        var timeParts = normalized.Split(':', StringSplitOptions.TrimEntries);
        if (timeParts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(timeParts[0], out hour) || !int.TryParse(timeParts[1], out minute))
        {
            return false;
        }

        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    static string NormalizeCronWeekDayField(string weekDay)
    {
        var normalized = weekDay.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        if (normalized is "*" or "?")
        {
            return "*";
        }

        if (normalized.Equals("all", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("daily", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("everyday", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("every day", StringComparison.OrdinalIgnoreCase))
        {
            return "*";
        }

        var segments = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var normalizedSegments = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            var normalizedSegment = NormalizeCronWeekDaySegment(segment);
            if (string.IsNullOrEmpty(normalizedSegment))
            {
                return string.Empty;
            }

            normalizedSegments.Add(normalizedSegment);
        }

        return string.Join(',', normalizedSegments);
    }

    static string NormalizeCronWeekDaySegment(string segment)
    {
        var normalized = segment.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        var stepParts = normalized.Split('/', 2, StringSplitOptions.TrimEntries);
        var basePart = stepParts[0];
        var stepPart = stepParts.Length == 2 ? stepParts[1] : null;

        string normalizedBasePart;
        if (basePart == "*")
        {
            normalizedBasePart = "*";
        }
        else if (basePart.Contains('-', StringComparison.Ordinal))
        {
            var rangeParts = basePart.Split('-', 2, StringSplitOptions.TrimEntries);
            if (rangeParts.Length != 2)
            {
                return string.Empty;
            }

            if (!TryNormalizeWeekDayToken(rangeParts[0], out var rangeStart)
                || !TryNormalizeWeekDayToken(rangeParts[1], out var rangeEnd))
            {
                return string.Empty;
            }

            normalizedBasePart = $"{rangeStart}-{rangeEnd}";
        }
        else
        {
            if (!TryNormalizeWeekDayToken(basePart, out normalizedBasePart))
            {
                return string.Empty;
            }
        }

        if (stepPart == null)
        {
            return normalizedBasePart;
        }

        if (!int.TryParse(stepPart, out var step) || step <= 0)
        {
            return string.Empty;
        }

        return $"{normalizedBasePart}/{step}";
    }

    static bool TryNormalizeWeekDayToken(string token, out string normalized)
    {
        normalized = string.Empty;
        var trimmed = token.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (WeekDayLookup.TryGetValue(trimmed, out var numericDay))
        {
            normalized = numericDay.ToString();
            return true;
        }

        return false;
    }

    public static string OverrideCronWeekDays(string existingRegex, string weekDays)
    {
        if (string.IsNullOrWhiteSpace(existingRegex))
        {
            return string.Empty;
        }

        var normalizedWeekDays = NormalizeCronWeekDayField(weekDays);
        if (string.IsNullOrEmpty(normalizedWeekDays))
        {
            return string.Empty;
        }

        var cronParts = existingRegex.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (cronParts.Length != 5)
        {
            return string.Empty;
        }

        cronParts[4] = normalizedWeekDays;
        return string.Join(' ', cronParts);
    }

    public static IEnumerable<ICommand> GetCommands(Type type)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters().Count() != 0 && m.GetParameters().First().ParameterType == typeof(SocketSlashCommand))
            .Select(CommandBase.BuildCommand);
    }

    public static IEnumerable<ICommand> GetCommands<T>()
    {
        return GetCommands(typeof(T));
    }

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
        try
        {
            return new Npgsql.NpgsqlConnection(_connectionString);
        }
        catch
        {
            return null;
        }
    }

    public static string CronFormatToReadable(string cronFormat)
    {
        // [impl] convert cronformat string to format like: 
        // ~HH:mm (Sun, Mon, Fri)
        // HH:mm (Sun, Mon, Fri)
        // HH:mm (Sun, Mon, Fri) ~
    }
}
