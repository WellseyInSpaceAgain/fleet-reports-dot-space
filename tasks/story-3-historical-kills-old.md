# Story 3 — Fetch Historical Kills for Roam (>24h Old Path)

## Status
- [ ] Implement `IZkillCharacterFetcher` → per-character zkillboard API, 3s rate limit, paginate
- [ ] Implement `IKillmailCacheService` → LiteDB get/set, compute `top_damage_id`, `final_blow_id`, resolve `system_name`
- [ ] ESI fallback on cache miss: `GET /killmails/{id}/{hash}/` → time filter → write or discard
- [ ] **Test**: `KillmailCacheService` — `top_damage_id` picks highest `damage_done`, `final_blow_id` picks correct attacker, tie-break deterministic
- [ ] **Test**: `ZkillCharacterFetcher` — dedup by `killmail_id` across multiple fleet members; outside time range → discarded
- [ ] **Test**: Cache hit same hash → no ESI call; cache miss → ESI called once per killmail
