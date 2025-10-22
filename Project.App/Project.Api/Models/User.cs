using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Api.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required, MaxLength(256)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(256)]
        public string Email { get; set; } = null!;

        public double Balance { get; set; } = 1000;

        [MaxLength(512)]
        public string? AvatarUrl { get; set; } //we will send this to the front for our pfp

        public ICollection<RoomPlayer> RoomPlayers { get; set; } = [];
    }
}
