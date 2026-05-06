# Story 6 — Live Kill Feed for Active Fleets

## Status

- [x] Implement `R2Z2BackgroundService` → forward poll (200 → process → 404 → 63s sleep) → fan out to subscriptions → notify on match
- [x] Implement `IReportUpdateNotifier` → in-memory pub/sub (`ConcurrentDictionary<string, List<Action>>`)
- [x] Auto-expire subscriptions when `UtcNow > ExpiryTime`
- [x] **Test**: `ReportUpdateNotifier` — subscribe + notify → callback fires; unsubscribe → callback not fired; multiple subscribers same reportId → all called
- [x] **Test**: `R2Z2BackgroundService` — expired subscription removed before fan-out; single killmail matching 2 subscriptions → both notified; no fleet member match → nothing written, nothing notified
