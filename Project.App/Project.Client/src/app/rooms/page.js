'use client';
import CreateGameForm from '../components/CreateGameForm';

const dummyRooms = [
  { id: 1, roomName: 'High Rollers', players: 2, minBet: 10 },
  { id: 2, roomName: 'Lucky Sevens', players: 5, minBet: 25 },
  { id: 3, roomName: 'Beginner’s Luck', players: 1, minBet: 5 },
  { id: 4, roomName: 'Golden Table', players: 4, minBet: 50 },
];

export default function Rooms() {
  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 flex flex-col md:flex-row gap-8 relative overflow-hidden">
      {/* Casino felt texture overlay */}
      <div
        className="absolute inset-0 opacity-30 pointer-events-none"
        style={{
          backgroundImage:
            'repeating-linear-gradient(45deg, transparent, transparent 2px, rgba(0,0,0,.1) 2px, rgba(0,0,0,.1) 4px)',
        }}
      ></div>

      {/* Room List */}
      <div className="flex-1 max-w-lg relative z-10">
        <h2 className="text-2xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-6">
          Available Game Rooms
        </h2>
        <ul className="space-y-4">
          {dummyRooms.map((room) => (
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

      {/* Create Game Form */}
      <div className="flex-1 flex flex-col items-start md:items-end relative z-10 mt-20">
        <CreateGameForm />
      </div>
    </div>
  );
}
