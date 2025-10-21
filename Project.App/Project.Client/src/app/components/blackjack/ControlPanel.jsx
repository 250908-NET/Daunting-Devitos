"use client";
import React, { useState } from "react";

export default function ControlPanel({
  gamePhase,
  bettingOpen,
  onHit,
  onStand,
  onDeal,
  onPlaceBet,
  onDouble,
  onSplit,
  canDouble,
  canSplit,
  timer,
  tableMinBet = 10, // optional prop for table minimum
}) {
  const [bet, setBet] = useState("");

  // === BETTING PHASE ===
  if (bettingOpen) {
    return (
      <div
        className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
      >
        <div className="bg-green-800 p-6 rounded-2xl shadow-2xl text-center w-96 border-2 border-yellow-500">
          <h2 className="text-yellow-300 font-bold text-2xl mb-3">
            Place your bet
          </h2>
          <p className="text-gray-200 mb-4 text-sm">
            Time remaining: <span className="text-yellow-400 font-semibold">{timer}s</span>
          </p>

          <input
            type="number"
            min={tableMinBet}
            placeholder={`Min bet: $${tableMinBet}`}
            value={bet}
            onChange={(e) => setBet(e.target.value)}
            className="w-full p-3 rounded-lg bg-white text-black text-lg text-center mb-5 focus:outline-none focus:ring-2 focus:ring-yellow-400"
          />

          <div className="flex justify-center gap-4">
            <button
              onClick={() => {
                const amt = Number(bet);
                if (amt < tableMinBet) {
                  alert(`Minimum bet is $${tableMinBet}`);
                  return;
                }
                onPlaceBet(amt);
              }}
              className="bg-yellow-400 hover:bg-yellow-500 text-black px-4 py-2 rounded-lg font-semibold transition"
            >
              Confirm Bet
            </button>
            <button
              onClick={() => onPlaceBet(0)}
              className="bg-gray-700 hover:bg-gray-800 text-white px-4 py-2 rounded-lg font-semibold transition"
            >
              Sit Out
            </button>
          </div>
        </div>
      </div>
    );
  }

  // === GAME PHASE ===
  if (gamePhase === "in-progress") {
    return (
      <div className="fixed bottom-6 left-1/2 -translate-x-1/2 flex gap-3 z-50">
        <button
          onClick={onHit}
          className="bg-yellow-500 hover:bg-yellow-600 text-black px-4 py-2 rounded-lg font-semibold"
        >
          Hit
        </button>
        <button
          onClick={onDouble}
          disabled={!canDouble}
          className={`px-4 py-2 rounded-lg font-semibold transition ${
            canDouble
              ? "bg-yellow-500 hover:bg-yellow-600 text-black"
              : "bg-gray-500 text-gray-300 cursor-not-allowed"
          }`}
        >
          Double
      </button>
        <button
          onClick={onStand}
          className="bg-yellow-500 hover:bg-yellow-600 text-black px-4 py-2 rounded-lg font-semibold"
        >
          Stand
        </button>
        <button
          onClick={onSplit}
          disabled={!canSplit}
          className={`px-4 py-2 rounded-lg font-semibold transition ${
            canSplit
              ? "bg-yellow-500 hover:bg-yellow-600 text-black"
              : "bg-gray-500 text-gray-300 cursor-not-allowed"
          }`}
        >
          Split
      </button>
      </div>
    );
  }

  return null;
}
