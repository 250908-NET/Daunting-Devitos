using System.Text.Json.Serialization;

namespace Project.Api.Models.Games;

public record BlackjackState : GameState<BlackjackStage>
{
    public string DealerHand { get; set; } = "";
    public Dictionary<Guid, long> Bets { get; set; } = [];
}

[JsonDerivedType(typeof(BlackjackInitStage), typeDiscriminator: "init")]
[JsonDerivedType(typeof(BlackjackSetupStage), typeDiscriminator: "setup")]
[JsonDerivedType(typeof(BlackjackBettingStage), typeDiscriminator: "betting")]
[JsonDerivedType(typeof(BlackjackDealingStage), typeDiscriminator: "dealing")]
[JsonDerivedType(typeof(BlackjackPlayerActionStage), typeDiscriminator: "player_action")]
[JsonDerivedType(typeof(BlackjackFinishRoundStage), typeDiscriminator: "finish_round")]
[JsonDerivedType(typeof(BlackjackTeardownStage), typeDiscriminator: "teardown")]
public abstract record BlackjackStage : GameStage;

// initial setup
// initialize deck, set game configs
public record BlackjackInitStage : BlackjackStage;

// doing pre-round setup
public record BlackjackSetupStage : BlackjackStage;

// waiting for players to bet
public record BlackjackBettingStage(DateTimeOffset Deadline, Dictionary<Guid, long> Bets)
    : BlackjackStage;

// dealing
public record BlackjackDealingStage : BlackjackStage;

// player turn
public record BlackjackPlayerActionStage(DateTimeOffset Deadline, int PlayerIndex, int HandIndex)
    : BlackjackStage;

// dealer turn and distribute winnings
public record BlackjackFinishRoundStage : BlackjackStage;

// teardown, close room
public record BlackjackTeardownStage : BlackjackStage;

public static class BlackjackStateExtensions
{
    /// <summary>
    /// Resets the deadline of a betting stage to the current time plus the given duration.
    /// </summary>
    public static BlackjackBettingStage ResetDeadline(
        this BlackjackBettingStage stage,
        TimeSpan duration
    )
    {
        return stage with { Deadline = DateTimeOffset.UtcNow + duration };
    }

    /// <summary>
    /// Resets the deadline of a player action stage to the current time plus the given duration.
    /// </summary>
    public static BlackjackPlayerActionStage ResetDeadline(
        this BlackjackPlayerActionStage stage,
        TimeSpan duration
    )
    {
        return stage with { Deadline = DateTimeOffset.UtcNow + duration };
    }
}
