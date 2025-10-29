'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import CreateGameForm from '../components/CreateGameForm';

export default function RoomsClient() {
  const router = useRouter();
  const [userId, setUserId] = useState(null);
  const [rooms, setRooms] = useState([]);
  const [joiningRoomId, setJoiningRoomId] = useState(null);
  const [isLoadingRooms, setIsLoadingRooms] = useState(true);
  const [userRooms, setUserRooms] = useState([]); // Rooms the user is already in

  // Fetch rooms the user is already in
  const fetchUserRooms = async () => {
    if (!userId) return;

    try {
      const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';
      // Check if user has an active room they're hosting or participating in
      // Only check ACTIVE rooms to avoid showing user as "in game" after leaving
      const response = await fetch(`${API_URL}/api/room/active`, {
        credentials: 'include',
        cache: 'no-store',
      });

      if (response.ok) {
        const activeRooms = await response.json();
        // Find active rooms where user is either host or a player
        const myRooms = [];

        for (const room of activeRooms) {
          if (room.hostId === userId) {
            myRooms.push(room.id);
            continue;
          }

          // Check if user is a player in this room
          try {
            const playersRes = await fetch(`${API_URL}/api/room/${room.id}/players`, {
              credentials: 'include',
              cache: 'no-store', // Ensure fresh data
            });
            if (playersRes.ok) {
              const players = await playersRes.json();
              if (players.some((p) => p.userId === userId)) {
                console.log(`[fetchUserRooms] User ${userId} is in room ${room.id}`);
                myRooms.push(room.id);
              }
            }
          } catch (e) {
            console.error('Error checking room players:', e);
          }
        }

        console.log(`[fetchUserRooms] User rooms found:`, myRooms);
        setUserRooms(myRooms);
      }
    } catch (error) {
      console.error('Error fetching user rooms:', error);
    }
  };

  // Fetch rooms
  const handleRefreshRooms = async () => {
    try {
      const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';
      const response = await fetch(`${API_URL}/api/room/public`, {
        credentials: 'include',
        cache: 'no-store',
      });

      if (response.ok) {
        const updatedRooms = await response.json();
        // Filter to only show active rooms (extra safety check)
        const activeRooms = updatedRooms.filter((room) => room.isActive);
        setRooms(activeRooms);
        setIsLoadingRooms(false);
      }

      // Also fetch user's rooms
      await fetchUserRooms();
    } catch (error) {
      console.error('Error refreshing rooms:', error);
      setIsLoadingRooms(false);
    }
  };

  useEffect(() => {
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';
    console.log('[RoomsClient] Checking authentication...');

    fetch(`${apiBaseUrl}/api/user/me`, { credentials: 'include' })
      .then((res) => {
        console.log('[RoomsClient] Auth response status:', res.status);
        if (!res.ok) {
          console.log('[RoomsClient] Not authenticated, redirecting to /login');
          router.replace('/login');
        } else {
          return res.json();
        }
      })
      .then((data) => {
        if (data) {
          console.log('[RoomsClient] ✅ Authenticated as:', data.name);
          console.log('[RoomsClient] Full user data:', data);
          setUserId(data.id);
          // Fetch rooms after authentication
          handleRefreshRooms();
        }
      })
      .catch((err) => {
        console.error('[RoomsClient] Auth check failed:', err);
        router.replace('/login');
      });
  }, [router]);

  // Auto-refresh rooms every 5 seconds
  useEffect(() => {
    const interval = setInterval(() => {
      handleRefreshRooms();
    }, 5000);

    return () => clearInterval(interval);
  }, []);

  const handleJoinRoom = async (roomId) => {
    if (!userId) {
      alert('Please log in first');
      return;
    }

    setJoiningRoomId(roomId);

    try {
      const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';

      const response = await fetch(`${API_URL}/api/room/${roomId}/join`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
          userId: userId,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        console.log('[JoinRoom] Error response:', error);
        console.log('[JoinRoom] Status code:', response.status);

        // If already in room (409 Conflict), just redirect to the room
        if (response.status === 409) {
          // Check both 'detail' and 'Detail' (case-insensitive)
          const errorMsg = (error.detail || error.Detail || error.message || error.title || '').toLowerCase();
          console.log('[JoinRoom] Conflict error message:', errorMsg);

          // Check if error is about already being in the room
          if (errorMsg.includes('already') || errorMsg.includes('player')) {
            console.log('[JoinRoom] Already in room, redirecting...');
            router.push(`/game/${roomId}`);
            return;
          }
        }

        throw new Error(error.detail || error.Detail || error.message || error.title || 'Failed to join room');
      }

      const updatedRoom = await response.json();
      console.log('Successfully joined room:', updatedRoom);

      // Redirect to the game room
      router.push(`/game/${roomId}`);
    } catch (error) {
      console.error('Error joining room:', error);
      alert(`Failed to join room: ${error.message}`);
      setJoiningRoomId(null);
    }
  };

  // Parse game config to get min bet
  const getMinBet = (room) => {
    try {
      const config = JSON.parse(room.gameConfig);
      return config.minBet || 0;
    } catch {
      return 0;
    }
  };
  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 flex flex-col md:flex-row gap-8 relative overflow-hidden">
      <div className="flex-1 max-w-lg relative z-10">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-2xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent">
            Available Game Rooms
          </h2>
          <button
            onClick={handleRefreshRooms}
            className="px-3 py-1 text-sm bg-yellow-600/20 border border-yellow-600 text-yellow-200 rounded-lg hover:bg-yellow-600/30 transition-colors"
          >
            Refresh
          </button>
        </div>
        {isLoadingRooms ? (
          <div className="bg-black/80 border-2 border-yellow-600 rounded-xl p-8 text-center">
            <p className="text-yellow-100 text-lg">Loading rooms...</p>
          </div>
        ) : rooms.filter((room) => !userRooms.includes(room.id)).length === 0 ? (
          <div className="bg-black/80 border-2 border-yellow-600 rounded-xl p-8 text-center">
            <p className="text-yellow-100 text-lg mb-2">No rooms available</p>
            <p className="text-yellow-300 text-sm mb-4">
              {userRooms.length > 0 ? "You're already in a game!" : 'Create a new game to get started!'}
            </p>
            {userRooms.length > 0 && (
              <button
                onClick={() => router.push(`/game/${userRooms[0]}`)}
                className="px-4 py-2 bg-gradient-to-r from-blue-400 via-blue-500 to-blue-600 text-white font-bold rounded-lg hover:from-blue-500 hover:to-blue-700 transition-all duration-200 border-2 border-blue-700 shadow-md"
              >
                Go to Your Game
              </button>
            )}
          </div>
        ) : (
          <ul className="space-y-4">
            {rooms
              .filter((room) => !userRooms.includes(room.id))
              .map((room) => (
                <li key={room.id} className="bg-black/80 border-2 border-yellow-600 rounded-xl p-5 shadow-lg">
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex-1">
                      <div className="text-yellow-200 font-bold text-lg mb-1">
                        {room.description || 'Blackjack Game'}
                      </div>
                      <div className="text-yellow-100/60 font-mono text-xs mb-2">ID: {room.id.substring(0, 8)}...</div>
                    </div>
                    <div
                      className={`px-2 py-1 rounded text-xs font-semibold ${
                        room.isActive
                          ? 'bg-green-600/20 text-green-300 border border-green-600'
                          : 'bg-gray-600/20 text-gray-300 border border-gray-600'
                      }`}
                    >
                      {room.isActive ? 'Active' : 'Waiting'}
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-2 mb-4">
                    <div className="text-yellow-300 text-sm">
                      Mode: <span className="font-bold text-yellow-200">{room.gameMode}</span>
                    </div>
                    <div className="text-yellow-300 text-sm">
                      Min Bet: <span className="font-bold text-yellow-200">${getMinBet(room)}</span>
                    </div>
                    <div className="text-yellow-300 text-sm">
                      Players:{' '}
                      <span className="font-bold text-yellow-200">
                        {room.minPlayers}-{room.maxPlayers}
                      </span>
                    </div>
                    <div className="text-yellow-300 text-sm">
                      Public: <span className="font-bold text-yellow-200">{room.isPublic ? 'Yes' : 'No'}</span>
                    </div>
                  </div>
                  <button
                    onClick={() => handleJoinRoom(room.id)}
                    disabled={joiningRoomId === room.id}
                    className="w-full py-2 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 border-2 border-yellow-700 shadow-md disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {joiningRoomId === room.id ? 'Joining...' : 'Join Room'}
                  </button>
                </li>
              ))}
          </ul>
        )}
      </div>
      <div className="flex-1 flex flex-col items-start md:items-end relative z-10">
        {userId && <CreateGameForm userId={userId} onRoomCreated={handleRefreshRooms} />}
      </div>
    </div>
  );
}
