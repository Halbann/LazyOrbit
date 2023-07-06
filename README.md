# LazyOrbit

![Lazy Orbit - Simple Mode](https://i.imgur.com/8INfw3O.png)

Lazy Orbit is a simple mod that allows you to set a vessel's orbit, land it on a surface, or teleport it to another vessel. Open the GUI by clicking the button in the APP.BAR (or press ALT+H), select a body and an altitude, and press Set Orbit. Great for testing and modding, don't use it for anything nefarious!

## Installation
1. Install [BepInEx/SpaceWarp](https://spacedock.info/mod/3277/Space%20Warp%20+%20BepInEx). (Skip if you've done this previously)
    1. Download and extract BepInEx/SpaceWarp into your game folder. If you've installed the game via Steam, then this is probably here: *C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program 2*.
1. Download and extract [Lazy Orbit Boosted](https://spacedock.info/mod/3410/Lazy%20Orbit%20Boosted) into your game's BepInEx/plugins folder.
    1. The mod's ZIP file contains a single BepInEx folder. You can drag the BepInEx folder from the ZIP right onto your KSP2 folder (e.g., *C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program 2*) to install the mod.
    1. Alternatively you may install the mod via CKAN
    1. If done correctly, you should have the following folder structure within your KSP2 game folder: *KSP2GameFolder*/**BepInEx/plugins/LazyOrbit**.

## Compatibility
* Tested with Kerbal Space Program 2 v0.1.3.1 & SpaceWarp 1.3.0.3
* Requires SpaceWarp 1.0.1

## Warning!
* Do not use this mod when preparing bug reports for the Early Access game as teleporting craft is incompatible with this purpose.
* It is advisable to perform a Quick Save (F5) prior to teleporting any vessel as teleporting will void the warranty and may damage your craft. Not kidding here, it may do damage to your craft.

## Features
* **Simple Mode**: Set the **Altitude** you want, pick the **Body** you'd like to be in orbit about, and press the **Set Orbit** button! What could possibly be simpler?
* **Advanced Mode**: Pick the **Body** you want to be in orbit about, then configure specific parameters you want to get the exact orbit you need. Set your **Semi-Major Axis** and see what you'll get for *Ap* and *Pe*. Set your **Inclination**, **Eccentricity**, **Longitude of Ascending Node**, and **Argument of Periapsis**. With these configured, press the **Set Orbit** button and before you know it you'll be in the exact orbit you want!
* **Landing Mode**: Here you can set the **Lattitude**, **Longitude**, and **Height** above ground level for the point you'd like to have your craft dropped at on the **Body** you've selected. Be sure to have landing legs and/or parachutes ready, or this might be a bumpy ride!
* **Rendezvous Mode**: Set the **Distance** (in meters) you'd like to be from a **Target** you can pick from the dropdown menu, then just press the **Rendezvous** button and you'll be there in a flash!

In all modes, clicking inside a text entry field will automatically disable game input from the keyboard and mouse. This allows you to type what you need to without inadvertently affecting the timewarp or muting the game or music. Your current Game Input state is displayed on the bottom of the GUI and will be bright Yellow when game input is disabled. Game input from your keyboard and mouse is automatically restored when you click anywhere outside a text input field, or if you should close the mod. In the image below note that Game Input is shown as Disabled.

![Lazy Orbit - Advanced Mode](https://i.imgur.com/GuGAHds.png)

All the text input fields in this mod's GUI are designed for entering numbers. If you should accidentally type something that can't be converted to a number (e.g., include non-numeric characters or too many decimal points, etc.) the mod will alert you by setting that field to red as shown above.

# Contributors

- [Halban](https://github.com/Halbann)
- [XYZ3211](https://github.com/XYZ3211)
- [Schlosrat](https://github.com/schlosrat)

# License

Lazy Orbit is distributed under the CC BY-SA 4.0 license. Read about the license here before redistributing:
https://creativecommons.org/licenses/by-sa/4.0/
