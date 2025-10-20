using Project.Api.Models.Games;

namespace Project.Api.Services.Interface;

public record BlackjackState : GameState<BlackjackStage>
{
    public required List<object> DealerHand { get; set; } = [];
    public required BlackjackStage Stage { get; set; }
}

/// <summary>
/// Represents a service for handling blackjack logic.
/// </summary>
public interface IBlackjackService : IGameService<BlackjackState> { }
