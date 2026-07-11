using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteriorDesignWeb.Models.Entities
{
    public enum UserRole
    {
        FreeUser,
        Member,
        PremiumMember,
        Administrator 
    }

    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("UserID")]
        public int UserID { get; set; }

        [Required]
        [Column("Username")]
        [StringLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Column("PasswordHash")]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("PhoneNumber")]
        [StringLength(15)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("Role")]
        public UserRole Role { get; set; } = UserRole.FreeUser;

        [Column("RegisterTime")]
        public DateTime RegisterTime { get; set; }

        public List<Project> Projects { get; set; } = new();
        public List<UserSession> Sessions { get; set; } = new();
    }
}
