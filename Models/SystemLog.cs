using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BUS_Agency_backstage.Models
{
    [Table("System_Logs")]
    public class SystemLog
    {
        [Key]
        public int LogID { get; set; }
        
        [Required]
        public Guid AdminID { get; set; }
        
        [Required, StringLength(50)]
        public string AdminName { get; set; }
        
        [Required, StringLength(50)]
        public string ActionType { get; set; }
        
        [Required, StringLength(100)]
        public string TargetObject { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        [StringLength(50)]
        public string IPAddress { get; set; }
        
        public DateTime LogDate { get; set; } = DateTime.Now;

        // 關聯導航屬性 (設定關聯到 Account)
        [ForeignKey("AdminID")]
        public virtual Account Admin { get; set; }
    }
}