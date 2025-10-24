using System.Text.Json;
using Project.Api.Models.Games;

namespace Project.Api.Services.Interface;

/// <summary>
/// A service for handling game logic.
/// Actions are async and stateless, so for each request, the service needs to get the current game state and
/// act accordingly, either by updating the game state or returning an error.
/// </summary>
public interface IGameService<TState, TConfig>
    where TState : IGameState
    where TConfig : GameConfig
{
    /// <summary>
    /// The unique identifier for the game mode handled by the service.
    /// </summary>
    string GameMode { get; }

    Task<TConfig> GetConfigAsync(Guid gameId);

    Task SetConfigAsync(Guid gameId, TConfig config);

    Task<TState> GetGameStateAsync(Guid gameId);

    /// <summary>
    /// Sets up the game with initial state.
    /// </summary>
    Task StartGameAsync(Guid gameId, TConfig config);

    /// <summary>
    /// Adds a player to the game.
    /// </summary>
    Task PlayerJoinAsync(Guid gameId, Guid playerId);

    /// <summary>
    /// Removes a player from the game.
    /// If the host leaves, a new host is selected.
    /// If the last player leaves, the game is marked as inactive.
    /// </summary>
    Task PlayerLeaveAsync(Guid gameId, Guid playerId);

    /// <summary>
    /// Performs a user action on the game, if valid, then updates the game state.
    /// Each game implementation provides their own main loop logic implementation, instead of being restricted to a
    /// specific structure.
    /// </summary>
    /// <param name="action">The action to perform</param>
    /// <param name="data">A JSON object containing the action data</param>
    /// <throws cref="ApiException">Thrown if the action is invalid</throws>
    Task PerformActionAsync(Guid gameId, Guid playerId, string action, JsonElement data);
}
