'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import AddCreditsModal from '../../components/AddCreditsModal';

export default function PlayerClient({ _id }) {
  const router = useRouter();
  const [playerName, setPlayerName] = useState('Danny Devito');
  const [playerId, setPlayerId] = useState('');
  const [avatarUrl, setAvatarUrl] = useState('https://www.shutterstock.com/editorial/image-editorial/NeTeY724MeD5Q1weMTgxMw==/danny-devito-440nw-5624612ab.jpg');
  const [balance, setBalance] = useState(2089234500);
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
          res.json().then(() => {
            // authenticated successfully
          });
        }
      })
      .catch(() => {
        router.replace('/login');
      });

    fetch(`${apiBaseUrl}/api/user/me`, { credentials: 'include' })
      .then((res) => {
        if (!res.ok) {
          router.replace('/rooms');
        } else {
          res.json().then((data) => {
            setPlayerId(data.id);
            setPlayerName(data.name);
            setBalance(data.balance);
            setAvatarUrl(data.avatarUrl);
          });
        }
      });
  }, [router]);
  
  console.log('After after setting user data:', avatarUrl);

  const handleAddCredits = async (e) => {
    e.preventDefault();
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';

    const amount = parseFloat(creditsToAdd);
    if (Number.isNaN(amount) || amount <= 0) return;

    const newBalance = balance + amount;

    try {
      const res = await fetch(`${apiBaseUrl}/api/user/${playerId}`, {
        method: 'PATCH',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ balance: newBalance }),
      });

      if (!res.ok) {
        if (res.status === 401) {
          router.replace('/login');
        }
        return;
      }

      setBalance(newBalance);
      setCreditsToAdd('');
      setShowModal(false);
    } catch {
      // intentionally left blank to satisfy lint rules
    }
  };

  const handleCloseModal = () => {
    setShowModal(false);
    setCreditsToAdd('');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 relative overflow-hidden flex items-center justify-center">
      <div className="flex flex-col items-center justify-center w-full max-w-md mx-auto">
        {/*
          //src={null}
          //alt="Gamer Avatar"
          //className="w-32 h-32 mb-4 rounded-full overflow-hidden border-4 border-yellow-600 shadow-lg"
        */}
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
        <div className="bg-black/50 rounded-lg p-4 mb-6 border border-yellow-600 max-w-xs w-full text-center">
          <p className="text-3xl font-bold text-yellow-400">{balance}</p>
          <p className="text-3xl font-bold text-yellow-400">Devito Bucks</p>
        </div>
        <button
          onClick={() => setShowModal(true)}
          className="px-6 py-3 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 shadow-[0_0_15px_rgba(234,179,8,0.5)] hover:shadow-[0_0_25px_rgba(234,179,8,0.8)] border-2 border-yellow-700"
          type="button"
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
