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
    public required BlackjackStage CurrentStage { get; set; }
}

/// <summary>
/// Specific DTO for a player action event.
/// Provides some context about the action taken by a player that any action might use.
/// </summary>
public record PlayerActionEventData : IRoomEventData
{
    public Guid PlayerId { get; set; }
    public int HandIndex { get; set; } = 0;
    public string Action { get; set; } = string.Empty;

    // TODO: make event data type-safe
    public long? Amount { get; set; } = 0;
    public List<CardDTO>? Cards { get; set; }
    public Guid? TargetPlayerId { get; set; }
    public bool? Success { get; set; } = true;
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
/// Specific DTO for a host change event (if the current host leaves)
/// </summary>
public record HostChangeEventData : IRoomEventData
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
    public Guid PlayerId { get; set; }
    public int HandIndex { get; set; } = 0;
    public List<CardDTO> PlayerHand { get; set; } = [];
    public int PlayerScore { get; set; }
}
