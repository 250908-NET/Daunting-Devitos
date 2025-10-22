'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import AddCreditsModal from '../../components/AddCreditsModal';

export default function PlayerClient({ _id, initialBalance }) {
  const router = useRouter();
  const [playerName] = useState('Danny Devito');
  const [balance, setBalance] = useState(initialBalance ?? 1000);
  const [showModal, setShowModal] = useState(false);
  const [creditsToAdd, setCreditsToAdd] = useState('');

  // Client-side auth guard
  useEffect(() => {
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';
    fetch(`${apiBaseUrl}/auth/me`, { credentials: 'include' })
      .then((res) => {
        if (!res.ok) {
          router.replace('/login');
        } else {
          res.json().then((data) => {
            console.log('Authenticated user:', data);
          });
        }
      })
      .catch((err) => {
        console.error('Auth check failed:', err);
        router.replace('/login');
      });
  }, [router]);

  const handleAddCredits = (e) => {
    e.preventDefault();
    const amount = parseFloat(creditsToAdd);
    if (!isNaN(amount) && amount > 0) {
      setBalance((prev) => prev + amount);
      setCreditsToAdd('');
      setShowModal(false);
      // Later: PATCH to backend with new balance for player {id}
    }
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setCreditsToAdd('');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 relative overflow-hidden flex items-center justify-center">
      <div className="flex flex-col items-center justify-center w-full max-w-md mx-auto">
        {/* Player Name */}
        <h1 className="text-4xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-4 text-center">
          {playerName}
        </h1>
        {/* Player Credits */}
        <div className="bg-black/50 rounded-lg p-4 mb-6 border border-yellow-600 max-w-xs w-full text-center">
          <p className="text-gray-300 text-sm mb-1">Current Credits</p>
          <p className="text-3xl font-bold text-yellow-400">{balance} Devito Bucks</p>
        </div>
        {/* Add Credits Button */}
        <button
          onClick={() => setShowModal(true)}
          className="px-6 py-3 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 shadow-[0_0_15px_rgba(234,179,8,0.5)] hover:shadow-[0_0_25px_rgba(234,179,8,0.8)] border-2 border-yellow-700"
        >
          Add Credits
        </button>
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
