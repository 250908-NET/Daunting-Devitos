// src/app/lib/mockRoomService.js
// Simple mock of backend room logic while the API isn't ready.

const rooms = {
  1: {
    id: 1,
    description: "Demo Blackjack Room",
    isActive: true,
    gameMode: "blackjack",
    maxPlayers: 5,
    minPlayers: 1,
    deckId: "default",
    round: 0,
    state: "playing",
    roomPlayers: [
      { id: 0, userId: 0, userName: "Duigi", role: "dealer", status: "ready", balance: 0 },
      { id: 1, userId: 1, userName: "Shane", role: "player", status: "ready", balance: 500 },
      { id: 2, userId: 2, userName: "Dean", role: "player", status: "ready", balance: 600 },
      { id: 3, userId: 3, userName: "Ariel", role: "player", status: "ready", balance: 450 },
      { id: 4, userId: 4, userName: "Stephen", role: "player", status: "ready", balance: 700 },
      { id: 5, userId: 5, userName: "Mathew", role: "player", status: "ready", balance: 550 },
    ],
  },
};


export async function getRoom(id) {
  await new Promise((r) => setTimeout(r, 150)); // simulate small delay
  return rooms[id] ?? null;
}

export async function joinRoom(roomId, user) {
  const room = rooms[roomId];
  if (!room) throw new Error("Room not found");

  // Skip if user already joined
  if (room.roomPlayers.find((p) => p.userId === user.id)) return room;

  const newPlayer = {
    id: "p" + Math.random().toString(36).slice(2, 8),
    roomId,
    userId: user.id,
    userName: user.name,
    role: "player",
    status: "ready",
    balance: user.balance ?? 0,
  };
  room.roomPlayers.push(newPlayer);
  return room;
}

export async function leaveRoom(roomId, userId) {
  const room = rooms[roomId];
  if (!room) throw new Error("Room not found");
  room.roomPlayers = room.roomPlayers.filter((p) => p.userId !== userId);
  return room;
}
