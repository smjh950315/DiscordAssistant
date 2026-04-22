using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAssistant.DBModels;

[Table("brileith_recruit")]
public class BriLeithRecruit
{
    [Key]
    public long id { get; set; }

    public long channel_id { get; set; }

    [MaxLength(512)]
    public string recruit_message { get; set; }

    [MaxLength(32)]
    public string recruit_time_regex { get; set; }
}