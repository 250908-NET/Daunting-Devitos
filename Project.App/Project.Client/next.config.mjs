/** @type {import('next').NextConfig} */
const nextConfig = {
  // output: 'export', // Commented out to support dynamic routes like /player/[id]
  images: {
    domains: ['www.shutterstock.com', 'lh3.googleusercontent.com'], 
  },
};

export default nextConfig;
