/** @type {import('next').NextConfig} */
const nextConfig = {
  // output: 'export', // Commented out to support dynamic routes like /player/[id]

  images: {
    domains: ['www.shutterstock.com', 'lh3.googleusercontent.com'],
  },
  // Disable SSL certificate validation in development
  // WARNING: Only use this in development, never in production
  webpack: (config, { isServer }) => {
    if (isServer) {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
    }
    return config;
  },
};

export default nextConfig;
