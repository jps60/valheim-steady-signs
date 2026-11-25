# Deliberate Interact 1.0.0

A client-side Valheim mod by Swiftfingers that prevents accidental edits and equipment removal.

| Action | Default control |
| --- | --- |
| Edit a sign | Shift + E |
| Rename a portal tag | Shift + E |
| Take equipment off an armor stand | Hold E |

Signs and portals follow the same Alternate Use and Use bindings as tame renaming. Armor stand equipment removal follows the vanilla item-stand hold input. Equipping armor and changing the stand's pose work as usual. The vanilla editing dialogs, access checks, and portal travel are preserved.

Install BepInExPack Valheim, then import the release ZIP as a local mod in r2modman and launch Valheim modded. Only the client needs the mod.

To build against a local game installation, run `scripts/build.ps1 -ValheimDirectory 'C:/path/to/Valheim'`.

Inspired by [PreventAccidentalInteraction](https://github.com/PhantomGamers/Valheim-PreventAccidentalInteraction) by PhantomGamers.
