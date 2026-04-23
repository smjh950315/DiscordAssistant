using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAssistant.DBModels;

[Table("schedule")]
public class Schedule
{
    [Key]
    public long id { get; set; }

    [MaxLength(32)]
    public string cron_expression { get; set; }

    [MaxLength(512)]
    public string message_template { get; set; }
}
