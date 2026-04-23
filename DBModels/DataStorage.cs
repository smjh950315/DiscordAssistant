
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAssistant.DBModels;

[Table("data_storage")]
public class DataStorage
{
    [Key]
    public long id { get; set; }

    [MaxLength(128)]
    public string name { get; set; }

    [MaxLength(1024)]
    public string data { get; set; }

    public long? guild_id { get; set; }

    public long? channel_id { get; set; }

    public long? user_id { get; set; }

    [MaxLength(64)]
    public string? scope_expression { get; set; }
}