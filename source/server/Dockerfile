FROM node:22-bookworm-slim AS build

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
COPY --chown=node:node content ./content

ENV WRANGLER_SEND_METRICS=false
RUN pnpm run deploy:dry && cp "$(node -p "require('workerd').default")" /app/workerd-bin

# The runtime image keeps node only for the compose health check; the relay itself is workerd alone. workerd refuses
# to start without the system CA certificates.
FROM node:22-bookworm-slim

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
RUN mkdir state && chown node:node state
COPY --from=build /app/workerd-bin /usr/local/bin/workerd
COPY --from=build /app/dist/index.js ./dist/index.js
COPY workerd ./workerd
USER node

EXPOSE 8787
CMD ["sh", "workerd/supervise.sh"]
