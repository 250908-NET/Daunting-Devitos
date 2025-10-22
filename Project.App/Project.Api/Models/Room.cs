using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Project.Api.Models;

public class Room
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid HostId { get; set; }

    public bool IsPublic { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    [MaxLength(50)]
    public required string GameMode { get; set; }

    public string GameConfig { get; set; } = string.Empty;

    public required string GameState { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int MaxPlayers { get; set; }

    public int MinPlayers { get; set; }

    public string DeckId { get; set; } = string.Empty;

    public int Round { get; set; }

    public string State { get; set; } = string.Empty;

    [ForeignKey("HostId")]
    public virtual User? Host { get; set; }

    public virtual ICollection<RoomPlayer> RoomPlayers { get; set; } = [];

    public byte[] RowVersion { get; set; } = []; // concurrency
}

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
