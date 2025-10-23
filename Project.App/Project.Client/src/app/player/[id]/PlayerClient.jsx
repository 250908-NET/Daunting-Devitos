'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import AddCreditsModal from '../../components/AddCreditsModal';

export default function PlayerClient({ _id, initialBalance }) {
  const router = useRouter();
  const [playerName, setPlayerName] = useState('Danny Devito');
  const [playerId, setPlayerId] = useState('');
  const [avatarUrl, setAvatarUrl] = useState('https://www.shutterstock.com/editorial/image-editorial/NeTeY724MeD5Q1weMTgxMw==/danny-devito-440nw-5624612ab.jpg');
  const [balance, setBalance] = useState(2089234500);
  const [showModal, setShowModal] = useState(false);
  const [creditsToAdd, setCreditsToAdd] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingUser, setIsLoadingUser] = useState(true); // Track initial load

  const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';

  // Client-side auth guard and fetch user data
  useEffect(() => {
    setIsLoadingUser(true);
    fetch(`${API_URL}/api/user/me`, { credentials: 'include' })
      .then((res) => {
        if (!res.ok) {
          router.replace('/login');
        } else {
          return res.json();
        }
      })
      .then((data) => {
        if (data) {
          console.log('Authenticated user:', data);
          setPlayerName(data.name);
          setPlayerId(data.id);
          setBalance(data.balance);
          setAvatarUrl(data.avatarUrl);
        }
      })
      .catch((err) => {
        console.error('Auth check failed:', err);
        router.replace('/login');
      })
      .finally(() => {
        setIsLoadingUser(false);
      });
  }, [router, API_URL]);

  const handleAddCredits = async (e) => {
    e.preventDefault();
    const amount = parseFloat(creditsToAdd);

    if (!playerId) {
      alert('Player ID not loaded. Please refresh the page.');
      return;
    }

    if (isNaN(amount) || amount <= 0) {
      alert('Please enter a valid amount greater than 0');
      return;
    }
    
    const newBalance = balance + amount;

    setIsLoading(true);

    try {
      console.log('New total shold be:', newBalance);
      const response = await fetch(`${API_URL}/api/user/${playerId}`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ balance: newBalance }),
      });
      console.log('Add credits response status:', response.status);

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to add credits');
      }

      //const data = await response.json();
      //console.log('Credits added successfully. New balance:', data.balance);

      // Update local state with new balance from server
      setBalance(newBalance);
      setCreditsToAdd('');
      setShowModal(false);
    } catch (error) {
      console.error('Error adding credits:', error);
      alert(`Failed to add credits: ${error.message}`);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setCreditsToAdd('');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 relative overflow-hidden flex items-center justify-center">
      <div className="flex flex-col items-center justify-center w-full max-w-md mx-auto">
        <Image 
          src={avatarUrl}
          alt="Description of my image" 
          width={128} 
          height={128}
          className="w-32 h-32 mb-4 rounded-full overflow-hidden border-4 border-yellow-600 shadow-lg"
        />
        <h1 className="text-4xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-4 text-center">
          {playerName}
        </h1>
        {/* Player Credits */}
        <div className="bg-black/50 rounded-lg p-4 mb-6 border border-yellow-600 max-w-xs w-full text-center">
          {isLoadingUser ? (
            <p className="text-3xl font-bold text-yellow-400">Loading...</p>
          ) : (
            <p className="text-3xl font-bold text-yellow-400">{balance ?? 0}</p>
          )}
            <p className="text-3xl font-bold text-yellow-400">Devito Bucks</p>
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
        balance={balance ?? 0}
        creditsToAdd={creditsToAdd}
        setCreditsToAdd={setCreditsToAdd}
        onSubmit={handleAddCredits}
        isLoading={isLoading}
      />
    </div>
  );
}
