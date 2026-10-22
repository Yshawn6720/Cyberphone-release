# Runs the relay with `workerd serve` in Docker (see Dockerfile and docs/self-hosting-relay.md). It mirrors
# wrangler.toml: keep the Durable Object classes, rate limits and variables of both files in step.
# ../dist/index.js is the bundle from `pnpm run deploy:dry`. Durable Object storage goes to the "state" directory,
# /app/state in the container; override it with `--directory-path state=<dir>` when running elsewhere.
# Every variable comes from the environment; an unset one reaches the Worker as null and takes the code's default.
using Workerd = import "/workerd/workerd.capnp";

const config :Workerd.Config = (
  services = [
    (name = "relay", worker = .relay),
    (name = "state", disk = (path = "/app/state", writable = true)),
  ],
  sockets = [(name = "http", address = "*:8787", http = (), service = "relay")],
  extensions = [(modules = [(name = "cyberphone:ratelimit", esModule = embed "ratelimit.js", internal = true)])],
);

const relay :Workerd.Worker = (
  modules = [(name = "index.js", esModule = embed "../dist/index.js")],
  compatibilityDate = "2026-08-15",
  durableObjectNamespaces = [
    (className = "PartyRoom", uniqueKey = "cyberphone-relay-PartyRoom", enableSql = true),
    (className = "JamRoom", uniqueKey = "cyberphone-relay-JamRoom", enableSql = true),
    (className = "GameRoom", uniqueKey = "cyberphone-relay-GameRoom", enableSql = true),
    (className = "CallRoom", uniqueKey = "cyberphone-relay-CallRoom", enableSql = true),
    (className = "SlotRegistry", uniqueKey = "cyberphone-relay-SlotRegistry", enableSql = true),
  ],
  durableObjectStorage = (localDisk = "state"),
  bindings = [
    (name = "ROOMS", durableObjectNamespace = "PartyRoom"),
    (name = "JAM_ROOMS", durableObjectNamespace = "JamRoom"),
    (name = "GAME_ROOMS", durableObjectNamespace = "GameRoom"),
    (name = "CALL_ROOMS", durableObjectNamespace = "CallRoom"),
    (name = "SLOT_REGISTRY", durableObjectNamespace = "SlotRegistry"),
    (name = "CONNECT_LIMITER", wrapped = (moduleName = "cyberphone:ratelimit", innerBindings = [(name = "limit", json = "10"), (name = "period", json = "60")])),
    (name = "ADDRESS_LIMITER", wrapped = (moduleName = "cyberphone:ratelimit", innerBindings = [(name = "limit", json = "30"), (name = "period", json = "60")])),
    (name = "TOTAL_LIMITER", wrapped = (moduleName = "cyberphone:ratelimit", innerBindings = [(name = "limit", json = "600"), (name = "period", json = "60")])),
    (name = "INVITE_CODE", fromEnvironment = "INVITE_CODE"),
    (name = "CALLS_ENABLED", fromEnvironment = "CALLS_ENABLED"),
    (name = "MAX_ROOMS", fromEnvironment = "MAX_ROOMS"),
    (name = "MAX_ROOMS_PER_ADDRESS", fromEnvironment = "MAX_ROOMS_PER_ADDRESS"),
    (name = "MAX_ROOMS_PARTY", fromEnvironment = "MAX_ROOMS_PARTY"),
    (name = "MAX_ROOMS_JAM", fromEnvironment = "MAX_ROOMS_JAM"),
    (name = "MAX_ROOMS_GAME", fromEnvironment = "MAX_ROOMS_GAME"),
    (name = "MAX_ROOMS_CALL", fromEnvironment = "MAX_ROOMS_CALL"),
  ],
);
