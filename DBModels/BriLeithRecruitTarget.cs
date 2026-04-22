using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAssistant.DBModels;

[Table("brileith_recruit_target")]
public class BriLeithRecruitTarget
{
    [Key]
    public long id { get; set; }
    
    public long recruit_id { get; set; }

    public long target_id { get; set; }

    [MaxLength(32)]
    public string recruit_time_regex { get; set; }
}