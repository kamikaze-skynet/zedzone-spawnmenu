# Spawn Menu (ZED ZONE)

A BepInEx (IL2CPP) testing mod for ZED ZONE. Press **Insert** in game to open a menu that spawns any item, plus one-click kits.

## Using it

- **Search**: just type. Backspace deletes, Ctrl+Backspace clears.
- **Tabs**: All, Mods (items added by mods), Guns, Ammo, Melee, Gear, Food, Material, Other. Tab / Shift+Tab cycles them.
- **Amount**: x1, x5, x10, x50, x100 or Stack (one full stack). Stackable items come in full stacks; other items come one by one (at most `MaxPerClick` per click).
- **Spawn**: click an item, or highlight it with the arrow keys and press Enter. Items go to your inventory, and what doesn't fit drops at your feet.
- **Kits** (right side): one click spawns the whole kit.
- **Fill my magazines**: loads every magazine you carry from the ammo you carry.
- **Save item list**: writes every item's id, type, stack size, name and mod guid to `BepInEx\SpawnMenu.items.txt`.
- **Insert** or **Esc** closes the menu. While it is open the game ignores your clicks and keys.

## Kits

Kits live in `BepInEx\config\SpawnMenu.kits.txt` and are re-read every time the menu opens (or with **Reload kits**):

```
[Boltgun kit]
mod:f5fc9e37-40ad-46b3-ad71-6be84c155ae4 1     # Boltgun
mod:bac356ff-83fd-47f1-a864-1a33e3c7ba5c 1     # Bolt Pistol
mod:8c0b9189-f401-48b1-8273-18bf8ed4f4ad 4     # Boltgun Magazine (30)
mod:a9462c6d-1c5f-4cfe-b1d7-87bf808df939 4     # Bolt Pistol Magazine (10)
mod:cfe0eb6a-4ba6-46f6-b7fc-0476d0a6cd2e 300   # .75 Bolt
fillmags

[Laser Gun kit]
mod:93b81951-6391-4707-beb4-0ea971889dc7 1     # Laser Rifle
mod:cef9bff0-579f-401b-a5a2-890dc6fd3ec8 4     # Laser Battery Pack

[Helicopter kit]
helicopter                                     # calls a helicopter in front of you (Helicopter mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Home next to it to refuel)
102 500                                        # 5.56mm rounds for the door gun
104 500                                        # 7.62x39mm rounds for the door gun
```

Kits added in a later version are appended to an existing kits file once (delete one and add a `# removed: <Kit name>` line to keep it gone).

Each line is `<item> <count>`, where `<item>` is a numeric item id, `mod:<modGuid>` for a mod's item, or the item's name in quotes. `fillmags` loads the kit's magazines from its ammo after spawning. `helicopter` calls in a helicopter from the Helicopter mod, ignoring its one-per-save limit. `gascans <n>` gives n full cans of gasoline for it. A kit item that isn't installed is skipped and named in the status line.

## Settings

`BepInEx\config\girthy_mods.zedzone.spawnmenu.cfg`: `OpenMenuKey` (Insert), `UiScale`, `ShowHiddenItems`, `MaxPerClick`, `FillMagazines`, `VerboseLog`.

Log: `BepInEx\SpawnMenu.log`.

## Building

```
dotnet build src\SpawnMenu.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\ZED ZONE"
```

`-p:CopyToMods=true` copies the DLL into `%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE\Mods\SpawnMenu`.

## Steam Workshop

```
.\publish-workshop.ps1 -SteamUser <login>
```

The first run creates a **Private** item and writes its id to `workshop\publishedfileid.txt` (commit it).
