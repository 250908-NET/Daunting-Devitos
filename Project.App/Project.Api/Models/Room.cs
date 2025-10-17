using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Api.Models;

public class Room
{
    [Key]
    public Guid Id { get; set; }

    public Guid HostId { get; set; }

    public bool isPublic { get; set; }

    public bool isActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    [MaxLength(50)]
    public required string GameMode { get; set; }

    public required string GameState { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int MaxPlayers { get; set; }

    public int MinPlayers { get; set; }

    public int DeckId { get; set; }

    public int Round { get; set; }

    public string State { get; set; } = string.Empty;

    [ForeignKey("HostId")]
    public virtual User? Host { get; set; }

    public virtual ICollection<RoomPlayer> RoomPlayers { get; set; } = [];
}
