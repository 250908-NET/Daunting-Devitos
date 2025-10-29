import {
  ChatEventData,
  GameStateUpdateEventData,
  PlayerActionEventData,
  PlayerJoinEventData,
  PlayerLeaveEventData,
  HostChangeEventData,
  DealerRevealEventData,
  PlayerRevealEventData,
} from './GameEvents.types';

/**
 * A map representing the set of functions necessary to update the current display state.
 * Will be called by the event handler to update the UI.
 */
export interface GameStateSetters {
  setMessages: React.Dispatch<React.SetStateAction<ChatEventData[]>>;
  setRoomPlayers: React.Dispatch<React.SetStateAction<any[]>>;
  setGameState: React.Dispatch<React.SetStateAction<any | null>>;
  setRoom: React.Dispatch<React.SetStateAction<any | null>>;
  fetchRoomPlayers: () => Promise<void>;
  user: any | null;
}

const eventHandlers = {
  chat: (event: ChatEventData, { setMessages }: GameStateSetters) => {
    console.log(`[SSE Event] Chat message: ${event.sender} said: ${event.content}`);

    // add message to display
    setMessages((prev) => [...prev, event]);
  },
  game_state_update: (event: GameStateUpdateEventData, { setGameState }: GameStateSetters) => {
    console.log(`[SSE Event] Game state updated: ${event.currentStage}`);

    // update game state
    // TODO: use strong typing for state parsing
    const parsedState = JSON.parse(event.currentStage);
    setGameState(parsedState);
  },
  player_action: (event: PlayerActionEventData, { fetchRoomPlayers }: GameStateSetters) => {
    // TODO: use better type system for action data

    switch (event.action) {
      case 'bet': {
        console.log(`[SSE Event] Player bet: ${event.amount}`);
        // sync player list with new balance
        fetchRoomPlayers();
        break;
      }
      case 'hit':
      case 'stand':
      case 'double':
      case 'split':
      case 'surrender': {
        console.log(`[SSE Event] Player ${event.action} (${event.playerId}, ${event.handIndex})`);
        // TODO: add player action to messages?

        // sync player list, since hand/balance/bet may have changed
        fetchRoomPlayers();
        break;
      }
      case 'hurry_up': {
        console.log(`[SSE Event] Player ${event.action} (${event.playerId}, ${event.targetPlayerId})`);
        // TODO: add player action to messages?
        break;
      }
      default: {
        console.warn(`[SSE Event] Unknown action: ${event.action}`);
        break;
      }
    }
    fetchRoomPlayers();
  },
  player_join: (event: PlayerJoinEventData, { fetchRoomPlayers }: GameStateSetters) => {
    console.log(`[SSE Event] Player joined: ${event.playerName} (${event.playerId})`);

    // sync player list
    fetchRoomPlayers();
  },
  player_leave: (event: PlayerLeaveEventData, { fetchRoomPlayers }: GameStateSetters) => {
    console.log(`[SSE Event] Player left: ${event.playerName} (${event.playerId})`);

    // sync player list
    fetchRoomPlayers();
  },
  host_change: (event: HostChangeEventData, { user }: GameStateSetters) => {
    console.log(`[SSE Event] Host changed: ${event.playerName} (${event.playerId})`);

    if (event.playerId === user?.id) {
      alert('The previous host left. You are now the host!');
    } else {
      alert(`The host has left. ${event.playerName} is the new host.`);
    }
  },
  player_reveal: (event: PlayerRevealEventData) => {
    console.log(`[SSE Event] Player cards revealed: ${event.playerHand} (${event.playerScore}) (${event.playerId})`);

    // TODO: update cards on screen
  },
  dealer_reveal: (event: DealerRevealEventData) => {
    console.log(`[SSE Event] Dealer cards revealed: ${event.dealerHand} (${event.dealerScore})`);

    // TODO: update cards on screen
    // NOTE: double check that balances are synced elsewhere
  },
};

export function createSSEListener(setters: GameStateSetters) {
  return (event: MessageEvent) => {
    try {
      const eventType = event.type as keyof typeof eventHandlers;
      const handler = eventHandlers[eventType];

      if (handler) {
        // The 'data' property on a MessageEvent is the string payload from the server.
        const data = JSON.parse(event.data);
        console.log(`[SSE] Handling '${eventType}':`, data);
        handler(data, setters);
      } else {
        console.warn(`[SSE] No handler for event type: ${event.type}`);
      }
    } catch (error) {
      console.error(`[SSE] Error processing event:`, event, error);
    }
  };
}
