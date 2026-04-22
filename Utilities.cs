namespace DiscordAssistant;

public class Utilities
{
    public static string GetDatabaseInitializeSql()
    {
        return """
            CREATE TABLE IF NOT EXISTS brileith_recruit (
                id BIGSERIAL PRIMARY KEY,
                channel_id BIGINT NOT NULL,
                recruit_message VARCHAR(512) NOT NULL,
                recruit_time_regex VARCHAR(32) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS brileith_recruit_target (
                id BIGSERIAL PRIMARY KEY,
                recruit_id BIGINT NOT NULL REFERENCES brileith_recruit(id) ON DELETE CASCADE,
                target_id TEXT NOT NULL,
                recruit_time_regex VARCHAR(32) NOT NULL
            );

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

}
