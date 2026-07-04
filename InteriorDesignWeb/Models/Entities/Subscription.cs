using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace InteriorDesignWeb.Models.Entities
{
    public class Subscription
    {
        [Key]
        public int SubscriptionID { get; set; }

        [ForeignKey("User")]
        public int UserID { get; set; }

        [Required]
        public string Plan { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }
    }
}
