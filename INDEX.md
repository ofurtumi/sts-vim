# Index

Build + install: `dotnet build -c Release` (copies to `SlayTheSpire2.app/Contents/MacOS/mods/StsVim`). Uninstall: delete that folder.
Build also fills `workshop/content`. Upload: [ModUploader](https://github.com/megacrit/sts2-mod-uploader/releases) (osx-arm64) `upload -w workshop`. Needs `workshop/image.png` (<1MB). `visibility` starts `private`.

## src/VimInput.cs — Harmony prefix on `NInputManager.ProcessShortcutKeyInput`

| Function | Purpose |
|---|---|
| `Prefix` | Entry point. Routes key events; returns `false` to skip the game's handling. |
| `HandlePress` | Resolves a key press: `Esc`/`Ctrl-[`, shift keys, `g` prefix, motions, singles. |
| `HandleG` | Resolves the key after `g` (`gm gd ga gs gx`). |
| `HandleRelease` | Sends the release for a held key, or swallows it. |
| `TryHold` | Presses the action mapped to a key in a map, if any. |
| `Hold` | Records key→action and sends the press. |
| `IsEscape` | True for `Esc` or `Ctrl-[`. |
| `Escape` | Sends cancel; sends pause only if no hotkey takes cancel. |
| `HasHotkeyBinding` | True if any on-screen button has a hotkey for the action. |
| `IsVimKey` | True if the key is a mod-handled single key (motion, single, shift). |
| `EnterFocusMode` | Switches mouse mode → controller/focus mode via the game's own path. |
| `HasModifier` | True if Ctrl/Alt/Meta held. |
| `CanSend` | Game's own guard: not in transition, window focused. |
| `Tap` | Press + release an action. |
| `Send` | Emits one `InputEventAction`. |

## src/KeyIcons.cs — Harmony postfix on `NInputManager.GetHotkeyIcon`

| Function | Purpose |
|---|---|
| `Postfix` | Swaps controller glyph for a keycap unless a gamepad is connected. |
| `LabelFor` | Key label for an action: vim key, else game keyboard binding. |
| `FindKey` | Reverse lookup of an action in a `VimInput` keymap. |
| `KeyName` | Short display name for a `Key` (`Esc`, `Spc`, `h`, `H`). |
| `GetKeycap` | Cached keycap texture per label + height. |
| `RenderKeycap` | Draws a keycap into an offscreen `SubViewport`, returns its texture. |
| `KeycapStyle` | Rounded dark box with light border. |

## Keymap

| Key | Action |
|---|---|
| `h j k l` | move focus (repeats when held) |
| `Space` / `Enter` | select |
| `Esc` / `Ctrl-[` | cancel / pause |
| `e` | end turn (game default) |
| `1`–`0` | select card (game default) |
| `H` / `L` | tab left / right |
| `p` | top panel (potions, relics) |
| `v` | peek |
| `gm gd ga gs gx` | map, deck, draw, discard, exhaust |
