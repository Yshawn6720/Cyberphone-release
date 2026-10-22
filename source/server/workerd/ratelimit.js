// The [[ratelimits]] bindings of wrangler.toml for `workerd serve`: a fixed window per key aligned to the clock, the
// way Miniflare's RateLimiterObject counts under `wrangler dev`. A refused call does not count, and the whole map is
// dropped when the window rolls over, so it holds at most one window of keys.

/**
 * @param {{ limit: number, period: number }} env
 * @returns {{ limit(options: { key?: string } | undefined): Promise<{ success: boolean }> }}
 */
export default function rateLimiter(env) {
  const periodMilliseconds = env.period * 1000;
  let window = -1;
  /** @type {Map<string, number>} */
  let counts = new Map();
  return {
    async limit(options) {
      const key = options?.key ?? "";
      const current = Math.floor(Date.now() / periodMilliseconds);
      if (current !== window) {
        window = current;
        counts = new Map();
      }

      const used = counts.get(key) ?? 0;
      if (used >= env.limit) {
        return { success: false };
      }

      counts.set(key, used + 1);
      return { success: true };
    },
  };
}
