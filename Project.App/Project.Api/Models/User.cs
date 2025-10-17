using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Api.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(256)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(256)]
        public string Email { get; set; } = null!;

        [Required, MaxLength(256)]
        public string PasswordHash { get; set; } = null!;
        public double Balance { get; set; } = 1000;

        public ICollection<RoomPlayer> RoomPlayers { get; set; } = new List<RoomPlayer>();
    }
}
