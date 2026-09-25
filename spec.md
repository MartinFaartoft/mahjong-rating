# Mahjong Rating list

## Functional requirements

### Users and players
- A player can play one or more rulesets

- A user can be connected to a player

- A user can have the admin role, granting certain access

### Games history
- As an admin, I want to be able to add a game containing:
    - Ruleset, Riichi or MCR
    - Number of winds
    - Score for each player in the game. Variable number of players (usually 3-7, but no hard constraint needed)
    - Timestamp of when the game was finished
    - Timestamp of when the game was added
    - The user who added the game

- FUTURE: As a user, I want to be able to add a game with above values in a "suggested" state, pending admin approval

- FUTURE: As an admin, I want to see a list of games pending approval, and either approve or reject each

- As a user, I want to be able to browse the list of games played, separated by ruleset

### Rating calculation
- As a user, I want to see a list of current ratings for each player, for a given ruleset

- When a game is added, updated, or deleted, the rating for each player in that game (and every subsequent game for the same ruleset) is recomputed from the affected timestamp forward. The formula reproduces the legacy mahjongdk.dk implementation bit-for-bit so historical ratings replay unchanged; it deviates from the naive spec in a few documented places (see below).

Per-player, per-game update:

    player_new_rating = player_old_rating + (player_score * gl_score + game_difficulty - player_old_rating) / damping_factor

where:

- `player_old_rating` — the player's current rating (or 0 for their first game).

- `gl_score` — score multiplier:
    - MCR: `4 / number_of_winds`
    - Riichi: `2 / number_of_winds`

- `gl_damp` — damping multiplier:
    - MCR: `4 / number_of_winds` (same as `gl_score`)
    - Riichi 4+ player table: `2 / number_of_winds` (same as `gl_score`)
    - Riichi 3-player table: `4 / number_of_winds` (doubled — Sanma damping quirk)

- `damping_factor = 40 * gl_damp + 1` (the `+1` matches the legacy impl and is intentional)

- `game_difficulty` — average of `player_old_rating` for all participants, using the following denominator:
    - MCR: `max(4, player_count)` (the legacy impl assumed a full 4-seat table; only affects sub-4 tables, of which one exists in the entire 22-year MCR archive)
    - Riichi: `player_count`

Legacy-parity is validated end-to-end by `LegacyDatasetReplayTests`, which replays the full 22-year mahjongdk archive (10k+ MCR games, 10k+ Riichi games) through `RatingCalculator` and asserts every per-player-per-game rating matches legacy within 1e-9 relative tolerance.

A rating history is kept for each player, enabling forward recompute of ratings when a game is added, changed, or deleted.

Ratings are computed and stored in `decimal` (28-digit precision). The database column is `numeric(18,10)`; API responses round to 4 decimal places at the edge.