# Storage Tweaks for Vintage Story

A small quality of life mod that makes managing storage and inventory a bit easier with basically three features

![Image](https://github.com/user-attachments/assets/941a4ccc-c964-49b5-bf7a-984da9df5d40)

## Sort & Compact
The sort button combines stacks if possibe and sorts items in the chest by class and code. This functionality is also available in the player inventory but doesn't touch the hotbar.

## Quick Unload
The quick unload button moves matching items from your inventory into the chest.

## Favorites
Favorites do not get unloaded with the quick unload/store buttons.

<details>
<summary>List of items added to favorites list when mod is first loaded</summary>
  <pre>axe
knife
pickaxe
pie
tongs
arrow
bow
bowl
cleaver
cookingpot
crock
falx
hammer
hoe
lantern
saw
scythe
shears
shield
shovel
spear
sword
torch</pre>
</details>

## Config
Configuration is optional and I strive to make the defaults good but configuration is available regardless

<details>
<summary>
The following options are available
</summary>

#### `storagetweaks.json` (client side config)

 - `HideFavorites`
   - Default: `false`
   - Hides the yellow corner of slots that contain favorited items.
   - While the favoriting mode is active in the inventory the yellow corner will be visible regardless of what this option is set to.
 - `StackPerishables`
   - Default: `false`
   - You can also toggle this option in the titlebar of the player inventory
   - When enabled this option will cause sorting and unload (quick stack) actions to stack items of different spoil rates rather then placing them in their own slots.
 - `SortHotbarWithBackpack`
   - Default: `false`
   - When enabled the inventory sort action will also sort none favorited items out of the hotbar.
 - `HideSortButton`
   - Default: false
   - When enabled hides the sort button in the inventory and chests (useful for those that prefer to only use the hotkeys)
 - `HideStoreNearbyButton`
   - Default: `false`
   - When enabled hides the store nearby button
 - `HideStackPerishablesButton`
   - Default: `false`
   - When enabled hides the button in the player inventory titlebar that allows toggling StackPerishables
 - `HideQuickStoreButton`
   - Default: `false`
   - When enabled hides the quick store button in container titlebars

#### `storagetweaks-server.json` (server side config)
 - `QuickStoreNearbySearchRadius`
   - Default: `8`
   - Configures the search radius that quick store nearby will use to find suitable containers for storing matching items
 - `AdditionalContainerWhitelist`
   - Default: `[]` (empty list)
   - Adds additional containers for quick store nearby to use if they are not in the built-in whitelist
   - Supports wildcard item/block codes to whitelist for quick store nearby
   - Consider sharing your whitelist so it can be added to the built-in whitelist of this mod for other users' convenience
   - Example
     ```json
     ["game:chest-*", "foodshelves:fruitbasket-normal"]
     ```
 - `ContainerBlacklist`
   - Default: `[]`
   - Blacklists containers from being used by quick store nearby. Takes priorty over both the built-in whitelist and the `AdditionalContainerWhitelist`
</details>

You can also use the Config lib mod to configure this mod in game
