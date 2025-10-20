import Link from 'next/link';

//Landing page, just allows the user to enter to log in
export default function Home() {
  return (
    <div className="flex items-center justify-center min-h-screen bg-gradient-to-br from-green-900 via-green-800 to-emerald-900 relative overflow-hidden">
      {/* Casino felt texture overlay */}
      <div
        className="absolute inset-0 opacity-30"
        style={{
          backgroundImage:
            'repeating-linear-gradient(45deg, transparent, transparent 2px, rgba(0,0,0,.1) 2px, rgba(0,0,0,.1) 4px)',
        }}
      ></div>

      {/* Decorative elements */}
      <div className="absolute top-10 left-10 w-20 h-20 border-4 border-yellow-500/20 rounded-full"></div>
      <div className="absolute bottom-10 right-10 w-32 h-32 border-4 border-yellow-500/20 rounded-full"></div>

      <main className="text-center px-4 relative z-10">
        <div className="mb-8">
          <div className="inline-block p-1 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 rounded-2xl shadow-2xl">
            <div className="bg-black px-12 py-8 rounded-xl">
              <h1 className="text-6xl md:text-8xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-2 tracking-tight">
                Double Down Devito
              </h1>
              <div className="h-1 bg-gradient-to-r from-transparent via-red-600 to-transparent"></div>
            </div>
          </div>
        </div>
        <p className="text-xl md:text-2xl text-yellow-100 mb-12 font-semibold tracking-wide">Where Luck Meets Legend</p>
        <Link
          href="/login"
          className="inline-block px-10 py-4 text-lg font-bold text-black bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-300 transform hover:scale-105 shadow-[0_0_20px_rgba(234,179,8,0.5)] hover:shadow-[0_0_30px_rgba(234,179,8,0.8)] border-2 border-yellow-600"
        >
          ENTER THE CASINO
        </Link>
      </main>
    </div>
  );
}
