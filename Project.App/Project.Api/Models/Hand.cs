using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Api.Models;

public class Hand
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid RoomPlayerId { get; set; }

    public int Order { get; set; }

    public int Bet { get; set; }

    [ForeignKey("RoomPlayerId")]
    public virtual RoomPlayer? RoomPlayer { get; set; }
}
