# Deliberate Interact 1.1.0

A client-side Valheim mod by Swiftfingers that prevents accidental edits and equipment removal.

| Action | Default control |
| --- | --- |
| Edit a sign | Shift + E |
| Rename a portal tag | Shift + E |
| Take equipment off an armor stand | Hold E |
| Mount an item on an armor stand or item stand | Hold E |

Signs and portals follow the same Alternate Use and Use bindings as tame renaming. Stand interactions use Valheim's item-stand hold timing.

For mounting, the game checks your right hand, then your left hand, then hotbar slots 1–8 for a compatible item. The hover prompt names the item that will mount. If a shared armor-stand control can mount an item in a free slot, mounting takes priority. Put away compatible items or move them off the hotbar to take equipment off that control. An occupied item stand continues to use Hold E for removal.

Direct hotbar mounting and armor-stand pose controls still work. The vanilla editing dialogs, access checks, and portal travel are preserved.

Install BepInExPack Valheim, then import the release ZIP as a local mod in r2modman and launch Valheim modded. Only the client needs the mod.

To build against a local game installation, run `scripts/build.ps1 -ValheimDirectory 'C:/path/to/Valheim'`.

Inspired by [PreventAccidentalInteraction](https://github.com/PhantomGamers/Valheim-PreventAccidentalInteraction) by PhantomGamers.
