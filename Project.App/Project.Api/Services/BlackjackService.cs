using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Enums;
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

        // check if player exists
        User player =
            await _userRepository.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("Player does not exist.");

        BlackjackActionDTO actionDTO = data.ToBlackjackAction(action);

        // do the action :)
        switch (actionDTO)
        {
            case BetAction betAction:
                // add player to room, if they're not already
                RoomPlayer? roomPlayer = await GetOrAddPlayerToRoomAsync(roomId, playerId);
                if (roomPlayer is null)
                {
                    return false;
                }

                // deduct bet account from player's room balance
                roomPlayer.Balance -= betAction.Amount;
                await _roomPlayerRepository.UpdateAsync(roomPlayer);

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

    /// <summary>
    /// Gets a player from the room, or adds them if they're not already in the room.
    /// </summary>
    /// <returns>The retrieved or added player</returns>
    public async Task<RoomPlayer?> GetOrAddPlayerToRoomAsync(Guid roomId, Guid playerId)
    {
        RoomPlayer? roomPlayer = await _roomPlayerRepository.GetByRoomAndUserAsync(
            roomId,
            playerId
        );

        if (
            roomPlayer is null
            && (await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId)).Count()
                >= Config.MaxPlayers
        )
        {
            roomPlayer = new RoomPlayer
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = playerId,
                Role = Role.Player,
                Balance = _config.StartingBalance,
            };
            roomPlayer = await _roomPlayerRepository.CreateAsync(roomPlayer);
        }

        return roomPlayer;
    }
}
