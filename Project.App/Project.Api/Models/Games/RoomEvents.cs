using Project.Api.DTOs;

namespace Project.Api.Models.Games;

/// <summary>
/// Base interface for broadcast game events
/// </summary>
public interface IRoomEventData { }

/// <summary>
/// Specific DTO for a chat message event
/// </summary>
public class MessageEventData : IRoomEventData
{
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Specific DTO for a game state update event
/// </summary>
public record GameStateUpdateEventData : IRoomEventData
{
    public string GameStateJson { get; set; } = string.Empty;
    public int Version { get; set; } = 0;
}

/// <summary>
/// Specific DTO for a player action event
/// </summary>
public record PlayerActionEventData : IRoomEventData
{
    public Guid PlayerId { get; set; }
    public string Action { get; set; } = string.Empty;
    public object? ActionDetails { get; set; } // e.g., BetAction, HitAction details
}

/// <summary>
/// Specific DTO for a player join event
/// </summary>
public record PlayerJoinEventData : IRoomEventData
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
}

/// <summary>
/// Specific DTO for a player leave event
/// </summary>
public record PlayerLeaveEventData : IRoomEventData
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
}

/// <summary>
/// Specific DTO for revealing the dealer's cards
/// </summary>
public record DealerRevealEventData : IRoomEventData
{
    public List<CardDTO> DealerHand { get; set; } = [];
    public int DealerScore { get; set; }
}

/// <summary>
/// Specific DTO for revealing scores at the end of a round
/// </summary>
public record PlayerRevealEventData : IRoomEventData
{
    public List<CardDTO> PlayerHand { get; set; } = [];
    public int PlayerScore { get; set; }
}
