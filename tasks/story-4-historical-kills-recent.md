# Story 4 — Fetch Historical Kills for Recent Roam (<24h Old Path)

## Status
- [ ] Implement `IR2Z2HistoricalFetcher` → walk `sequence.json` backwards → filter fleet members → write matching kills only
- [ ] **Test**: Killmail with no fleet member attacker/victim → not written; matching → written; stops walk when `killmail_time < start_time`
