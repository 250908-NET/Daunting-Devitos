'use client';

import { useState, use } from 'react';
import AddCreditsModal from '../../components/AddCreditsModal';

export default function PlayerProfile({ params }) {
  const { id } = use(params);

  // Dummy data - will be replaced with real data from backend
  const [playerName] = useState('Danny Devito');
  const [balance, setBalance] = useState(1000);
  const [showModal, setShowModal] = useState(false);
  const [creditsToAdd, setCreditsToAdd] = useState('');

  const handleAddCredits = (e) => {
    e.preventDefault();
    const amount = parseFloat(creditsToAdd);
    if (!isNaN(amount) && amount > 0) {
      setBalance(balance + amount);
      setCreditsToAdd('');
      setShowModal(false);
      // In the future, this will make a PATCH request to update the player's balance
      // Example: fetch(`/api/player/${id}`, { method: 'PATCH', body: JSON.stringify({ balance: balance + amount }) })
      console.log(`Added ${amount} credits to player ${id}`);
    }
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setCreditsToAdd('');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 relative overflow-hidden">
      {/* Casino felt texture overlay */}
      <div
        className="absolute inset-0 opacity-30"
        style={{
          backgroundImage:
            'repeating-linear-gradient(45deg, transparent, transparent 2px, rgba(0,0,0,.1) 2px, rgba(0,0,0,.1) 4px)',
        }}
      ></div>

      <div className="max-w-4xl mx-auto relative z-10">
        <div className="bg-black/80 backdrop-blur-lg rounded-2xl p-8 shadow-2xl border-2 border-yellow-600">
          {/* Player Profile Header */}
          <div className="flex flex-col md:flex-row gap-6">
            {/* Profile Image Placeholder */}
            <div className="flex-shrink-0">
              <div className="w-40 h-40 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded-lg flex items-center justify-center shadow-lg border-2 border-yellow-700">
                <svg className="w-20 h-20 text-black/50" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" />
                </svg>
              </div>
            </div>

            {/* Player Info */}
            <div className="flex-grow">
              <h1 className="text-4xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-2">
                {playerName}
              </h1>
              <p className="text-gray-300 mb-4">
                Player ID: <span className="text-yellow-400 font-semibold">{id}</span>
              </p>

              <div className="bg-black/50 rounded-lg p-4 mb-4 border border-red-900">
                <p className="text-gray-300 text-sm mb-1">Current Balance</p>
                <p className="text-3xl font-bold text-yellow-400">{balance.toFixed(0)} Devito Bucks</p>
              </div>

              <button
                onClick={() => setShowModal(true)}
                className="px-6 py-3 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 shadow-[0_0_15px_rgba(234,179,8,0.5)] hover:shadow-[0_0_25px_rgba(234,179,8,0.8)] border-2 border-yellow-700"
              >
                Add Credits
              </button>
            </div>
          </div>
        </div>
      </div>

      <AddCreditsModal
        isOpen={showModal}
        onClose={handleCloseModal}
        balance={balance}
        creditsToAdd={creditsToAdd}
        setCreditsToAdd={setCreditsToAdd}
        onSubmit={handleAddCredits}
      />
    </div>
  );
}
