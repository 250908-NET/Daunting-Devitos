'use client';

export default function AddCreditsModal({ isOpen, onClose, balance, creditsToAdd, setCreditsToAdd, onSubmit }) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-black/90 backdrop-blur-lg rounded-2xl p-8 max-w-md w-full shadow-2xl border-2 border-yellow-600">
        <h2 className="text-2xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-4">
          Add Credits
        </h2>
        <div className="h-1 bg-gradient-to-r from-transparent via-red-600 to-transparent mb-6"></div>

        <div className="bg-black/50 rounded-lg p-4 mb-6 border border-red-900">
          <p className="text-gray-300 text-sm mb-1">Current Balance</p>
          <p className="text-2xl font-bold text-yellow-400">${balance.toFixed(2)}</p>
        </div>

        <form onSubmit={onSubmit}>
          <label className="block mb-2">
            <span className="text-yellow-100 text-sm font-medium">Amount to Add</span>
            <input
              type="number"
              step="10"
              min="0"
              max="200000"
              value={creditsToAdd}
              onChange={(e) => setCreditsToAdd(e.target.value)}
              placeholder="0"
              className="w-full mt-1 px-4 py-3 bg-black/50 border-2 border-yellow-600 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-yellow-500 focus:border-yellow-500"
              required
            />
          </label>

          <div className="flex gap-3 mt-6">
            <button
              type="submit"
              className="flex-1 px-6 py-3 bg-gradient-to-r from-yellow-400 via-yellow-500 to-yellow-600 text-black font-bold rounded-lg hover:from-yellow-500 hover:to-yellow-700 transition-all duration-200 shadow-[0_0_15px_rgba(234,179,8,0.5)] hover:shadow-[0_0_25px_rgba(234,179,8,0.8)] border-2 border-yellow-700"
            >
              Add Credits
            </button>
            <button
              type="button"
              onClick={onClose}
              className="px-6 py-3 bg-red-900/50 text-white font-semibold rounded-lg hover:bg-red-900/70 transition-all duration-200 border-2 border-red-800"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
