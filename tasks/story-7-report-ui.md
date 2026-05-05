# Story 7 — Report UI

## Status
- [ ] Home page (`/`) — textarea + UTC datetime pickers + submit + inline progress
- [ ] `ReportProgress` component — `StateHasChanged()` steps ("Resolving characters…", "Fetching sequence…")
- [ ] Report page (`/r/{shortId}`) — summary bar + chronological kill table (ship img, ISK, system, portraits, zkill link)
- [ ] Subscribe on mount, re-read LiteDB + `StateHasChanged()` on notify, unsubscribe on unmount
- [ ] Live badge when `end_time > now`
- [ ] Unknown `shortId` → friendly 404
