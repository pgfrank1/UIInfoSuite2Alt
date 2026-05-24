# Custom Icons

Add your own HUD icons to UI Info Suite 2 Alternative using Content Patcher. Icons appear alongside the built-in ones (luck, birthday, weather, etc.).

## Quick Start

1. Create a **20x20 pixel** icon image in your `assets/` folder.

![My amazing icon](../.github/assets/test-icon.png)

2. Load the texture and add your icon in `content.json`:

```json
{
  "Format": "2.8.0",
  "Changes": [
    {
      "Action": "Load",
      "Target": "Mods/YourModId/Icons",
      "FromFile": "assets/icons.png"
    },
    {
      "Action": "EditData",
      "Target": "Mods/DazUki.UIInfoSuite2Alt/CustomIcons",
      "Entries": {
        "YourModId.MyIcon": {
          "Texture": "Mods/YourModId/Icons",
          "SourceRect": { "X": 0, "Y": 0, "Width": 20, "Height": 20 },
          "HoverText": "{{i18n:my-icon-tooltip}}"
        }
      },
      "When": {
        "Day": "15",
        "Season": "Spring"
      }
    }
  ]
}
```

3. Add UIInfoSuite2Alt as an optional dependency in `manifest.json`:

```json
{
  "Dependencies": [
    { "UniqueID": "DazUki.UIInfoSuite2Alt", "IsRequired": false }
  ]
}
```

4. See your icon appear on Spring 15:

![My Custom Icon](../.github/assets/custom-icons-2.7.0.png)

If UI Info Suite 2 Alternative isn't installed, the patch is silently ignored.

## Fields

| Field | Required | Description |
|---|---|---|
| `Texture` | Yes | The asset name of a texture loaded via Content Patcher (e.g. `Mods/YourModId/Icons` from a `Load` patch). Not a raw file path. If the asset can't be loaded, the icon silently fails with a warning in the SMAPI log. |
| `SourceRect` | No | Region to draw from the texture. Default: `{ X: 0, Y: 0, Width: 20, Height: 20 }`. Use 20x20 for best result. |
| `HoverText` | No | Tooltip shown on hover. Supports `{{i18n:key}}` for translations. |

Icons should be **20x20 pixels**. The mod scales and centers them to match the built-in icons.

## When Icons Appear

Use `When` conditions like any other CP patch. The icon shows when the conditions are met and disappears when they're not.

### Icon that disappears after a condition is met

This shows an icon reminding the player about a quest until they receive a mail flag (e.g. from completing it). Once the flag is set, the icon disappears and won't come back:

```json
{
  "Action": "EditData",
  "Target": "Mods/DazUki.UIInfoSuite2Alt/CustomIcons",
  "UpdateRate": "OnTimeChange",
  "Entries": {
    "YourModId.QuestReminder": {
      "Texture": "Mods/YourModId/Icons",
      "SourceRect": { "X": 0, "Y": 0, "Width": 20, "Height": 20 },
      "HoverText": "You still need to complete the ritual!"
    }
  },
  "When": {
    "HasFlag": "YourModId.RitualStarted",
    "HasFlag |contains=YourModId.RitualComplete": false
  }
}
```

The icon appears once `YourModId.RitualStarted` is set and disappears when `YourModId.RitualComplete` is set.

_This assumes both flags are permanent. If either flag is later cleared (e.g. via `Game1.player.mailReceived.Remove(...)`), the icon will re-appear or re-hide based on current state._

Content Patcher patches also re-evaluate only at the start of each day by default. To have the icon appear or disappear mid-day, add `"UpdateRate": "OnTimeChange"`(shown on the example above) to the patch. Changes will then apply at the next 10-minute in-game tick.

Use `"OnLocationChange"` instead if the change should apply when the player warps.

## Multiple Icons

Add multiple entries in one patch, or use separate patches with different `When` conditions:

```json
{
  "Action": "EditData",
  "Target": "Mods/DazUki.UIInfoSuite2Alt/CustomIcons",
  "Entries": {
    "YourModId.MarketDay": {
      "Texture": "Mods/YourModId/Icons",
      "SourceRect": { "X": 0, "Y": 0, "Width": 20, "Height": 20 },
      "HoverText": "{{i18n:market-day}}"
    },
    "YourModId.Festival": {
      "Texture": "Mods/YourModId/Icons",
      "SourceRect": { "X": 20, "Y": 0, "Width": 20, "Height": 20 },
      "HoverText": "{{i18n:festival-reminder}}"
    }
  },
  "When": {
    "Season": "Fall"
  }
}
```

A maximum of **15 custom CP icons** can be shown at any given time. Any beyond that are not displayed.


## Multi-Line Hover Text

Use `\n` to add line breaks in your tooltip or `\n\n` for an empty line:

```json
{
  "YourModId.MyIcon": {
    "Texture": "Mods/YourModId/Icons",
    "HoverText": "Line one\nLine two\n\nLine three with empty line above"
  }
}
```

## Some Good-To-Know Stuff

- Prefix entry keys with your mod ID to avoid conflicts (e.g. `YourModId.IconName`).
- Players can toggle custom icons on/off in the mod settings(+GMCM).
- Custom icons share a single position in the icon order settings for now("Icon Order" > "Custom Icons").
