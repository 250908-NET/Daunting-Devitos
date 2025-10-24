using System.Text.Json;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Models.Games;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Constants;
using Project.Api.Utilities.Enums;
using Project.Api.Utilities.Extensions;

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
    IHandRepository handRepository,
    IDeckApiService deckApiService,
    IRoomSSEService roomSSEService,
    IUserRepository userRepository,
    ILogger<BlackjackService> logger
) : IGameService<IGameState, GameConfig>
{
    private readonly IRoomRepository _roomRepository = roomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository = roomPlayerRepository; // TODO: use transactions for long-running tasks
    private readonly IHandRepository _handRepository = handRepository;
    private readonly IDeckApiService _deckApiService = deckApiService;
    private readonly IRoomSSEService _roomSSEService = roomSSEService;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILogger<BlackjackService> _logger = logger; // TODO: add more logging

    public string GameMode => GameModes.Blackjack;

    public async Task<GameConfig> GetConfigAsync(Guid gameId)
    {
        string configString = await _roomRepository.GetGameConfigAsync(gameId);

        if (string.IsNullOrWhiteSpace(configString))
            return new BlackjackConfig();

        return JsonSerializer.Deserialize<BlackjackConfig>(configString) ?? new BlackjackConfig();
    }

    public async Task SetConfigAsync(Guid gameId, GameConfig inputConfig)
    {
        if (inputConfig is not BlackjackConfig config)
            throw new ArgumentException("Config must be of type BlackjackConfig.");

        string configString = JsonSerializer.Serialize(config);
        await _roomRepository.UpdateGameConfigAsync(gameId, configString);
    }

    public async Task<IGameState> GetGameStateAsync(Guid roomId)
    {
        string stateString = await _roomRepository.GetGameStateAsync(roomId);

        return JsonSerializer.Deserialize<BlackjackState>(stateString)
            ?? throw new InternalServerException("Failed to deserialize game state.");
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
            "hurry_up" => stage is BlackjackBettingStage or BlackjackPlayerActionStage,
            _ => false,
        };

    public async Task StartGameAsync(Guid roomId, GameConfig inputConfig)
    {
        if (inputConfig is not BlackjackConfig config)
            throw new ArgumentException("Config must be of type BlackjackConfig.");

        // create initial game state
        BlackjackState initialState = new()
        {
            CurrentStage = new BlackjackBettingStage(
                DateTimeOffset.UtcNow + config.BettingTimeLimit,
                []
            ),
        };

        // initialize player balances
        await _roomPlayerRepository.UpdatePlayersInRoomAsync(
            roomId,
            player =>
            {
                player.Balance = config.StartingBalance;
                player.Status = Status.Away; // Players start as Away, become Active when they bet
            }
        );

        // create new deck
        string deckId = await _deckApiService.CreateDeck();

        // update room
        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");

        room.GameConfig = JsonSerializer.Serialize(config);
        room.StartedAt = DateTimeOffset.UtcNow;
        room.DeckId = deckId;
        room.GameState = JsonSerializer.Serialize(initialState);
        room.IsActive = true;
        room.Round = 1;

        await _roomRepository.UpdateAsync(room);

        // broadcast initial game state, do not save (since it's already saved)
        await initialState.SaveStateAndBroadcastAsync(roomId, roomSSEService: _roomSSEService);
    }

    public async Task PlayerJoinAsync(Guid roomId, Guid playerId)
    {
        if (await GetConfigAsync(roomId) is not BlackjackConfig config)
            throw new InternalServerException("Failed to get game config.");

        // validate max player count
        int playerCount = await _roomPlayerRepository.GetPlayerCountInRoomAsync(roomId);
        if (playerCount >= config.MaxPlayers)
        {
            throw new BadRequestException(
                $"Room {roomId} is full ({playerCount}/{config.MaxPlayers})."
            );
        }

        // get user (for name)
        User player =
            await _userRepository.GetByIdAsync(playerId)
            ?? throw new NotFoundException($"User {playerId} not found.");

        // check if player is already in room
        if (
            await _roomPlayerRepository.GetByRoomIdAndUserIdAsync(roomId, playerId)
            is RoomPlayer existingPlayer
        )
        {
            // if player is inactive or left, mark as away
            if (existingPlayer.Status is Status.Inactive or Status.Left)
            {
                existingPlayer.Status = Status.Away;

                // if out of chips and balance reset is enabled, set balance to initial amount
                if (config.AllowBalanceReset && existingPlayer.Balance <= 0)
                {
                    existingPlayer.Balance = config.StartingBalance;
                }

                await _roomPlayerRepository.UpdateAsync(existingPlayer);
            }
        }
        else
        {
            // player is new, create new room player
            await _roomPlayerRepository.CreateAsync(
                new RoomPlayer
                {
                    RoomId = roomId,
                    UserId = playerId,
                    Balance = config.StartingBalance,
                    Status = Status.Away,
                }
            );
        }

        // broadcast player join
        await _roomSSEService.BroadcastEventAsync(
            roomId,
            RoomEventType.PlayerJoin,
            new PlayerJoinEventData() { PlayerId = playerId, PlayerName = player.Name }
        );
    }

    public async Task PlayerLeaveAsync(Guid gameId, Guid playerId)
    {
        // mark user as left
        RoomPlayer roomPlayer =
            await _roomPlayerRepository.GetByRoomIdAndUserIdAsync(gameId, playerId)
            ?? throw new BadRequestException($"Player {playerId} not found in room {gameId}.");

        roomPlayer.Status = Status.Left;
        await _roomPlayerRepository.UpdateAsync(roomPlayer);

        // get user
        User player =
            await _userRepository.GetByIdAsync(playerId)
            ?? throw new NotFoundException($"User {playerId} not found.");

        // get room
        Room room =
            await _roomRepository.GetByIdAsync(gameId)
            ?? throw new NotFoundException($"Room {gameId} not found.");

        // broadcast player leave
        await _roomSSEService.BroadcastEventAsync(
            gameId,
            RoomEventType.PlayerLeave,
            new PlayerLeaveEventData() { PlayerId = playerId, PlayerName = player.Name }
        );

        // if player was host, find a new host
        if (room.HostId == playerId)
        {
            // get list of players that are active or away
            List<RoomPlayer> availablePlayers =
            [
                .. (await _roomPlayerRepository.GetByRoomIdAsync(gameId)).Where(p =>
                    p.Status == Status.Active || p.Status == Status.Away
                ),
            ];

            if (availablePlayers.Count == 0)
            {
                // no active players, mark room as inactive
                room.IsActive = false;
                await _roomRepository.UpdateAsync(room);
            }
            else
            {
                // otherwise, make the first active player the new host
                room.HostId = availablePlayers[0].UserId;
                await _roomRepository.UpdateAsync(room);

                // broadcast new host
                await _roomSSEService.BroadcastEventAsync(
                    gameId,
                    RoomEventType.HostChange,
                    new HostChangeEventData() { PlayerId = playerId, PlayerName = player.Name }
                );
            }
        }
    }

    public async Task PerformActionAsync(
        Guid roomId,
        Guid playerId,
        string action,
        JsonElement data
    )
    {
        // ensure action is valid for this stage
        if (await GetGameStateAsync(roomId) is not BlackjackState state)
            throw new InternalServerException("Failed to get game state.");

        if (!IsActionValid(action, state.CurrentStage))
        {
            throw new BadRequestException(
                $"Action {action} is not a valid action for this game stage."
            );
        }

        // check if player is in the room
        RoomPlayer player =
            await _roomPlayerRepository.GetByRoomIdAndUserIdAsync(roomId, playerId)
            ?? throw new BadRequestException($"Player {playerId} not found.");

        BlackjackActionDTO actionDTO = data.ToBlackjackAction(action);

        // get config
        if (await GetConfigAsync(roomId) is not BlackjackConfig config)
            throw new InternalServerException("Failed to get game config.");

        // do the action :)
        switch (actionDTO)
        {
            case BetAction betAction:
                await ProcessBetAsync(state, roomId, player, betAction.Amount);

                BlackjackBettingStage bettingStage = (BlackjackBettingStage)state.CurrentStage;

                // if past deadline, move to next stage
                if (DateTimeOffset.UtcNow > bettingStage.Deadline)
                {
                    await StartRoundAsync(state, roomId);
                    break;
                }

                // if all players that are active or away have bet, start the round
                List<RoomPlayer> players =
                [
                    .. await _roomPlayerRepository.GetByRoomIdAsync(roomId),
                ];
                Dictionary<Guid, long> bets = bettingStage.Bets;
                if (
                    players
                        .Where(p => p.Status == Status.Active || p.Status == Status.Away)
                        .All(p => bets.ContainsKey(p.Id))
                    && bets.Count > 0
                )
                {
                    await StartRoundAsync(state, roomId);
                    break;
                }

                // otherwise, do nothing
                break;
            case HitAction:
                (state, bool busted) = await DoHitAsync(state, roomId, player);

                if (busted)
                {
                    await NextHandOrFinishRoundAsync(state, roomId);
                }
                else
                {
                    // stay on the same player's turn, but reset the deadline
                    ((BlackjackPlayerActionStage)state.CurrentStage).ResetDeadline(
                        config.TurnTimeLimit
                    );
                    await state.SaveStateAndBroadcastAsync(
                        roomId,
                        _roomRepository,
                        _roomSSEService
                    );
                }
                break;

            case StandAction:
                // do nothing :)

                // broadcast player action (stand)
                BlackjackPlayerActionStage stage = (BlackjackPlayerActionStage)state.CurrentStage;
                Hand hand = await _handRepository.GetHandByRoomOrderAsync(
                    roomId,
                    stage.PlayerIndex,
                    stage.HandIndex
                );

                await _roomSSEService.BroadcastPlayerActionAsync(
                    roomId,
                    player.UserId,
                    hand.HandNumber,
                    "stand"
                );

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
            case DoubleAction:
                await DoDoubleAsync(state, roomId, player);

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                // throw new NotImplementedException(); // This line should be removed if DoDoubleAsync is fully implemented
                break;
            case SplitAction splitAction:
                await DoSplitAsync(state, roomId, player, splitAction.Amount);

                // stay on the same player's turn, but reset the deadline
                ((BlackjackPlayerActionStage)state.CurrentStage).ResetDeadline(
                    config.TurnTimeLimit
                );
                await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);
                break;
            case SurrenderAction:
                await DoSurrenderAsync(state, roomId, player);

                // next player or next stage
                await NextHandOrFinishRoundAsync(state, roomId);
                break;
            case HurryUpAction:
                switch (state.CurrentStage)
                {
                    case BlackjackBettingStage:
                        // check if all active players have bet
                        List<RoomPlayer> activePlayersList =
                        [
                            .. await _roomPlayerRepository.GetActivePlayersInRoomAsync(roomId),
                        ];
                        if (activePlayersList.Count != state.Bets.Count)
                        {
                            throw new BadRequestException("Not all active players have bet yet.");
                        }

                        // start the round
                        await StartRoundAsync(state, roomId);
                        break;
                    case BlackjackPlayerActionStage actionStage:
                        // check if deadline has passed
                        if (DateTimeOffset.UtcNow < actionStage.Deadline)
                        {
                            // deadline not passed, broadcast failed player action
                            await _roomSSEService.BroadcastPlayerActionAsync(
                                roomId,
                                playerId,
                                ((BlackjackPlayerActionStage)state.CurrentStage).HandIndex,
                                "hurry_up",
                                success: false
                            );
                        }
                        else
                        {
                            // get (in)active player of the current stage
                            Guid inactiveRoomPlayerId = (
                                await _handRepository.GetHandByRoomOrderAsync(
                                    roomId,
                                    actionStage.PlayerIndex,
                                    actionStage.HandIndex
                                )
                            ).RoomPlayerId;
                            RoomPlayer inactivePlayer =
                                await _roomPlayerRepository.GetByIdAsync(inactiveRoomPlayerId)
                                ?? throw new InternalServerException(
                                    $"Could not find current player {inactiveRoomPlayerId} in room {roomId}."
                                );

                            // deadline passed, mark player as inactive
                            inactivePlayer.Status = Status.Inactive;
                            await _roomPlayerRepository.UpdateAsync(inactivePlayer);

                            // broadcast player action
                            await _roomSSEService.BroadcastPlayerActionAsync(
                                roomId,
                                playerId,
                                actionStage.HandIndex,
                                "hurry_up",
                                success: true,
                                targetPlayerId: inactivePlayer.UserId
                            );
                        }

                        // act as if the player stood
                        await NextHandOrFinishRoundAsync(state, roomId, config);
                        break;
                    default:
                        throw new BadRequestException("Nothing to hurry in the current stage.");
                }
                break;
            default:
                throw new BadRequestException("Unrecognized action type.");
        }
    }

    /// <summary>
    /// Process a player's bet during the betting stage.
    /// </summary>
    /// <exception cref="BadRequestException">Thrown if the player does not have enough chips to bet.</exception>
    private async Task ProcessBetAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player,
        long bet
    )
    {
        // check if player has enough chips
        if (player.Balance < bet)
        {
            throw new BadRequestException(
                $"Player {player.UserId} does not have enough chips to bet {bet}."
            );
        }

        BlackjackBettingStage stage = (BlackjackBettingStage)state.CurrentStage;

        // set bet in gamestate
        stage.Bets[player.Id] = bet;

        // update player status
        player.Status = Status.Active;
        await _roomPlayerRepository.UpdateAsync(player);

        // broadcast player action (bet)
        await _roomSSEService.BroadcastPlayerActionAsync(
            roomId,
            player.UserId,
            0,
            "bet",
            amount: bet
        );

        await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);
    }

    /// <summary>
    /// Start the round after betting is complete.
    /// Deduct bets from player balances and move to dealing stage.
    /// </summary>
    private async Task StartRoundAsync(
        BlackjackState state,
        Guid roomId,
        BlackjackConfig? config = null
    )
    {
        config ??=
            await GetConfigAsync(roomId) as BlackjackConfig
            ?? throw new InternalServerException("Failed to get game config.");

        // get bets
        Dictionary<Guid, long> bets = ((BlackjackBettingStage)state.CurrentStage).Bets;

        // move to dealing stage
        state.CurrentStage = new BlackjackDealingStage();
        await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);

        // deduct bets from player balances and initialize hands
        int order = 0;
        foreach ((Guid better, long bet) in bets)
        {
            try
            {
                await _roomPlayerRepository.UpdatePlayerBalanceAsync(better, -bet);

                await _handRepository.CreateHandAsync(
                    new Hand
                    {
                        RoomPlayerId = better,
                        Order = order++,
                        Bet = bet,
                    }
                );
            }
            catch (NotFoundException)
            {
                // a bet was recorded for a player who no longer exists?
                // should not happen, but just in case
                throw new InternalServerException(
                    $"Could not find player {better} to process their bet."
                );
            }
        }

        // get deck ID
        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");
        string deckId = await room.GetOrCreateDeckId(_deckApiService, _roomRepository, _logger);

        // deal initial cards (2 to each player, 2 to dealer, one at a time)
        List<Hand> hands = await _handRepository.GetHandsByRoomIdAsync(roomId);

        if (hands.Count == 0)
        {
            throw new InternalServerException($"No hands found for room {roomId}.");
        }

        // one card at a time, deal 2 rounds to players and dealer
        for (int i = 0; i < 2; i++)
        {
            // deal one card to each hand in order
            foreach (Hand hand in hands.OrderBy(h => h.Order))
            {
                await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
            }

            // deal to dealer
            await _deckApiService.DrawCards(deckId, "dealer", 1);
        }

        // after dealing, broadcast initial player hands
        foreach (Hand hand in hands.OrderBy(h => h.Order))
        {
            List<CardDTO> playerHandCards = await _deckApiService.ListHand(
                deckId,
                $"hand-{hand.Id}"
            );
            RoomPlayer roomPlayer =
                await _roomPlayerRepository.GetByIdAsync(hand.RoomPlayerId)
                ?? throw new InternalServerException($"RoomPlayer {hand.RoomPlayerId} not found.");
            await playerHandCards.BroadcastAsync(
                _roomSSEService,
                roomId,
                roomPlayer.UserId,
                hand.HandNumber
            ); // no hidden cards for players
        }

        // broadcast dealer's visible card (hide second card)
        List<CardDTO> dealerHandCards = await _deckApiService.ListHand(deckId, "dealer");
        await dealerHandCards.BroadcastAsync(_roomSSEService, roomId, hiddenIndices: [1]);

        // move to player action stage
        state.CurrentStage = new BlackjackPlayerActionStage(
            DateTimeOffset.UtcNow + config.TurnTimeLimit,
            0,
            0
        );
        await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);
    }

    /// <summary>
    /// Initializes a player action.
    /// </summary>
    /// <returns>
    /// A tuple containing the current player action stage, the player's hand, and the deck ID.
    /// </returns>
    private async Task<(BlackjackPlayerActionStage, Hand, string)> InitializePlayerActionAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player
    )
    {
        BlackjackPlayerActionStage stage =
            state.CurrentStage as BlackjackPlayerActionStage
            ?? throw new InternalServerException("Current stage is not player action stage.");

        Hand hand = await _handRepository.GetHandByRoomOrderAsync(
            roomId,
            stage.PlayerIndex,
            stage.HandIndex
        );

        // make sure player corresponds to the current turn's player
        if (hand.RoomPlayerId != player.Id)
        {
            throw new BadRequestException("It is not your turn.");
        }

        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");

        return (
            stage,
            hand,
            room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID.")
        );
    }

    /// <summary>
    /// Process a player's "hit" action during their turn.
    /// </summary>
    /// <returns>true if player busted</returns>
    private async Task<(BlackjackState, bool)> DoHitAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player
    )
    {
        (_, Hand hand, string deckId) = await InitializePlayerActionAsync(state, roomId, player);

        // draw one card and add it to player's hand
        List<CardDTO> drawnCards = await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
        List<CardDTO> playerHandCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        // broadcast player action (hit)
        await _roomSSEService.BroadcastPlayerActionAsync(
            roomId,
            player.UserId,
            hand.HandNumber,
            "hit",
            cards: drawnCards
        );

        // broadcast updated player hand
        await playerHandCards.BroadcastAsync(
            _roomSSEService,
            roomId,
            player.UserId,
            hand.HandNumber
        );

        // return state and if player busted
        return (state, playerHandCards.CalculateHandValue() > 21);
    }

    /// <summary>
    /// Process a player's "double" action during their turn, doubling their bet and drawing one card.
    /// </summary>
    /// <remarks>Should end the player's turn.</remarks>
    private async Task DoDoubleAsync(BlackjackState state, Guid roomId, RoomPlayer player)
    {
        (BlackjackPlayerActionStage stage, Hand hand, string deckId) =
            await InitializePlayerActionAsync(state, roomId, player);

        // can only be done on the player's first turn!
        // check if player only has two cards in hand and is on their first hand
        List<CardDTO> handCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        if (stage.HandIndex != 0 || handCards.Count > 2)
        {
            throw new BadRequestException("Double can only be done on the player's first turn.");
        }

        // check if player has enough chips to double their bet
        if (player.Balance < hand.Bet)
        {
            throw new BadRequestException(
                $"Player {player.UserId} does not have enough chips to double their bet."
            );
        }

        // double player's bet (deduct from balance and update gamestate)
        await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, -hand.Bet);
        hand.Bet *= 2;
        await _handRepository.UpdateHandAsync(hand.Id, hand);

        // draw one card and add it to player's hand
        List<CardDTO> drawnCard = await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
        List<CardDTO> playerHandCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        // broadcast player action (double)
        await _roomSSEService.BroadcastPlayerActionAsync(
            roomId,
            player.UserId,
            hand.HandNumber,
            "double",
            cards: drawnCard,
            amount: hand.Bet
        );

        // broadcast updated player hand
        await playerHandCards.BroadcastAsync(
            _roomSSEService,
            roomId,
            player.UserId,
            hand.HandNumber
        );
    }

    /// <summary>
    /// Process a player's "split" action during their turn, splitting their hand into two hands.
    /// </summary>
    /// <remarks>Next turn should be the player's first hand.</remarks>
    private async Task DoSplitAsync(
        BlackjackState state,
        Guid roomId,
        RoomPlayer player,
        long amount
    )
    {
        (BlackjackPlayerActionStage stage, Hand hand, string deckId) =
            await InitializePlayerActionAsync(state, roomId, player);

        // check if player has enough chips to do the new bet
        if (player.Balance < amount)
        {
            throw new BadRequestException(
                $"Player {player.UserId} does not have enough chips to split their bet with {amount}."
            );
        }

        // can only be done on the player's first turn!
        // check if player only has two cards in hand and is on their first hand
        List<CardDTO> handCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        if (stage.HandIndex != 0 || handCards.Count != 2)
        {
            throw new BadRequestException("Split can only be done on the player's first turn.");
        }

        // check if both cards are the same value
        if (handCards[0].Value != handCards[1].Value)
        {
            throw new BadRequestException("Can only split if both cards have the same value.");
        }

        // create new hand with second card
        await _handRepository.CreateHandAsync(
            new()
            {
                RoomPlayerId = hand.RoomPlayerId,
                Order = hand.Order,
                HandNumber = hand.HandNumber + 1,
                Bet = amount,
            }
        );
        Hand newHand = await _handRepository.GetHandByRoomOrderAsync(
            roomId,
            hand.Order,
            hand.HandNumber + 1
        );

        // move second card to new hand
        CardDTO cardToMove = handCards[1];
        await _deckApiService.RemoveFromHand(deckId, $"hand-{hand.Id}", cardToMove.Code);
        await _deckApiService.AddToHand(deckId, $"hand-{newHand.Id}", cardToMove.Code);

        // draw one card for each hand
        await _deckApiService.DrawCards(deckId, $"hand-{hand.Id}", 1);
        await _deckApiService.DrawCards(deckId, $"hand-{newHand.Id}", 1);

        // deduct bet from player's balance
        await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, -amount);

        // broadcast player action (split)
        await _roomSSEService.BroadcastPlayerActionAsync(
            roomId,
            player.UserId,
            hand.HandNumber,
            "split",
            amount: amount
        ); // can't really broadcast both hands in one event

        // broadcast both updated player hands
        List<CardDTO> firstHandCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");
        await firstHandCards.BroadcastAsync(
            _roomSSEService,
            roomId,
            player.UserId,
            hand.HandNumber
        );
        List<CardDTO> secondHandCards = await _deckApiService.ListHand(
            deckId,
            $"hand-{newHand.Id}"
        );
        await secondHandCards.BroadcastAsync(
            _roomSSEService,
            roomId,
            player.UserId,
            newHand.HandNumber
        );
    }

    /// <summary>
    /// Process a player's "surrender" action during their turn, forfeiting half their bet and ending their turn.
    /// </summary>
    private async Task DoSurrenderAsync(BlackjackState state, Guid roomId, RoomPlayer player)
    {
        (BlackjackPlayerActionStage stage, Hand hand, string deckId) =
            await InitializePlayerActionAsync(state, roomId, player);

        List<CardDTO> handCards = await _deckApiService.ListHand(deckId, $"hand-{hand.Id}");

        // can only surrender on first action
        // check if player only has two cards in hand
        if (stage.HandIndex != 0 || handCards.Count > 2)
        {
            throw new BadRequestException("Surrender is only allowed on your first action.");
        }

        // refund half of player's bet
        long refund = hand.Bet / 2;
        await _roomPlayerRepository.UpdatePlayerBalanceAsync(player.Id, refund);

        // broadcast player action (surrender)
        await _roomSSEService.BroadcastPlayerActionAsync(
            roomId,
            player.UserId,
            hand.HandNumber,
            "surrender",
            amount: refund
        );
    }

    /// <summary>
    /// Move to the next player/hand turn, or if no players/hands are left, move to next stage (dealer turn).
    /// </summary>
    private async Task NextHandOrFinishRoundAsync(
        BlackjackState state,
        Guid roomId,
        BlackjackConfig? config = null
    )
    {
        config ??=
            await GetConfigAsync(roomId) as BlackjackConfig
            ?? throw new InternalServerException("Failed to get game config.");

        if (state.CurrentStage is not BlackjackPlayerActionStage)
        {
            throw new InvalidOperationException(
                "Cannot move to next hand when not in player action stage."
            );
        }

        BlackjackPlayerActionStage stage = (BlackjackPlayerActionStage)state.CurrentStage;

        // check if current player's next hand exists
        try
        {
            int nextHandIndex = stage.HandIndex + 1;
            Hand nextHand = await _handRepository.GetHandByRoomOrderAsync(
                roomId,
                stage.PlayerIndex,
                stage.HandIndex + 1
            );

            // if so, move to next hand
            state.CurrentStage = new BlackjackPlayerActionStage(
                DateTimeOffset.UtcNow + config.TurnTimeLimit,
                stage.PlayerIndex,
                stage.HandIndex + 1
            );
            await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);
            return;
        }
        catch (NotFoundException)
        {
            // player has no more hands
        }

        // if not, check if next player's first hand exists
        try
        {
            int nextPlayerIndex = stage.PlayerIndex + 1;
            Hand nextPlayerFirstHand = await _handRepository.GetHandByRoomOrderAsync(
                roomId,
                nextPlayerIndex,
                0
            );

            // if so, move to next player
            state.CurrentStage = new BlackjackPlayerActionStage(
                DateTimeOffset.UtcNow + config.TurnTimeLimit,
                nextPlayerIndex,
                0
            );
            await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);
            return;
        }
        catch (NotFoundException)
        {
            // no more players
        }

        // all players have finished, move to dealer turn and finish round
        await FinishRoundAsync(state, roomId, config);
    }

    // After the players have finished playing, the dealer's hand is resolved by drawing cards until
    // the hand achieves a total of 17 or higher. If the dealer has a total of 17 including an ace valued as 11
    // (a "soft 17"), some games require the dealer to stand while other games require the dealer to hit.
    // The dealer never doubles, splits, or surrenders. If the dealer busts, all players who haven't busted win.
    // If the dealer does not bust, each remaining bet wins if its hand is higher than the dealer's and
    // loses if it is lower. In the case of a tie ("push" or "standoff"), bets are returned without adjustment.
    // A blackjack beats any hand that is not a blackjack, even one with a value of 21.
    private async Task FinishRoundAsync(
        BlackjackState state,
        Guid roomId,
        BlackjackConfig? config = null
    )
    {
        config ??=
            await GetConfigAsync(roomId) as BlackjackConfig
            ?? throw new InternalServerException("Failed to get game config.");

        // transition to finish round stage
        state.CurrentStage = new BlackjackFinishRoundStage();
        await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);

        Room room =
            await _roomRepository.GetByIdAsync(roomId)
            ?? throw new NotFoundException($"Room {roomId} not found.");

        List<CardDTO> dealerHand = await _deckApiService.ListHand(
            room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
            "dealer"
        );

        // hit until dealer has at least 17
        int dealerValue = dealerHand.CalculateHandValue();
        _logger.LogInformation(
            "Room {roomId} - Dealer hand: {dealerHand} ({dealerValue})",
            roomId,
            string.Join(", ", dealerHand.Select(c => c.Code)),
            dealerValue
        );
        while (dealerValue < 17)
        {
            await _deckApiService.DrawCards(
                room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
                "dealer",
                1
            );
            dealerHand = await _deckApiService.ListHand(
                room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
                "dealer"
            );
            dealerValue = dealerHand.CalculateHandValue();
            _logger.LogInformation(
                "Room {roomId} - Dealer hit: {dealerHand} ({dealerValue})",
                roomId,
                string.Join(", ", dealerHand),
                dealerValue
            );
        }

        // broadcast final dealer hand (do not hide any cards)
        await dealerHand.BroadcastAsync(_roomSSEService, roomId);

        // calculate winnings for each player hand
        List<Hand> hands =
        [
            .. (await _handRepository.GetHandsByRoomIdAsync(roomId))
                .OrderBy(h => h.Order)
                .ThenBy(h => h.HandNumber),
        ]; // ensure hands are processed in order
        foreach (Hand hand in hands)
        {
            List<CardDTO> playerHand = await _deckApiService.ListHand(
                room.DeckId ?? throw new InternalServerException($"Room {roomId} has no deck ID."),
                $"hand-{hand.Id}"
            );

            switch (playerHand.CompareHand(dealerHand))
            {
                case > 0:
                    // player wins
                    long winnings = hand.Bet * 2;
                    await _roomPlayerRepository.UpdatePlayerBalanceAsync(
                        hand.RoomPlayerId,
                        winnings
                    );
                    break;
                case 0:
                    // push
                    await _roomPlayerRepository.UpdatePlayerBalanceAsync(
                        hand.RoomPlayerId,
                        hand.Bet
                    );
                    break;
                case < 0:
                    // dealer wins, do nothing
                    break;
            }

            // broadcast final player hand
            RoomPlayer roomPlayer =
                await _roomPlayerRepository.GetByIdAsync(hand.RoomPlayerId)
                ?? throw new InternalServerException($"RoomPlayer {hand.RoomPlayerId} not found.");
            await playerHand.BroadcastAsync(
                _roomSSEService,
                roomId,
                roomPlayer.UserId,
                hand.HandNumber
            );
        }

        // reset player hands
        foreach (Hand hand in hands)
        {
            await _handRepository.DeleteHandAsync(hand.Id);
        }

        // return all cards to deck and shuffle
        string deckId = await room.GetOrCreateDeckId(_deckApiService, _roomRepository, _logger);
        bool success = await _deckApiService.ReturnAllCardsToDeck(deckId);
        if (!success)
        {
            _logger.LogError("Failed to return cards to deck for room {RoomId}, skipping.", roomId);
        }

        // initialize next betting stage
        state.CurrentStage = new BlackjackBettingStage(
            DateTimeOffset.UtcNow + config.BettingTimeLimit,
            []
        );
        await state.SaveStateAndBroadcastAsync(roomId, _roomRepository, _roomSSEService);

        // increment round
        room.Round += 1;
        await _roomRepository.UpdateAsync(room);
    }
}
