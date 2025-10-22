namespace Project.Api.Models.Games;

/// <summary>
/// Represents the configuration for a blackjack game.
/// </summary>
public record BlackjackConfig : GameConfig
{
    public long StartingBalance { get; set; } = 1000;
    public long MinBet { get; set; } = 0;
    public TimeSpan BettingTimeLimit { get; set; } = TimeSpan.FromSeconds(60);
    public override TimeSpan TurnTimeLimit { get; set; } = TimeSpan.FromSeconds(30);
}
