import RoomsClient from './RoomsClient';

//Will fetch the real rooms data here
const dummyRooms = [
  { id: 1, roomName: 'High Rollers', players: 2, minBet: 10 },
  { id: 2, roomName: 'Lucky Sevens', players: 5, minBet: 25 },
  { id: 3, roomName: "Beginner's Luck", players: 1, minBet: 5 },
  { id: 4, roomName: 'Golden Table', players: 4, minBet: 50 },
];

export default function Rooms() {
  return <RoomsClient rooms={dummyRooms} />;
}
