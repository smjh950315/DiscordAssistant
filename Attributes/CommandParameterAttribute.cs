namespace DiscordAssistant.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class CommandParameterAttribute : Attribute
{
    public bool IsRequire { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public CommandParameterAttribute(bool isRequire, string? description = null)
    {
        IsRequire = isRequire;
        Description = description;
    }
    public CommandParameterAttribute(string name, bool isRequire, string? description = null)
    {
        Name = name;
        IsRequire = isRequire;
        Description = description;
    }
    public CommandParameterAttribute(string name, string? description = null)
    {
        Name = name;
        IsRequire = false;
        Description = description;
    }
}