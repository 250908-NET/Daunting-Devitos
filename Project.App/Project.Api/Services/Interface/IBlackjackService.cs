using Project.Api.Models.Games;

namespace Project.Api.Services.Interface;

/// <summary>
/// Represents a service for handling blackjack logic.
/// </summary>
public interface IBlackjackService : IGameService<BlackjackState, BlackjackConfig> { }
