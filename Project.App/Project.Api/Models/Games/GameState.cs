namespace Project.Api.Models.Games;

/// <summary>
/// Used to represent the current stage of a game, eg. "setup", "dealing", etc.
/// Each game should have their own "parent" abstract class that extends this class to ensure type safety.
/// </summary>
public abstract record GameStage;

/// <summary>
/// Represents the current state of a game, including all relevant information.
/// Should be extended to include more information for a specific game type.
/// </summary>
public abstract record GameState<TStage> : IGameState
    where TStage : GameStage
{
    public required TStage CurrentStage { get; set; }
    GameStage IGameState.CurrentStage => CurrentStage;
}

// literally just here so i don't need to define a service using both state and stage,
// since state already includes stage
public interface IGameState
{
    GameStage CurrentStage { get; }
}
