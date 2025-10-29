'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function CreateGameForm({ userId, onRoomCreated }) {
  const router = useRouter();
  const [roomDescription, setRoomDescription] = useState('');
  const [minBet, setMinBet] = useState('10');
  const [startingBalance, setStartingBalance] = useState('1000');
  const [maxPlayers, setMaxPlayers] = useState(5);
  const [formError, setFormError] = useState('');
  const [isCreating, setIsCreating] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();

    const description = roomDescription.trim();
    if (description.length > 500) {
      setFormError('Room description must be 500 characters or less.');
      return;
    }

    if (!description) {
      setFormError('Room description is required.');
      return;
    }

    const minBetValue = parseInt(minBet);
    const startingBalanceValue = parseInt(startingBalance);

    if (minBetValue < 0) {
      setFormError('Minimum bet cannot be negative.');
      return;
    }

    if (minBetValue > startingBalanceValue) {
      setFormError('Minimum bet cannot exceed starting balance.');
      return;
    }

    setFormError('');
    setIsCreating(true);

    try {
      const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';

      // Create initial game state for Blackjack
      const initialGameState = JSON.stringify({
        currentStage: {
          $type: 'init',
        },
        dealerHand: [],
      });

      // Create game config with BlackjackConfig structure
      const gameConfig = JSON.stringify({
        startingBalance: startingBalanceValue,
        minBet: minBetValue,
        bettingTimeLimit: '00:01:00', // 1 minute
        turnTimeLimit: '00:00:30', // 30 seconds
      });

      const response = await fetch(`${API_URL}/api/room`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
          hostId: userId,
          description: description,
          isPublic: true,
          gameMode: 'Blackjack',
          gameState: initialGameState,
          gameConfig: gameConfig,
          maxPlayers: maxPlayers,
          minPlayers: 1,
          deckId: '',
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        console.error('Server error response:', error);
        throw new Error(error.message || error.title || 'Failed to create room');
      }

      const room = await response.json();

      // Refresh the rooms list
      if (onRoomCreated) {
        onRoomCreated();
      }

      // Redirect to the game room immediately
      router.push(`/game/${room.id}`);
    } catch (error) {
      console.error('Error creating room:', error);
      setFormError(error.message);
      setIsCreating(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="bg-black/80 border-2 border-yellow-600 rounded-xl p-6 shadow-xl w-full max-w-md"
    >
      <h2 className="text-2xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-4">
        Create New Game
      </h2>
      {formError && (
        <div className="mb-4 p-3 bg-red-900/50 border border-red-500 rounded text-red-200 text-sm">{formError}</div>
      )}
      <div className="mb-4">
        <label className="block text-yellow-100 mb-1 font-semibold">Room Description (max 500 chars)</label>
        <textarea
          value={roomDescription}
          onChange={(e) => {
            setFormError('');
            setRoomDescription(e.target.value);
          }}
          maxLength={500}
          rows={3}
          className={`w-full px-4 py-2 rounded bg-black/60 border ${formError ? 'border-red-500' : 'border-yellow-700'} text-yellow-100 focus:outline-none focus:ring-2 ${formError ? 'focus:ring-red-500' : 'focus:ring-yellow-500'}`}
          placeholder="e.g., High stakes blackjack - experienced players only!"
          required
        />
      </div>
      <div className="mb-4">
        <label className="block text-yellow-100 mb-1 font-semibold">Starting Balance</label>
        <input
          type="number"
          min="100"
          step="100"
          value={startingBalance}
          onChange={(e) => {
            setFormError('');
            setStartingBalance(e.target.value);
          }}
          className="w-full px-4 py-2 rounded bg-black/60 border border-yellow-700 text-yellow-100 focus:outline-none focus:ring-2 focus:ring-yellow-500"
          required
        />
      </div>
      <div className="mb-4">
        <label className="block text-yellow-100 mb-1 font-semibold">Minimum Bet</label>
        <input
          type="number"
          min="1"
          step="1"
          value={minBet}
          onChange={(e) => {
            setFormError('');
            setMinBet(e.target.value);
          }}
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
        disabled={isCreating}
        className="w-full py-2 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 border-2 border-yellow-700 shadow-md disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isCreating ? 'Creating...' : 'Create Game'}
      </button>
    </form>
  );
}
