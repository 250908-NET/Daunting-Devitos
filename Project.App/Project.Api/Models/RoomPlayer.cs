using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project.Api.Utilities.Enums;

namespace Project.Api.Models;

public class RoomPlayer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public Guid RoomId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey("RoomId")]
    public virtual Room? Room { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public virtual ICollection<Hand> Hands { get; set; } = [];

    public Role Role { get; set; }

    public Status Status { get; set; } = Status.Active;

    [Required]
    public long Balance { get; set; }

    public long BalanceDelta { get; set; } = 0;
}
