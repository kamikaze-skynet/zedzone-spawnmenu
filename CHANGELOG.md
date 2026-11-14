# Changelog

## 1.0.0
- First version. Insert opens a menu that lists every item the game has loaded (its own and every mod's), with search, category tabs and an amount picker. Click an item to spawn it into your inventory (anything that doesn't fit drops at your feet).
- Kits from `BepInEx\config\SpawnMenu.kits.txt`, spawned with one click. Ships with a Boltgun kit (rifle, pistol, 4 magazines each, 300 rounds of .75, magazines loaded).
- "Fill my magazines" loads every magazine you carry from the ammo you carry. "Save item list" writes every item's id and name to `BepInEx\SpawnMenu.items.txt`.
- While the menu is open the game ignores your clicks and keys, so typing a search doesn't walk you around and clicking doesn't shoot.
