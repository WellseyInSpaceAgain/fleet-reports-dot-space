# Story 2 — Resolve Fleet Member Names to Character IDs

## Status

- [x] Implement `IEsiService` typed `HttpClient` (User-Agent + gzip headers)
- [x] Implement `ICharacterService` → `POST /universe/ids/` → `Dictionary<string, int>`
- [x] **Test**: `CharacterService` — mock `HttpClient`, assert name→ID mapping, assert unknown names excluded
