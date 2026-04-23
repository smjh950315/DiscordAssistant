namespace DiscordAssistant;

public static class Extensions
{
    public static bool HasEnum(this ValueType @enum, ValueType target)
    {
        ulong src = (ulong)@enum;
        ulong dst = (ulong)target;
        return (@src & (~dst)) != 0;
    }
}