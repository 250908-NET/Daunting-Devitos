import PlayerClient from './PlayerClient';

export default function PlayerProfile({ params }) {
  //const { id } = params;

  // Auth check is handled by client-side guard in PlayerClient
  //id={id} initialBalance={1000} 
  return <PlayerClient/>;
}
