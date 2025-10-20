using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Enums;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;

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

/*

problem:
  a REST API is stateless, so there's no way to have a "timer" for game phases.
  this could lead to a long delay if a player never moves.

solution (the realistic one):
  use something like Redis pub/sub to broadcast a delayed message that acts as a timer

solution (the hacky one):
  have the initial request handler that started the betting phase start a timer and trigger the next game phase
  (this could be brittle if the server crashes or restarts)

solution (the funny one):
  have a "hurry up" button that triggers the next game phase if the time is past the deadline
  (could be combined with prev, but could lead to a race condition)

*/

public class BlackjackService(
    IRoomRepository roomRepository,
    IRoomPlayerRepository roomPlayerRepository,
    IUserRepository userRepository
) : IBlackjackService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository = roomPlayerRepository;
    private readonly IUserRepository _userRepository = userRepository;

    private BlackjackConfig _config = new();
    public BlackjackConfig Config
    {
        get => _config;
        set => _config = value;
    }

    public async Task<BlackjackState> GetGameStateAsync(Guid roomId)
    {
        string stateString = await _roomRepository.GetGameStateAsync(roomId);

        return JsonSerializer.Deserialize<BlackjackState>(stateString)!;
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
        Guid roomId,
        Guid playerId,
        string action,
        JsonElement data
    )
    {
        // ensure action is valid for this stage
        BlackjackState state = await GetGameStateAsync(roomId);
        if (!IsActionValid(action, state.Stage))
        {
            return false; // or throw an exception
        }

        // check if player is in the room
        RoomPlayer player =
            await _roomPlayerRepository.GetByRoomIdAndUserIdAsync(roomId, playerId)
            ?? throw new NotFoundException("Player not found.");

        BlackjackActionDTO actionDTO = data.ToBlackjackAction(action);

        // do the action :)
        switch (actionDTO)
        {
            case BetAction betAction:
                // check if player has enough chips
                if (player.Balance < betAction.Amount)
                {
                    return false; // or throw an exception
                }

                BlackjackBettingStage stage = (BlackjackBettingStage)state.Stage;

                // set bet in gamestate
                stage.Bets[player.Id] = betAction.Amount;

                player.Status = Status.Active;

                await _roomPlayerRepository.UpdateAsync(player);

                // if not past deadline, do not move to next stage
                if (DateTime.UtcNow < stage.Deadline)
                {
                    return true;
                }

                // all bets final
                foreach (Guid better in stage.Bets.Keys)
                {
                    await _roomPlayerRepository.UpdatePlayerBalanceAsync(
                        better,
                        stage.Bets[better]
                    );
                }

                // move to next stage
                state.Stage = new BlackjackPlayerActionStage(0);
                await _roomRepository.UpdateGameStateAsync(roomId, JsonSerializer.Serialize(state));

                return true;
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
    }
}
