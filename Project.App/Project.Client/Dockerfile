FROM node:24-alpine AS build

# set working directory
WORKDIR /src

# install dependencies
COPY package.json package-lock.json ./
RUN npm ci

# copy frontend code
COPY . . 

# build
RUN npm run build

# --- run frontend from nginx server (static) ---
# FROM nginx:alpine AS run

# # copy nginx config (if anything other than the default is required)
# # COPY nginx.conf /etc/nginx/conf.d/default.conf

# # copy built files from build
# COPY --from=build /src/out /usr/share/nginx/html

# # according to richard this does nothing :/
# EXPOSE 80

# # run (in the foreground)! hopefully.
# CMD ["nginx", "-g", "daemon off;"] 

# --- run frontend from node server (if SSR is required) ---
FROM node:24-alpine AS run

# set working directory
WORKDIR /app

# copy built files from build
COPY --from=build /src/.next ./.next
# copy static files
COPY --from=build /src/public ./public 

# install dependencies (without dev deps)
COPY package.json package-lock.json ./
RUN npm ci --omit=dev

# run! (on node server)
CMD ["npm", "run", "start"]
