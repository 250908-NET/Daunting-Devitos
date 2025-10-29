// for reference:
// Project.App/Project.Api/DTOs/CardDTO.cs
// Project.App/Project.Api/Models/Games/RoomEvents.cs
// Project.App/Project.Api/Models/Games/BlackjackState.cs

export interface Card {
  code: string;
  image: string;
  value: string;
  suit: string;
  isFaceDown: boolean;
}

export interface ChatEventData {
  sender: string; // user's display name
  content: string;
  timestamp: string;
}

// TODO: strong typing for state
export interface GameStateUpdateEventData {
  currentStage: string;
}

export interface PlayerActionEventData {
  playerId: string; // user guid
  handIndex: number;
  action: string;
  amount: number | null | undefined; // optional
  cards: Card[] | null | undefined; // optional
  targetPlayerId: string | null | undefined; // optional target user guid
  success: boolean | null | undefined; // optional
}

export interface PlayerJoinEventData {
  playerId: string; // user guid
  playerName: string;
}

export interface PlayerLeaveEventData {
  playerId: string; // user guid
  playerName: string;
}

export interface HostChangeEventData {
  playerId: string; // user guid
  playerName: string;
}

export interface DealerRevealEventData {
  dealerHand: Card[];
  dealerScore: number;
}

export interface PlayerRevealEventData {
  playerId: string; // user guid
  handIndex: number;
  playerHand: Card[];
  playerScore: number;
}
