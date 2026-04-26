namespace DiscordAssistant.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class CommandDescriptionAttribute : Attribute
{
    public string DescriptionText {get;set;}
    public CommandDescriptionAttribute(string desc)
    {
        DescriptionText = desc;
    }
}