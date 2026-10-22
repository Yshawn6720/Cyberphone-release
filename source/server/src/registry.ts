import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import { MAX_NEARBY_ROOMS, NEARBY_RATE, SLOT_RESERVATION_MS, type RoomKind } from "./protocol";
import { PROBE_PATH, type RoomListing, type SlotOccupant } from "./room";
import { type Bucket, charge, freshBucket } from "./wire";

export const CLAIM_PATH = "/claim";
export const RELEASE_PATH = "/release";
export const NEARBY_PATH = "/nearby";

export interface SlotClaim {
  kind: RoomKind;
  id: string;
  address: string;
  start: number;
  roomCount: number;
  perAddress: number;
}

export type SlotDecision = { slot: number } | { refused: "rooms_full" | "address_rooms" };

export interface SlotRelease {
  id: string;
  slot: number;
}

export interface NearbyQuery {
  kind: RoomKind;
  territoryId: number;
  worldId: number;
  address: string;
  roomCount: number;
}

export type NearbyAnswer = { listings: RoomListing[] } | { retryAfterSeconds: number };

interface Reservation {
  id: string;
  address: string;
  until: number;
}

export function roomsOf(env: Env, kind: RoomKind): DurableObjectNamespace {
  switch (kind) {
    case "jam":
      return env.JAM_ROOMS;
    case "game":
      return env.GAME_ROOMS;
    case "call":
      return env.CALL_ROOMS;
    case "party":
      return env.ROOMS;
  }
}

export function slotStub(rooms: DurableObjectNamespace, slot: number): DurableObjectStub {
  return rooms.get(rooms.idFromName(`slot:${slot}`));
}

async function probe(rooms: DurableObjectNamespace, slot: number): Promise<SlotOccupant> {
  const response = await slotStub(rooms, slot).fetch(`https://relay.internal${PROBE_PATH}`);
  return await response.json() as SlotOccupant;
}

// One registry per room kind hands out the slot for every new room. Claims run one at a time and each asks every
// slot who holds it, so a burst of creates from one address (each with a made-up character) cannot slip past the
// per-address cap. A slot handed out stays reserved until the Worker reports its host ticket answered (or the
// reservation runs out), so the next claim cannot hand it out again in between. Nothing is stored: after a restart
// the rooms themselves are the record.
export class SlotRegistry extends DurableObject<Env> {
  private readonly reservations = new Map<number, Reservation>();
  private readonly nearbyBuckets = new Map<string, Bucket>();
  private tail: Promise<void> = Promise.resolve();

  async fetch(request: Request): Promise<Response> {
    const path = new URL(request.url).pathname;
    if (path === RELEASE_PATH) {
      const release = await request.json() as SlotRelease;
      await this.exclusive(async () => this.release(release));
      return new Response(null, { status: 204 });
    }

    if (path === NEARBY_PATH) {
      return Response.json(await this.nearby(await request.json() as NearbyQuery));
    }

    const claim = await request.json() as SlotClaim;
    return Response.json(await this.exclusive(() => this.decide(claim)));
  }

  private release(release: SlotRelease): void {
    if (this.reservations.get(release.slot)?.id === release.id) {
      this.reservations.delete(release.slot);
    }
  }

  // A nearby list reads the slots without claiming anything, so it skips the claim queue. Full buckets are dropped
  // on every query, which keeps the map to the addresses that asked within the last minute.
  private async nearby(query: NearbyQuery): Promise<NearbyAnswer> {
    const now = Date.now();
    for (const [address, bucket] of this.nearbyBuckets) {
      if (now - bucket.at >= NEARBY_RATE.capacity * NEARBY_RATE.refillMs) {
        this.nearbyBuckets.delete(address);
      }
    }

    const bucket = this.nearbyBuckets.get(query.address) ?? freshBucket(NEARBY_RATE, now);
    this.nearbyBuckets.set(query.address, bucket);
    if (charge(bucket, NEARBY_RATE, now) !== "accept") {
      return { retryAfterSeconds: Math.ceil((1 - bucket.tokens) * NEARBY_RATE.refillMs / 1000) };
    }

    const rooms = roomsOf(this.env, query.kind);
    const probes: Promise<SlotOccupant>[] = [];
    for (let slot = 0; slot < query.roomCount; slot++) {
      probes.push(probe(rooms, slot));
    }

    const listings: RoomListing[] = [];
    for (const occupant of await Promise.all(probes)) {
      const listing = occupant.listing;
      if (listing !== undefined && listing !== null && listing.territoryId === query.territoryId
        && listing.worldId === query.worldId && listings.length < MAX_NEARBY_ROOMS) {
        listings.push(listing);
      }
    }

    return { listings };
  }

  private async exclusive<T>(work: () => Promise<T>): Promise<T> {
    const previous = this.tail;
    const turn = Promise.withResolvers<void>();
    this.tail = turn.promise;
    await previous;
    try {
      return await work();
    } finally {
      turn.resolve();
    }
  }

  private async decide(claim: SlotClaim): Promise<SlotDecision> {
    const rooms = roomsOf(this.env, claim.kind);
    const probes: Promise<SlotOccupant>[] = [];
    for (let slot = 0; slot < claim.roomCount; slot++) {
      probes.push(probe(rooms, slot));
    }

    const occupants = await Promise.all(probes);
    const now = Date.now();
    const taken: boolean[] = [];
    let own = -1;
    let held = 0;
    for (let slot = 0; slot < occupants.length; slot++) {
      const occupant = occupants[slot];
      let reservation = this.reservations.get(slot);
      if (reservation !== undefined && (reservation.until <= now || occupant.occupied)) {
        this.reservations.delete(slot);
        reservation = undefined;
      }

      const holder = occupant.occupied ? occupant.hostId : reservation?.id ?? null;
      const address = occupant.occupied ? occupant.address : reservation?.address ?? null;
      taken.push(occupant.occupied || reservation !== undefined);
      if (holder === claim.id) {
        own = slot;
      } else if (taken[slot] && address === claim.address) {
        held++;
      }
    }

    if (own >= 0) {
      return this.reserve(own, claim, now);
    }

    let free = -1;
    for (let offset = 0; offset < claim.roomCount && free < 0; offset++) {
      const slot = (claim.start + offset) % claim.roomCount;
      if (!taken[slot]) {
        free = slot;
      }
    }

    if (free < 0) {
      return { refused: "rooms_full" };
    }

    if (held >= claim.perAddress) {
      return { refused: "address_rooms" };
    }

    return this.reserve(free, claim, now);
  }

  private reserve(slot: number, claim: SlotClaim, now: number): SlotDecision {
    this.reservations.set(slot, { id: claim.id, address: claim.address, until: now + SLOT_RESERVATION_MS });
    return { slot };
  }
}
