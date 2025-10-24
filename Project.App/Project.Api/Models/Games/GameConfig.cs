using System.Text.Json.Serialization;

namespace Project.Api.Models.Games;

/// <summary>
/// Represents the configuration for a game.
/// Should be extended with settings for a specific game type.
/// </summary>
[JsonDerivedType(typeof(BlackjackConfig), typeDiscriminator: "blackjack")]
public abstract record GameConfig
{
    public virtual int? MaxPlayers { get; set; }
    public virtual int MinPlayers { get; set; } = 1;
    public virtual TimeSpan TurnTimeLimit { get; set; } = TimeSpan.FromSeconds(30);
}
