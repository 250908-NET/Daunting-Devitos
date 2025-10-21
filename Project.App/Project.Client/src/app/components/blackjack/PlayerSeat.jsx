"use client";
import React from "react";

export default function PlayerSeat({ player, user, x, y, positionLabel, hand = [] }) {
  if (!player) return null;

  const isCurrent = user?.name === player.userName;

  return (
    <div
      className="absolute text-center"
      style={{
        left: `${x}px`,
        top: `${y}px`,
        transform: "translate(-50%, -50%)",
      }}
    >
      <div
        className={`px-3 py-2 rounded-lg border text-sm font-semibold ${
          isCurrent
            ? "bg-yellow-500 text-black border-yellow-300"
            : "bg-gray-900 text-white border-gray-600"
        }`}
      >
        {player.userName}
      </div>

      <div className="text-xs text-gray-300">Balance: ${player.balance}</div>
      <div className="text-[10px] text-gray-400">Seat: {positionLabel}</div>

      {/* Show cards */}
      {hand.length > 0 && (
        <div className="flex justify-center gap-1 mt-2">
          {hand.map((card) => (
            <img
              key={card.code}
              src={card.image}
              alt={card.code}
              className="w-12 h-auto rounded shadow-md"
            />
          ))}
        </div>
      )}
    </div>
  );
}
