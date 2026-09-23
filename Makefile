SHELL := /usr/bin/env bash
.PHONY: gen dev down clean psql run help

help:
	@echo "Targets:"
	@echo "  make gen    - regenerate docker-compose.dev.yml from codex.yml"
	@echo "  make dev    - start local postgres (regenerates compose first)"
	@echo "  make run    - dotnet run against local postgres"
	@echo "  make psql   - psql shell inside the db container"
	@echo "  make down   - stop the db container (keeps volume)"
	@echo "  make clean  - stop + delete db volume + delete generated compose"

gen:
	scripts/gen-dev-compose.sh > docker-compose.dev.yml

dev: gen
	docker compose -f docker-compose.dev.yml up -d --wait
	@echo ""
	@echo "postgres up on localhost:5432 (db=rating user=rating password=dev)"
	@echo "next: make run"

run:
	dotnet run

down:
	docker compose -f docker-compose.dev.yml down

clean:
	docker compose -f docker-compose.dev.yml down -v || true
	rm -f docker-compose.dev.yml

psql:
	docker compose -f docker-compose.dev.yml exec db psql -U rating -d rating
