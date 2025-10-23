'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

export default function NavBar() {
  const pathname = usePathname();

  const handleLogout = async () => {
    console.log('[NavBar] Logout clicked');

    try {
      const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7069';
      console.log('[NavBar] Calling logout endpoint:', `${apiBaseUrl}/auth/logout`);

      const response = await fetch(`${apiBaseUrl}/auth/logout`, {
        method: 'POST',
        credentials: 'include',
      });

      console.log('[NavBar] Logout response status:', response.status);

      if (response.ok) {
        console.log('[NavBar] Logout successful, redirecting to /login');
        window.location.href = '/login';
      } else {
        console.error('[NavBar] Logout failed with status:', response.status);
        // Redirect anyway to force re-authentication
        window.location.href = '/login';
      }
    } catch (error) {
      console.error('[NavBar] Logout error:', error);
      // Redirect anyway to force re-authentication
      window.location.href = '/login';
    }
  };

  let links = [
    // TODO: Replace '/player/1' with the real logged-in player's id when auth is wired up
    // { href: '/player/1', label: 'Profile' },
    // { href: '/rooms', label: 'Rooms'},
  ];
  if (pathname.startsWith('/player/')) {
    links = [{ href: '/rooms', label: 'Rooms' }];
  } else if (pathname.startsWith('/rooms')) {
    links = [{ href: '/player/1', label: 'Profile' }];
  } else if (pathname.startsWith('/game/')) {
    // No additional links in game view - leave button is in the game UI
    links = [];
  }

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 w-full bg-black/80 border-b-2 border-yellow-600 py-3 px-6 flex gap-6 items-center shadow-lg backdrop-blur">
      <span className="font-bold text-yellow-400 text-xl tracking-wide">Double Down Devito</span>
      <div className="flex gap-4 ml-auto">
        {links.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className="text-yellow-100 hover:text-yellow-400 font-semibold px-3 py-1 rounded transition"
          >
            {link.label}
          </Link>
        ))}
        {/* Show logout button only on protected pages */}
        {(pathname.startsWith('/player/') || pathname.startsWith('/rooms') || pathname.startsWith('/game/')) && (
          <button
            onClick={handleLogout}
            className="text-yellow-100 hover:text-yellow-400 font-semibold px-3 py-1 rounded transition"
          >
            Logout
          </button>
        )}
      </div>
    </nav>
  );
}

// src/components/NavBar.jsx
// export default function NavBar({ route, onNavigate, onLogout, role }) {
//   return (
//     <nav className="app-navbar" aria-label="Primary">
//       <button
//         className="app-nav-btn"
//         onClick={() => onNavigate("profile")}
//         aria-current={route === "profile" ? "page" : undefined}
//       >
//         Profile
//       </button>
//       <button
//         className="app-nav-btn"
//         onClick={() => onNavigate("games")}
//         aria-current={route === "games" ? "page" : undefined}
//       >
//         Games
//       </button>

//       {role === "admin" && (
//         <button
//           className="app-nav-btn"
//           onClick={() => onNavigate("admin")}
//           aria-current={route === "admin" ? "page" : undefined}
//         >
//           Admin
//         </button>
//       )}

//       <button className="app-nav-btn" onClick={onLogout}>Logout</button>
//     </nav>
//   );
// }
