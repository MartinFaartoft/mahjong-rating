# mahjong-rating

Scaffold for a .NET 8 web API backed by Postgres, deployed via codex.

Endpoints:
- `GET /healthz` — liveness, no DB
- `GET /probe` — opens a DB connection and runs `SELECT 1 + 1`

## Local development

Requires: `dotnet` 8 SDK, Docker Desktop, `yq` (`brew install yq`).

```sh
make dev     # starts postgres on localhost:5432
make run     # runs the app on http://localhost:5000 (or whatever ASP.NET picks)
curl http://localhost:5000/probe
# → {"db":"up","one_plus_one":2}
```

Other targets: `make psql`, `make down`, `make clean`, `make gen`.

The dev DB uses password `dev`. Connection string is in `appsettings.Development.json`.

## Deployment

Push to `main` → GitHub Actions builds + pushes image to GHCR + SSHes JSON to the VPS. The reusable workflow renders a two-service compose (app + postgres) on the VPS. `DB_PASSWORD` is generated once on the VPS on first deploy and persisted in `/srv/sites/_secrets/rating.env`. It is never rotated automatically.

Data lives in the docker volume `rating_pgdata`. It survives deploys and rollbacks; it does not survive `docker volume rm`.

Migrations: none yet. When you add EF Core migrations, apply them on app startup with `db.Database.Migrate()`. Keep migrations additive so rollback is safe.

## Source of truth

`codex.yml` drives both the CI-side prod compose and the local dev compose (via `scripts/gen-dev-compose.sh`). Change the pg version there, both sides update.
