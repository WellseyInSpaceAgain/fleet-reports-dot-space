# Story 5 — Create Report + Shareable URL

## Status
- [ ] Implement `IReportService` → orchestrate name resolution + source selection + fetch → compute stats → persist `ReportDocument` with NanoId
- [ ] If `end_time > now`, register `FleetSubscription` with background service
- [ ] **Test**: Fleet member as attacker → `kill_ids`; as victim → `loss_ids`; same `killmail_id` in both → appears in both lists
- [ ] **Test**: ISK totals sum correctly; `end_time ≤ now` → no subscription registered
- [ ] **Test**: Source selection: `end_time > (now − 24h)` → R2Z2 fetcher called; older → zkillboard fetcher called
