# MSG2 Toolbox

A collection of small Unity Editor utilities. Editor-only: nothing from this package ends up in your builds.

## Installation

**Git URL:** *Window → Package Manager → + → Add package from git URL…*

```
https://github.com/Muppetsg2/MSG2Toolbox.git
```

Pin a version with a tag, e.g. `https://github.com/Muppetsg2/MSG2Toolbox.git#v1.0.0`.

Requirements: Unity **2021.3+**. Installing from a git URL needs Git installed and on your `PATH`.

## Tools

### Scene Switcher

A Scene View overlay for switching scenes quickly.

Enable it in the Scene View: press **`** (backtick) or right-click the Scene View toolbar → **Overlays** →
**Scene Switcher**.

| Element | What it does |
|---|---|
| Scene dropdown | Shows the active scene and lists the enabled scenes from *Build Settings* |
| ▶ toggle | Enters Play Mode right after switching scene |
| 👁 toggle | Lists **all** scenes in `Assets` (grouped into submenus by folder) instead of only Build Settings |

Unsaved changes trigger the usual *Save / Don't Save / Cancel* dialog; Cancel keeps the current scene.
Switching is disabled during Play Mode. Toggle states are stored per project in `UserSettings/`.

## License

[MIT](LICENSE)
