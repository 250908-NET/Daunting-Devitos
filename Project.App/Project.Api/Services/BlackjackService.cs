using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Services.Interface;

namespace Project.Api.Services;

/*

set up deck API connection
set up game configs

loop
    shuffle deck(?)

    loop
        if everyone has bet/left or time is up
            break
        end if
        wait
    end loop
    deduct bets

    deal 2 cards to each player
    deal 2 cards to dealer (one hidden)

    loop (foreach player)
        loop
            if hit
                deal card
            else if stand
                break
            end if
        end loop
    end loop

    deal to dealer (hit until 17)
    calculate scores
    determine outcomes
    distribute winnings
end loop

teardown
close room

*/

#region Blackjack Game Stages

// initial setup
// initialize deck, set game configs
public record BlackjackInitStage : BlackjackStage;

// doing pre-round setup
public record BlackjackSetupStage : BlackjackStage;

// waiting for players to bet
public record BlackjackBettingStage : BlackjackStage;

// dealing
public record BlackjackDealingStage : BlackjackStage;

// player turn
// TODO: figure out how turn order will work
public record BlackjackPlayerActionStage(int Index) : BlackjackStage;

// dealer turn and distribute winnings
public record BlackjackFinishRoundStage : BlackjackStage;

// teardown, close room
public record BlackjackTeardownStage : BlackjackStage;

#endregion

public class BlackjackService : IBlackjackService
{
    public async Task<BlackjackState> GetGamestateAsync(string gameId)
    {
        throw new NotImplementedException();
    }

    public static bool IsActionValid(string action, BlackjackStage stage) =>
        action switch
        {
            "bet" => stage is BlackjackBettingStage,
            "hit" => stage is BlackjackPlayerActionStage,
            "stand" => stage is BlackjackPlayerActionStage,
            "double" => stage is BlackjackPlayerActionStage,
            "split" => stage is BlackjackPlayerActionStage,
            "surrender" => stage is BlackjackPlayerActionStage,
            _ => false,
        };

    public async Task<bool> PerformActionAsync(
        string gameId,
        string playerId,
        string action,
        JsonElement data
    )
    {
        // ensure action is valid for this stage
        BlackjackState state = await GetGamestateAsync(gameId);

        if (!IsActionValid(action, state.Stage))
        {
            return false; // or throw an exception
        }

        BlackjackActionDTO actionDTO = data.ToBlackjackAction(action);

        switch (actionDTO)
        {
            case BetAction betAction:

                throw new NotImplementedException();
            case HitAction hitAction:
                throw new NotImplementedException();
            case StandAction standAction:
                throw new NotImplementedException();
            case DoubleAction doubleAction:
                throw new NotImplementedException();
            case SplitAction splitAction:
                throw new NotImplementedException();
            case SurrenderAction surrenderAction:
                throw new NotImplementedException();
            default:
                throw new NotImplementedException();
        }

        throw new NotImplementedException();
    }
}
