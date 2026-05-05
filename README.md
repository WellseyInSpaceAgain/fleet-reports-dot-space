# fleet-reports.space

A Blazor Server (.NET 10) web app for generating EVE Online fleet roam reports — a self-hosted replacement for [roamreport.com](https://www.roamreport.com).

## What it does

Paste a list of fleet member names and a UTC time range. The app:

1. Resolves character names to IDs via ESI
2. Fetches historical kills from either R2Z2 (roams <24h old) or the zKillboard character API + ESI (older roams)
3. Generates a shareable report at a short URL (e.g. `/r/aB3xKp9mQr`)
4. Live-updates the report in real time if the fleet end time is in the future

Only killmails involving a fleet member (as attacker or victim) are ever written to the database.

## Tech stack

- **Blazor Server** (.NET 10)
- **LiteDB** — embedded document store for killmails and reports
- **zKillboard R2Z2 API** — live kill feed and recent historical data
- **EVE ESI** — character resolution, killmail details, system names
- **NanoId** — short shareable report URLs

## Data sources

| Roam age | Kill source |
|---|---|
| < 24 hours | R2Z2 backwards walk — full killmail data in sequence files |
| ≥ 24 hours | zKillboard character API → ESI for full data (3s delay between requests) |

## Report features

- Summary: fleet size, kills, ISK destroyed, losses, ISK lost
- Chronological kill table with ship images, portraits, system, ISK value, and links to zkillboard
- Live badge with real-time updates while the fleet end time is in the future
- Persistent — reports survive app restarts

## Running locally

```bash
dotnet run --project src/FleetReports
```

## References

- [zKillboard R2Z2 API](https://github.com/zKillboard/zKillboard/wiki/API-(R2Z2))
- [EVE ESI](https://esi.evetech.net/ui/)
- [EVE Image Server](https://developers.eveonline.com/docs/services/image-server/)
