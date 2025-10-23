using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Api.Models;

public class Hand
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid RoomPlayerId { get; set; }

    public required int Order { get; set; } // order of the player in the game

    public int HandNumber { get; set; } = 0; // number of the hand for the player

    public long Bet { get; set; }

    [ForeignKey("RoomPlayerId")]
    public virtual RoomPlayer? RoomPlayer { get; set; }
}
