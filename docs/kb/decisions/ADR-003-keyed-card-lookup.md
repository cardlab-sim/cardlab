# ADR-003: Cards are found by a keyed one-way hash; the card number is never stored or shown

- **Status:** accepted (feature 001-card-controls, design DEC-8, DEC-14, 2026-11-04)
- **Context:** No full card number may appear in logs, errors, audit events, stored records
  (beyond what lookup needs), API responses or UI screens (NFR-1). Authorisation requests identify
  the card by its 16-digit number.
- **Decision:** `LookupHash = HMAC-SHA256(key, digits)` stored as `binary(32)` with a unique
  index, plus the last four digits. The key comes from configuration `CardLab:LookupKey`: a fixed,
  documented non-secret development key in the repo (fictional data) and a random key set once as
  an App Service application setting in Azure (never in GitHub). The admin UI never handles a card
  number: the simulate-purchase form calls `POST /api/cards/{id}/simulate-purchase`, which shares
  the authorisation service with `POST /api/authorisations`.
- **Alternatives:** unkeyed SHA-256 (brute-forceable over 10^16 inputs); Azure Key Vault (extra
  resource and setup for no gain at this horizon); the SPA sending card numbers (violates NFR-1).
- **Consequences:** rotating the key requires re-seeding lookup values (the idempotent seed
  recomputes them on start); tests scan logs, responses, rows and screens for 16-digit runs.
