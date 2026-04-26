namespace DiscordAssistant.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class OptionalParameterAttribute : Attribute
{
    public OptionalParameterAttribute()
    {
    }
}