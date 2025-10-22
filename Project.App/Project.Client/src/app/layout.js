import { Geist, Geist_Mono } from 'next/font/google';
import NavBar from './components/NavBar';
import './globals.css';

const geistSans = Geist({
  variable: '--font-geist-sans',
  subsets: ['latin'],
});

const geistMono = Geist_Mono({
  variable: '--font-geist-mono',
  subsets: ['latin'],
});

export const metadata = {
  title: 'Double Down Devito',
  description: 'Welcome to Double Down Devito',
  icons: {
    icon: '/devito.png',
    shortcut: '/devito.png',
    apple: '/devito.png',
  },
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body className={`${geistSans.variable} ${geistMono.variable} antialiased`}>
        <NavBar />
        <div className="pt-16">{children}</div>
      </body>
    </html>
  );
}
