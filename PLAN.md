# Plan: Eve Online Roam Report Clone

**TL;DR**: Blazor Server (.NET 10) web app. User pastes fleet member names + UTC time range → names resolved to IDs via ESI → historical kills fetched (R2Z2 backwards walk if <24h, zkillboard character API + ESI if older) → report created in LiteDB with shareable NanoId URL → live forward-polling background service fans out new kills to active subscriptions → Blazor component notified via thin pub/sub, re-reads LiteDB read-only. **Only killmails relevant to a fleet member are ever written to LiteDB.**

---

## Data Schemas

### `killmails` collection

```
{
  _id:            int       // killmail_id — natural PK, stable, never reused
  hash:           string    // dedup: if hash differs on same _id → re-processed, update
  killmail_time:  DateTime
  system_name:    string    // resolved via ESI at fetch time, stored permanently

  victim_id:      int?      // character_id → images.evetech.net/characters/{id}/portrait
  ship_type_id:   int       // → images.evetech.net/types/{id}/render

  top_damage_id:  int?      // character_id with highest damage_done (computed at fetch)
  final_blow_id:  int?      // character_id with final_blow=true (computed at fetch)

  total_value:    decimal   // ISK from zkb.total_value
}
```

### `reports` collection

```
{
  _id:                  string    // NanoId, e.g. "aB3xKp9mQr"
  created_at:           DateTime

  fleet_member_names:   string[]  // original pasted names (display only)
  fleet_member_ids:     int[]     // resolved character IDs (used for filtering)
  start_time:           DateTime  // UTC
  end_time:             DateTime  // UTC — also used as subscription expiry

  kill_ids:             int[]     // killmail_ids where fleet member is attacker
  loss_ids:             int[]     // killmail_ids where fleet member is victim
                                  // note: same killmail_id can appear in both

  // pre-computed to avoid reprocessing on every page load
  total_kills:          int
  total_losses:         int
  isk_destroyed:        decimal
  isk_lost:             decimal
  top_damage_dealer_id: int?
}
```

---

## Phase 1 — Foundation

1. Create `.NET 10` Blazor Server project
2. Add NuGet: `LiteDB`, `Nanoid`, `Microsoft.Extensions.Http.Polly`
3. Register `LiteDatabase` as singleton with `killmails` and `reports` collections
4. Define domain models: `KillmailDocument`, `ReportDocument`, `FleetSubscription`

## Phase 2 — ESI + Character Resolution

5. `IEsiService` (typed `HttpClient`): required headers `User-Agent` (403 without it) and `Accept-Encoding: gzip`. Used for name resolution, killmail fetch, and system name lookup.
6. `ICharacterService`: single `POST /universe/ids/` call per report submission → `Dictionary<string, int>` (name → character_id). Only purpose is filtering — never used for display.
    - Image URLs built directly from IDs already in killmail data:
        - Portrait: `https://images.evetech.net/characters/{id}/portrait`
        - Ship render: `https://images.evetech.net/types/{typeId}/render`

## Phase 3 — Historical Killmail Fetch (one-shot at report creation)

**Source selection** (boundary is `now − 24h`):

- `end_time > (now − 24h)` → R2Z2 backwards walk
- `end_time ≤ (now − 24h)` → zkillboard character API + ESI

> **History file (`r2z2.zkillboard.com/history/YYYYMMDD.json`) is not used** — it contains only `{killmail_id: hash}` pairs with no victim/attacker data, making it impossible to filter without fetching every entry from ESI individually. The zkillboard character API pre-filters by character and is far more efficient.

7. **`IR2Z2HistoricalFetcher`** (one-shot): start from current `sequence.json` → walk backwards → stop when `killmail_time < start_time`. Full killmail data present in each sequence file — filter before write:
    - Any `fleet_member_id` in `victim.character_id` OR any `attacker.character_id` → compute fields → write to LiteDB
    - No match → discard, nothing written

8. **`IZkillCharacterFetcher`** (used for roams >24h old): for each `fleet_member_id`, call zkillboard character API:

    ```
    GET https://zkillboard.com/api/characterID/{id}/year/Y/month/m/
    ```

    - **Rate limiting**: wait 3 seconds between every HTTP request to the zkillboard character API (courteous pacing; ~50 requests for a 25-member fleet ≈ ~2.5 min total)
    - Paginate until all kills for that character in the relevant month(s) are retrieved
    - Returns `{killmail_id, hash, zkb: {total_value, ...}}` only — full killmail data unavailable
    - Collect and deduplicate by `killmail_id` across all fleet members
    - Filter by `killmail_time` — not available from zkillboard character API, so for each deduplicated entry:
        - Check LiteDB cache first
        - Cache hit + same hash → use cached, check time range
        - Cache miss → `GET /killmails/{id}/{hash}/` on ESI → get full data including `killmail_time`
        - If `killmail_time` within `start_time`/`end_time` → compute fields → write to LiteDB
        - Outside time range → discard

