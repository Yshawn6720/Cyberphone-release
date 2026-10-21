// Two simulated clients against a running relay: create, join, receive state, leave.
// Usage (bash): RELAY_INVITE=<invite> node scripts/smoke.mjs https://cyberphone-relay.<subdomain>.workers.dev
const base = process.argv[2];
const invite = process.env.RELAY_INVITE;
if (base === undefined || invite === undefined || invite.length === 0) {
  console.error("usage: RELAY_INVITE=<invite> node scripts/smoke.mjs <relay base url>");
  process.exit(2);
}

const realtimeBase = `${base.replace(/\/+$/, "").replace(/^http/, "ws")}/rt`;

/**
 * @param {string} query
 * @returns {Promise<{ socket: WebSocket, next: (type: string) => Promise<Record<string, unknown>> }>}
 */
async function open(query) {
  const socket = new WebSocket(`${realtimeBase}?${query}`, { headers: { Authorization: `Bearer ${invite}` } });
  /** @type {Record<string, unknown>[]} */
  const inbox = [];
  /** @type {Array<() => void>} */
  let waiters = [];
  socket.addEventListener("message", (event) => {
    inbox.push(JSON.parse(String(event.data)));
    const current = waiters;
    waiters = [];
    current.forEach((wake) => wake());
  });
  const opened = Promise.withResolvers();
  socket.addEventListener("open", () => opened.resolve(undefined));
  socket.addEventListener("error", () => opened.reject(new Error(`connection failed: ${query.split("&")[0]}`)));
  await opened.promise;

  /**
   * @param {string} type
   * @returns {Promise<Record<string, unknown>>}
   */
  async function next(type) {
    const deadline = Date.now() + 10000;
    while (Date.now() < deadline) {
      const index = inbox.findIndex((message) => message.type === type);
      if (index >= 0) {
        return inbox.splice(index, 1)[0];
      }

      const arrived = Promise.withResolvers();
      waiters.push(() => arrived.resolve(undefined));
      await Promise.race([arrived.promise, new Promise((resolve) => setTimeout(resolve, 250))]);
    }

    throw new Error(`timed out waiting for ${type}`);
  }

  return { socket, next };
}

/**
 * @param {string} name
 * @returns {string}
 */
function identity(name) {
  return `name=${encodeURIComponent(name)}&world=${encodeURIComponent("Smoke")}`;
}

const host = await open(`create=1&${identity("SmokeHost")}`);
console.log("host connected");
host.socket.send(JSON.stringify({ type: "stream.state", url: "https://example.com/video", positionSeconds: 10, paused: false }));
const echo = await host.next("stream.state");
const code = String(echo.code);
console.log(`room created: code=${code} features=${echo.features} hostId=${echo.hostId}`);

const viewer = await open(`code=${code}&${identity("SmokeViewer")}`);
viewer.socket.send(JSON.stringify({ type: "stream.join", code, features: 1 }));
const roster = await host.next("stream.roster");
console.log(`host saw roster: ${JSON.stringify(roster.participants)}`);
host.socket.send(JSON.stringify({ type: "stream.state", url: "https://example.com/video", positionSeconds: 12, paused: false }));
const joined = await viewer.next("stream.joined");
console.log(`viewer joined: hostId=${joined.hostId} url=${joined.url} position=${joined.positionSeconds}`);

host.socket.send(JSON.stringify({ type: "stream.state", url: "https://example.com/video", positionSeconds: 20, paused: true }));
const state = await viewer.next("stream.state");
console.log(`viewer received stream.state: position=${state.positionSeconds} paused=${state.paused} stateAtUnixMs=${state.stateAtUnixMs}`);

viewer.socket.send(JSON.stringify({ type: "stream.leave" }));
const left = await host.next("stream.left");
console.log(`host saw viewer leave: ${left.userId}`);
host.socket.send(JSON.stringify({ type: "stream.leave" }));
host.socket.close();
console.log("host left; smoke passed");
