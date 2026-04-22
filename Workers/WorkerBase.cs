using System.Data;
using Discord.WebSocket;

namespace DiscordAssistant.Workers;

public class WorkerBase
{
    protected CancellationTokenSource? _cancellationTokenSource { get; set; }
    protected DiscordSocketClient _client { get; set; }
    protected Func<IDbConnection> _dbFactory { get; set; }
    protected ulong _scheduleId { get; set; }

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

    protected static bool CronMatches(string cronExpression, DateTimeOffset timestamp)
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

    public WorkerBase(DiscordSocketClient client, Func<IDbConnection> factory, ulong schedule_id)
    {
        _client = client;
        _dbFactory = factory;
        _scheduleId = schedule_id;
    }
}