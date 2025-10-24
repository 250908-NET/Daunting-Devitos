import PlayerClient from './PlayerClient';

export default function PlayerProfile() {
  // Auth check is handled by client-side guard in PlayerClient
  return <PlayerClient />;
}