9. **`IKillmailCacheService`**: LiteDB get/set by `_id`. At write time: compute `top_damage_id`, `final_blow_id`, resolve `system_name` via ESI (deduplicated with a per-fetch-run `Dictionary<int, string>` in memory).

## Phase 4 — Report Generation

10. **`IReportService`**:
    - Resolve names → choose source → call historical fetcher → results already filtered
    - Kill = fleet member in attackers, loss = fleet member is victim, deduplicate by `killmail_id`
    - Compute stats, persist `ReportDocument` with NanoId `_id`
    - If `end_time > now`: register `FleetSubscription` with `R2Z2BackgroundService`
    - Return short ID → redirect to `/r/{shortId}`

## Phase 5 — Live Forward Polling

11. **`R2Z2BackgroundService : BackgroundService`** (singleton):
    - Maintains `ConcurrentDictionary<string, FleetSubscription>` (reportId → subscription)
    - Each `FleetSubscription`: `HashSet<int>` pilot IDs + `DateTime` expiry
    - **One polling loop**: only active when subscription dictionary is non-empty, idles otherwise
    - Iterates R2Z2 forward per best practice: 200 → process, sleep 100ms, sequence++ → 404 → sleep 63s, retry same sequence
    - For each fetched killmail, fan out to all active subscriptions in one pass:
        - Any `fleet_member_id` matches → write to LiteDB (cache + update report) → `IReportUpdateNotifier.Notify(reportId)`
        - No match → discard, nothing written to LiteDB
    - A single killmail can match multiple subscriptions
    - Remove subscriptions where `UtcNow > ExpiryTime`

12. **`IReportUpdateNotifier`** (singleton, thin pub/sub):
    - `Subscribe(reportId, Action callback)` / `Unsubscribe(reportId)` / `Notify(reportId)`
    - Internally: `ConcurrentDictionary<string, List<Action>>`
    - No LiteDB polling, no Channels — purely in-memory push

## Phase 6 — Blazor UI

13. **Home page** (`/`): fleet member `<textarea>` + UTC start/end datetime pickers + submit → inline progress
14. **Progress component**: `StateHasChanged()` driven updates — "Resolving characters…", "Fetching sequence 96088891…", "Loading kills for character 12345678…"
15. **Report page** (`/r/{shortId}`):
    - On mount: read report from LiteDB + `IReportUpdateNotifier.Subscribe(...)`
    - On notification: re-read report from LiteDB (read-only) + `StateHasChanged()`
    - On unmount: `IReportUpdateNotifier.Unsubscribe(...)`
    - Summary bar: fleet size, kills, ISK destroyed, losses, ISK lost
    - Chronological kill table: time | ship image | ISK value | system name | victim portrait | top damage portrait | final blow portrait | zkillboard link (`https://zkillboard.com/kill/{id}/`)
    - Live badge shown if `end_time > now`
16. Unknown short ID → friendly 404

---

## Files to Create

- `Services/EsiService.cs`
- `Services/CharacterService.cs`
- `Services/R2Z2HistoricalFetcher.cs`
- `Services/ZkillCharacterFetcher.cs`
- `Services/KillmailCacheService.cs`
- `Services/ReportService.cs`
- `Services/R2Z2BackgroundService.cs`
- `Services/ReportUpdateNotifier.cs`
- `Components/Pages/Home.razor`
- `Components/Pages/Report.razor`
- `Components/ReportProgress.razor`

---

## Verification

1. Same fleet + time as existing roamreport.com link → compare kill count and ISK totals
2. Roam >24h → zkillboard character API + ESI path, LiteDB cache hit confirmed on second run (no ESI calls)
3. Recent roam <24h → R2Z2 historical backwards walk, only matching kills written to LiteDB
4. Live fleet: `end_time` in future → new kills appear in real time, multiple browser tabs all update
5. Fleet end time passes → subscription expires, background service idles
6. App restart → `/r/{shortId}` still resolves (LiteDB persistence)
7. Confirm zero non-fleet killmails written to LiteDB during any path
8. `docker build` + run on Ubuntu → Linux compatibility confirmed

---

## Out of Scope (v1)

- Corp/alliance-based input
- Battle report style (br.evetools.org territory)
- EVE SSO / private killmails
- Ship/system name resolution beyond the kill table
- History file (`r2z2.zkillboard.com/history/YYYYMMDD.json`) — not used

---

## Reference Links

- [zKillboard R2Z2 API](<https://github.com/zKillboard/zKillboard/wiki/API-(R2Z2)>)
- [zKillboard History API](<https://github.com/zKillboard/zKillboard/wiki/API-(History)>)
- [zKillboard Killmails API](<https://github.com/zKillboard/zKillboard/wiki/API-(Killmails)>)
- [EVE ESI](https://esi.evetech.net/ui/)
- [EVE Image Server](https://developers.eveonline.com/docs/services/image-server/)
- [roamreport.com](https://www.roamreport.com) — tool being replaced
