"use client";
import React from "react";

export default function InfoPanel({ room }) {
  return (
    <div className="absolute top-4 right-6 text-sm text-gray-300 bg-black/40 px-4 py-2 rounded-lg border border-yellow-700">
      <div>🪙 Table State: {room.state}</div>
      <div>🔁 Round: {room.round}</div>
      <div>💰 Min Bet: ${room.minBet ?? 10}</div>
    </div>
  );
}
