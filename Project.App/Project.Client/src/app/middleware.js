import { NextResponse } from 'next/server';

export function middleware(request) {
  const authCookie = request.cookies.get('.AspNetCore.Cookies');
  const protectedPaths = ['/rooms', '/player', '/game'];

  if (protectedPaths.some((path) => request.nextUrl.pathname.startsWith(path)) && !authCookie) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  return NextResponse.next();
}
