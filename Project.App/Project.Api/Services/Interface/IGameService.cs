using System.Text.Json;

namespace Project.Api.Services.Interface;

/// <summary>
/// Used to represent the current stage of a game, eg. "setup", "dealing", etc.
/// Each game should have their own "parent" abstract class that extends this class to ensure type safety.
/// </summary>
public abstract record GameStage;

/// <summary>
/// Represents the current state of a game, including all relevant information.
/// Can be extended to include more information for a specific game type.
/// </summary>
public record GameState<TStage>
    where TStage : GameStage
{
    public required TStage CurrentStage { get; set; }
}

/// <summary>
/// A service for handling game logic.
/// Actions are async and stateless, so for each request, the service needs to get the current game state and
/// act accordingly, either by updating the game state or returning an error.
/// </summary>
public interface IGameService<TState>
{
    Task<TState> GetGamestateAsync(string gameId);

    /// <summary>
    /// Performs a user action on the game, if valid, then updates the game state.
    /// Each game implementation provides their own main loop logic implementation, instead of being restricted to a
    /// specific structure.
    /// </summary>
    /// <param name="action">The action to perform</param>
    /// <param name="data">A JSON object containing the action data</param>
    Task<bool> PerformActionAsync(string gameId, string playerId, string action, JsonElement data);
}
