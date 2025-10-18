'use client';
import { useState } from 'react';

export default function CreateGameForm() {
  const [roomName, setRoomName] = useState('');
  const [minBet, setMinBet] = useState('');
  const [maxPlayers, setMaxPlayers] = useState(5);
  const [roomNameError, setRoomNameError] = useState('');
  const [roomDescription, setRoomDescription] = useState('');

  const handleSubmit = (e) => {
    e.preventDefault();
    // Validate roomName length
    const name = roomName.trim();
    if (name.length > 20) {
      setRoomNameError('Room name must be 20 characters or less.');
        const description = roomDescription.trim();
        if (description.length > 100) {
          setRoomNameError('Room description must be 100 characters or less.');
          return;
        }
    
    }
    setRoomNameError('');

    // Dummy submit
    alert(`Game created! Room: ${name} Description: ${roomDescription} (min bet: $${minBet}, max players: ${maxPlayers})`);
    setRoomName('');
    setMinBet('');
    setMaxPlayers(5);
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="bg-black/80 border-2 border-yellow-600 rounded-xl p-6 shadow-xl w-full max-w-md"
    >
      <h2 className="text-2xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-4">
        Create New Game
      </h2>
      <div className="mb-4">
        <label className="block text-yellow-100 mb-1 font-semibold">Room Name (max 20 chars)</label>
        <input
          type="text"
          value={roomName}
          onChange={(e) => {
            const val = e.target.value;
            if (val.trim().length <= 20) {
              setRoomNameError('');
            }
            setRoomName(val);
          }}
          maxLength={20}
          className={`w-full px-4 py-2 rounded bg-black/60 border ${roomNameError ? 'border-red-500' : 'border-yellow-700'} text-yellow-100 focus:outline-none focus:ring-2 ${roomNameError ? 'focus:ring-red-500' : 'focus:ring-yellow-500'}`}
          aria-invalid={roomNameError ? 'true' : 'false'}
          required
        />
        {roomNameError && <p className="mt-1 text-sm text-red-400">{roomNameError}</p>}
      </div>
      <div className="mb-4">
        <label className="block text-yellow-100 mb-1 font-semibold">Room Description (max 100 chars)</label>
        <input
          type="text"
          value={roomDescription}
          onChange={(e) => {
            const val = e.target.value;
            if (val.trim().length <= 100) {
              setRoomNameError('');
            }
            setRoomDescription(val);
          }}
          maxLength={100}
          className={`w-full px-4 py-2 rounded bg-black/60 border ${roomNameError ? 'border-red-500' : 'border-yellow-700'} text-yellow-100 focus:outline-none focus:ring-2 ${roomNameError ? 'focus:ring-red-500' : 'focus:ring-yellow-500'}`}
          aria-invalid={roomNameError ? 'true' : 'false'}
          required
        />
        {roomNameError && <p className="mt-1 text-sm text-red-400">{roomNameError}</p>}
      </div>
      <div className="mb-4">
        <label className="block text-yellow-100 mb-1 font-semibold">Minimum Bet </label>
        <input
          type="number"
          min="10"
          step="10"
          value={minBet}
          onChange={(e) => setMinBet(e.target.value)}
          className="w-full px-4 py-2 rounded bg-black/60 border border-yellow-700 text-yellow-100 focus:outline-none focus:ring-2 focus:ring-yellow-500"
          required
        />
      </div>
      <div className="mb-6">
        <label className="block text-yellow-100 mb-1 font-semibold">Max Players</label>
        <select
          value={maxPlayers}
          onChange={(e) => setMaxPlayers(Number(e.target.value))}
          className="w-full px-4 py-2 rounded bg-black/60 border border-yellow-700 text-yellow-100 focus:outline-none focus:ring-2 focus:ring-yellow-500"
        >
          {[2, 3, 4, 5].map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </div>
      <button
        type="submit"
        className="w-full py-2 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 border-2 border-yellow-700 shadow-md"
      >
        Create Game
      </button>
    </form>
  );
}
