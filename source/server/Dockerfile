FROM node:22-bookworm-slim

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN npm install --global pnpm@10.34.4

WORKDIR /app
RUN chown node:node /app
USER node

COPY --chown=node:node package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY --chown=node:node wrangler.toml tsconfig.json ./
COPY --chown=node:node src ./src

ENV WRANGLER_SEND_METRICS=false CLOUDFLARE_INCLUDE_PROCESS_ENV=true
EXPOSE 8787
CMD ["pnpm", "exec", "wrangler", "dev", "--local", "--ip", "0.0.0.0", "--port", "8787", "--show-interactive-dev-session=false"]
