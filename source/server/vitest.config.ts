import { cloudflareTest } from "@cloudflare/vitest-pool-workers";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [
    cloudflareTest({
      wrangler: { configPath: "./wrangler.toml" },
      miniflare: {
        bindings: { INVITE_CODE: "test-invite-code" },
        ratelimits: {
          CONNECT_LIMITER: { namespace_id: "4101", simple: { limit: 10000, period: 60 } },
          ADDRESS_LIMITER: { namespace_id: "4102", simple: { limit: 10000, period: 60 } },
          TOTAL_LIMITER: { namespace_id: "4103", simple: { limit: 10000, period: 60 } },
        },
      },
    }),
  ],
});
