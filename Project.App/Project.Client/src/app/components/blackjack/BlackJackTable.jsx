"use client";
import React, { useState } from "react";
import { useBlackjackLogic } from "./hooks/useBlackjackLogic";
import DealerArea from "./DealerArea";
import TableSurface from "./TableSurface";
import PlayerSeat from "./PlayerSeat";
import ControlPanel from "./ControlPanel";
import InfoPanel from "./InfoPanel";

export default function BlackJackTable({ roomId }) {
  const [user] = useState({ id: 2, name: "Dean" });

  const {
    loading,
    room,
    dealer,
    seatPositions,
    dealerCards,
    dealInitialCards,
    hit,
    gamePhase,
    bettingOpen,
    timer,
    placeBet,
    doubleDown,
    splitHand,
    canDouble,
    canSplit,
  } = useBlackjackLogic(roomId, user);

  if (loading) return <div className="text-white p-6">Loading table...</div>;
  if (!room) return <div className="text-white p-6">Room not found</div>;

  return (
    <div className="relative text-white z-10 flex flex-col items-center w-full">
      <TableSurface>
        <DealerArea dealer={dealer} cards={dealerCards} />
        {seatPositions.map(({ player, x, y, positionLabel, hand }, i) => (
          <PlayerSeat
            key={i}
            player={player}
            user={user}
            x={x}
            y={y}
            positionLabel={positionLabel}
            hand={hand}
          />
        ))}

        <ControlPanel
          gamePhase={gamePhase}
          bettingOpen={bettingOpen}
          timer={timer}
          onPlaceBet={(amt) => placeBet(user.name, amt)}
          onHit={() => hit(user.name)}
          onStand={() => console.log("Stand pressed")}
          onDeal={() => dealInitialCards()}
          onDouble={() => doubleDown(user.name)}
          onSplit={() => split(user.name)}
          canDouble={canDouble}
          canSplit={canSplit}
        />
      </TableSurface>

      <InfoPanel room={room} />
    </div>
  );
}
