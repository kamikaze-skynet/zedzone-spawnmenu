# Changelog

## 1.2.0
- New Helicopter kit: calls in a helicopter from the Helicopter mod (skipping its one-per-save limit) plus 500 rounds each of 5.56mm and 7.62x39mm for its door gun. Added to an existing kits file automatically, once.
- New kit line `helicopter` that calls in a helicopter (needs the Helicopter mod).

## 1.1.0
- New Laser Gun kit (Laser Rifle + 4 Laser Battery Packs). It is added to an existing kits file automatically, once.

## 1.0.0
- First version. Insert opens a menu that lists every item the game has loaded (its own and every mod's), with search, category tabs and an amount picker. Click an item to spawn it into your inventory (anything that doesn't fit drops at your feet).
- Kits from `BepInEx\config\SpawnMenu.kits.txt`, spawned with one click. Ships with a Boltgun kit (rifle, pistol, 4 magazines each, 300 rounds of .75, magazines loaded).
- "Fill my magazines" loads every magazine you carry from the ammo you carry. "Save item list" writes every item's id and name to `BepInEx\SpawnMenu.items.txt`.
- While the menu is open the game ignores your clicks and keys, so typing a search doesn't walk you around and clicking doesn't shoot.
