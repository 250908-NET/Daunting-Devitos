namespace Project.Api.Models.Games;

/// <summary>
/// Represents the configuration for a blackjack game.
/// </summary>
public record BlackjackConfig : GameConfig
{
    public int? MaxPlayers { get; set; }
    public int MinPlayers { get; set; } = 1;
    public bool BuyIn { get; set; } = false;
    public int StartingBalance { get; set; } = 1000;
}
