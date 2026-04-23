using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAssistant.DBModels;

[Table("schedule_subscriber")]
public class ScheduleSubscriber
{
    [Key]
    public long id { get; set; }

    public long schedule_id { get; set; }

    public long subscriber_id { get; set; }

    public long subscriber_channel_id { get; set; }
}