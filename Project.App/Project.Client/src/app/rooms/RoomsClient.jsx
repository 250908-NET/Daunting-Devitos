'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import CreateGameForm from '../components/CreateGameForm';

export default function RoomsClient({ rooms }) {
  const router = useRouter();

  useEffect(() => {
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';
    console.log('[RoomsClient] Checking authentication...');

    fetch(`${apiBaseUrl}/auth/me`, { credentials: 'include' })
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
        }
      })
      .catch((err) => {
        console.error('[RoomsClient] Auth check failed:', err);
        router.replace('/login');
      });
  }, [router]);
  // Any state or handlers you need
  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 flex flex-col md:flex-row gap-8 relative overflow-hidden">
      {/* ...existing overlay and structure... */}
      <div className="flex-1 max-w-lg relative z-10">
        <h2 className="text-2xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-6">
          Available Game Rooms
        </h2>
        <ul className="space-y-4">
          {rooms.map((room) => (
            <li
              key={room.id}
              className="bg-black/80 border-2 border-yellow-600 rounded-xl p-5 flex items-center justify-between shadow-lg"
            >
              <div>
                <div className="text-yellow-200 font-bold text-lg mb-1">{room.roomName}</div>
                <div className="text-yellow-100 font-semibold text-base">Game #{room.id}</div>
                <div className="text-yellow-300 text-sm">
                  Players: <span className="font-bold">{room.players}</span> / 5
                </div>
                <div className="text-yellow-300 text-sm">
                  Min Bet: <span className="font-bold">${room.minBet}</span>
                </div>
              </div>
              <button
                onClick={() => {
                  console.log('lets gamble baybee');
                  alert("Let's gamble, baybee!");
                }}
                className="ml-4 px-4 py-2 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 border-2 border-yellow-700 shadow-md"
              >
                Join
              </button>
            </li>
          ))}
        </ul>
      </div>
      <div className="flex-1 flex flex-col items-start md:items-end relative z-10 mt-20">
        <CreateGameForm />
      </div>
    </div>
  );
}
