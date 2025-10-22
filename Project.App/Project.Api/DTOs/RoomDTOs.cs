namespace Project.Api.DTOs;

public class RoomDTO
{
    public Guid Id { get; set; }
    public Guid HostId { get; set; }
    public bool IsPublic { get; set; }
    public string GameMode { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string GameConfig { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxPlayers { get; set; }
    public int MinPlayers { get; set; }
    public string DeckId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateRoomDTO
{
    public Guid HostId { get; set; }
    public bool IsPublic { get; set; }
    public string GameMode { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string GameConfig { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxPlayers { get; set; }
    public int MinPlayers { get; set; }
    public string DeckId { get; set; } = string.Empty;
}

public class UpdateRoomDTO : CreateRoomDTO
{
    public Guid Id { get; set; }
}
