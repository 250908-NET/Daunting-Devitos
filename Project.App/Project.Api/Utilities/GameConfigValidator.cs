using Project.Api.Models.Games;

namespace Project.Api.Utilities;

/// <summary>
/// Validates game configuration objects.
/// </summary>
public static class GameConfigValidator
{
    /// <summary>
    /// Validates a BlackjackConfig and throws BadRequestException if invalid.
    /// </summary>
    public static void ValidateBlackjackConfig(BlackjackConfig config)
    {
        if (config.StartingBalance <= 0)
            throw new BadRequestException(
                $"Starting balance must be positive. Got: {config.StartingBalance}"
            );

        if (config.MinBet < 0)
            throw new BadRequestException($"Minimum bet cannot be negative. Got: {config.MinBet}");

        if (config.MinBet > config.StartingBalance)
            throw new BadRequestException(
                $"Minimum bet ({config.MinBet}) cannot exceed starting balance ({config.StartingBalance})."
            );

        if (config.BettingTimeLimit <= TimeSpan.Zero)
            throw new BadRequestException(
                $"Betting time limit must be positive. Got: {config.BettingTimeLimit}"
            );

        if (config.TurnTimeLimit <= TimeSpan.Zero)
            throw new BadRequestException(
                $"Turn time limit must be positive. Got: {config.TurnTimeLimit}"
            );

        if (config.MinPlayers < 1)
            throw new BadRequestException(
                $"Minimum players must be at least 1. Got: {config.MinPlayers}"
            );

        if (config.MaxPlayers.HasValue && config.MaxPlayers.Value < config.MinPlayers)
            throw new BadRequestException(
                $"Maximum players ({config.MaxPlayers}) cannot be less than minimum players ({config.MinPlayers})."
            );
    }
}
