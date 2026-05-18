using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BUS_Agency_backstage.Models
{
    [Table("Faqs")]
    public partial class Faq
    {
        [Key]
        public int FaqId { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string Question { get; set; } = null!;

        [Required]
        public string Answer { get; set; } = null!;

        public DateTime? CreatedDate { get; set; }
    }
}