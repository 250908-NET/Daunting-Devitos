namespace Project.Api.Models.Games;

/// <summary>
/// Represents the configuration for a game.
/// Should be extended with settings for a specific game type.
/// </summary>
public abstract record GameConfig
{
    public int? MaxPlayers { get; set; }
    public int MinPlayers { get; set; } = 1;
    public TimeSpan TurnTimeLimit { get; set; } = TimeSpan.FromSeconds(30);
}
