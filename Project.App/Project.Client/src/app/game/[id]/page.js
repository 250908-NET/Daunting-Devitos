import { use } from 'react';

export default function GamePage({ params }) {
  const { id } = use(params);

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 p-8 relative overflow-hidden">
      {/* Casino felt texture overlay */}
      <div
        className="absolute inset-0 opacity-30"
        style={{
          backgroundImage:
            'repeating-linear-gradient(45deg, transparent, transparent 2px, rgba(0,0,0,.1) 2px, rgba(0,0,0,.1) 4px)',
        }}
      />
      <h1 className="text-4xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-6 relative z-10">
        Game Table ID: {id}
      </h1>
      {/* Game  implementation will go here */}
    </div>
  );
}
