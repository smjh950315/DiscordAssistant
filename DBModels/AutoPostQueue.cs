using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAssistant.DBModels;

[Table("auto_post_queue")]
public class AutoPostQueue
{
    [Key]
    public long id { get; set; }

    public long? channel_id { get; set; }

    [MaxLength(1024)]
    public string? url { get; set; }
}
