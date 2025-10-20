using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
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

public class BlackjackService(IRoomRepository roomRepository, IUserRepository userRepository)
    : IBlackjackService
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IUserRepository _userRepository = userRepository;

    private BlackjackConfig _config = new();
    public BlackjackConfig Config
    {
        get => _config;
        set => _config = value;
    }

    public async Task<BlackjackState> GetGamestateAsync(Guid gameId)
    {
        string stateString = await _roomRepository.GetGameStateAsync(gameId);

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
        Guid gameId,
        Guid playerId,
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

        // check if player exists
        User player =
            await _userRepository.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("Player does not exist.");

        BlackjackActionDTO actionDTO = data.ToBlackjackAction(action);

        // do the action :)
        switch (actionDTO)
        {
            case BetAction betAction:

                // add player to room, if they're not already there

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
