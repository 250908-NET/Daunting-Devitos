"use client";
import React, { useState } from "react";

export default function BetModal({ show, onBet, onSitOut, timer, tableMinBet = 10 }) {
  const [amount, setAmount] = useState("");

  if (!show) return null;

  const handleBet = () => {
    const numericAmount = Number(amount);
    if (numericAmount < tableMinBet) {
      alert(`Minimum bet is $${tableMinBet}`);
      return;
    }
    onBet(numericAmount);
  };

  return (
    <div
      className="fixed inset-0 bg-black/70 z-50"
      style={{
        backdropFilter: "blur(5px)",
      }}
    >
      <div
        className="absolute bg-green-800 p-8 rounded-2xl shadow-2xl text-center w-96 border-2 border-yellow-500"
        style={{
          top: "50%",
          left: "50%",
          transform: "translate(-50%, -50%)",
        }}
      >
        <h2 className="text-yellow-300 text-2xl font-bold mb-4">💰 Place Your Bet</h2>
        <p className="text-sm text-gray-200 mb-6">
          Time remaining:{" "}
          <span className="text-yellow-300 font-semibold">{timer}s</span>
        </p>

        <input
          type="number"
          min={tableMinBet}
          placeholder={`Minimum bet: $${tableMinBet}`}
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className="w-full p-3 rounded-lg bg-white text-black mb-6 border border-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-400 text-lg text-center"
        />

        <div className="flex justify-between gap-4">
          <button
            onClick={handleBet}
            className="flex-1 bg-yellow-400 hover:bg-yellow-500 text-black font-semibold py-2 rounded-lg transition-colors"
          >
            Bet
          </button>
          <button
            onClick={onSitOut}
            className="flex-1 bg-gray-700 hover:bg-gray-800 text-white font-semibold py-2 rounded-lg transition-colors"
          >
            Sit Out
          </button>
        </div>
      </div>
    </div>
  );
}
