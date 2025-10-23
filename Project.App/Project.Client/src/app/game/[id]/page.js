import { use } from 'react';
import GameClient from './GameClient';

export default function GamePage({ params }) {
  const { id } = use(params);

  return <GameClient roomId={id} />;
}
