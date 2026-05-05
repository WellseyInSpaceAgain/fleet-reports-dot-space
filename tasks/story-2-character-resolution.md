# Story 2 — Resolve Fleet Member Names to Character IDs

## Status
- [ ] Implement `IEsiService` typed `HttpClient` (User-Agent + gzip headers)
- [ ] Implement `ICharacterService` → `POST /universe/ids/` → `Dictionary<string, int>`
- [ ] **Test**: `CharacterService` — mock `HttpClient`, assert name→ID mapping, assert unknown names excluded
