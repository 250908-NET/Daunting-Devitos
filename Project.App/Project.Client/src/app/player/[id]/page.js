import PlayerClient from './PlayerClient';

export default async function PlayerProfile({ params }) {
  const { id } = await params;

  // Auth check is handled by client-side guard in PlayerClient
  // Balance will be fetched client-side from /api/user/me
  return <PlayerClient id={id} />;
}
