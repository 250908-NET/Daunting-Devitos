'use client';

export default function Login() {
  // Click handler for "Sign in with Google"
  const handleGoogleSignIn = () => {
    console.log('Google sign-in clicked');

    // API base HTTPS in dev. Set NEXT_PUBLIC_API_URL in .env.local, else fallback.
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';

    // Where to land in the SPA after successful login
    const returnUrl = `${window.location.origin}/rooms`; // e.g., http://localhost:3000/rooms

    console.log('API Base URL:', apiBaseUrl);
    console.log('Return URL:', returnUrl);
    console.log('Full login URL:', `${apiBaseUrl}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`);

    // Send the browser to your backend auth endpoint with a safe returnUrl
    window.location.href = `${apiBaseUrl}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  };

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

      <main className="bg-black/80 backdrop-blur-lg rounded-2xl p-8 md:p-12 shadow-2xl w-full max-w-md mx-4 border-2 border-yellow-600 relative z-10">
        <div className="text-center mb-6">
          <h1 className="text-4xl font-bold bg-gradient-to-b from-yellow-400 via-yellow-500 to-yellow-600 bg-clip-text text-transparent mb-2">
            Welcome Back
          </h1>
          <div className="h-1 bg-gradient-to-r from-transparent via-red-600 to-transparent mb-4"></div>
          <p className="text-yellow-100 font-semibold">Ready to Double Down with Devito?</p>
        </div>
        <p className="text-gray-300 text-center mb-8">Sign in to continue</p>

        <div className="space-y-4">
          <button
            className="w-full flex items-center justify-center gap-3 px-6 py-3 bg-white text-gray-700 rounded-lg font-medium hover:bg-gray-100 transition-colors duration-200 shadow-md hover:shadow-lg"
            onClick={handleGoogleSignIn}
          >
            <svg className="w-5 h-5" viewBox="0 0 24 24">
              <path
                fill="#4285F4"
                d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
              />
              <path
                fill="#34A853"
                d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
              />
              <path
                fill="#FBBC05"
                d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
              />
              <path
                fill="#EA4335"
                d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
              />
            </svg>
            Sign in with Google
          </button>
        </div>

        <p className="text-gray-400 text-sm text-center mt-8">
          By signing in, you agree to our Terms of Service and Privacy Policy
        </p>
      </main>
    </div>
  );
}
