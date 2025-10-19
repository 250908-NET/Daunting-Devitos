namespace Project.Api.Models.Games;

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
