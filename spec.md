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

- When a game is added, updated, or deleted, the rating for each player in that game should be updated, using the following formula: 
player_new_rating = player_old_rating + (player_score * game_length_factor + game_difficulty - player_old_rating) / damping_factor, where: 

player_old_rating is the players current rating (or 0 for their first game)

game_difficulty is the average of player_old_rating for all participating players

game_length_factor = 4 / number_of_winds (for MCR) and 2 / number_of_winds for Riichi,

damping_factor = 40 * game_length_factor

A simple example would be a 4 wind MCR game, with new players (player_old_rating = 0) with the following scores:
player_1 = 40
player_2 = -40
player_3 = 0
player_4 = 0

which should yield the following new ratings:
player_1 = 1
player_2 = -1
player_3 = 0
player_4 = 0

- A rating history should be kept for each player, enabling forward recompute of ratings, when a game is added, changed or deleted