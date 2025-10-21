using System.Text.Json;

namespace Project.Api.DTOs;

/// <summary>
/// The base class for all blackjack-specific action DTOs.
/// </summary>
public abstract record BlackjackActionDTO : GameActionDTO;

public record BetAction(long Amount) : BlackjackActionDTO;

public record HitAction : BlackjackActionDTO;

public record StandAction : BlackjackActionDTO;

public record DoubleAction : BlackjackActionDTO;

public record SplitAction(long Amount) : BlackjackActionDTO;

public record SurrenderAction : BlackjackActionDTO;

public static class JsonElementExtensions
{
    /// <summary>
    /// Extension method to deserialize a <see cref="BlackjackActionDTO"/> from a <see cref="JsonElement"/>.
    /// Any errors thrown during deserialization will be passed to the caller.
    /// </summary>
    public static BlackjackActionDTO ToBlackjackAction(this JsonElement element, string action)
    {
        return (
                action switch
                {
                    "bet" => element.Deserialize<BetAction>(),
                    "hit" => element.Deserialize<HitAction>(),
                    "stand" => element.Deserialize<StandAction>(),
                    "double" => element.Deserialize<DoubleAction>(),
                    "split" => element.Deserialize<SplitAction>(),
                    "surrender" => (BlackjackActionDTO?)element.Deserialize<SurrenderAction>(),
                    _ => throw new NotSupportedException(
                        $"Action '{action}' is not a valid action for Blackjack."
                    ),
                }
            ) ?? throw new InvalidOperationException($"Could not deserialize action {action}.");
    }
}
