'use client';

import RoomsClient from './RoomsClient';

export default function Rooms() {
  // RoomsClient now handles fetching rooms on mount
  return <RoomsClient />;
}
