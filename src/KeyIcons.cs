using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace StsVim;

// Replaces controller glyphs with keycaps showing the key to press. Real gamepads keep their glyphs.
[HarmonyPatch(typeof(NInputManager), nameof(NInputManager.GetHotkeyIcon))]
public static class KeyIcons
{
    const float DefaultSize = 64f;
    const float Padding = 0.35f;

    static readonly Dictionary<string, Texture2D> _cache = new();

    static readonly Dictionary<Key, string> ShortNames = new()
    {
        { Key.Escape, "Esc" },
        { Key.Space, "Spc" },
        { Key.Enter, "Ret" },
    };

    static void Postfix(NInputManager __instance, string hotkey, ref Texture2D? __result)
    {
        if (Input.GetConnectedJoypads().Count > 0)
        {
            return;
        }
        string? label = LabelFor(__instance, hotkey);
        if (label == null)
        {
            return;
        }
        float height = __result?.GetSize().Y ?? DefaultSize;
        __result = GetKeycap(__instance, label, height);
    }

    // Vim key if the mod maps this action, else the game's keyboard binding.
    static string? LabelFor(NInputManager manager, StringName action)
    {
        string? vim = FindKey(VimInput.Motions, action, false) ?? FindKey(VimInput.Singles, action, false) ?? FindKey(VimInput.ShiftSingles, action, true);
        if (vim != null)
        {
            return vim;
        }
        Key key = manager.GetShortcutKey(action);
        if (key == Key.None)
        {
            return null;
        }
        return KeyName(key, false);
    }

    static string? FindKey(Dictionary<Key, StringName> map, StringName action, bool shifted)
    {
        foreach (KeyValuePair<Key, StringName> pair in map)
        {
            if (pair.Value == action)
            {
                return KeyName(pair.Key, shifted);
            }
        }
        return null;
    }

    // Letters: lowercase, uppercase when shifted (vim style).
    static string KeyName(Key key, bool shifted)
    {
        if (ShortNames.TryGetValue(key, out string? name))
        {
            return name;
        }
        string text = OS.GetKeycodeString(key);
        return shifted ? text.ToUpperInvariant() : text.ToLowerInvariant();
    }

    static Texture2D GetKeycap(Node host, string label, float height)
    {
        string cacheKey = label + "@" + height;
        if (!_cache.TryGetValue(cacheKey, out Texture2D? texture))
        {
            texture = RenderKeycap(host, label, height);
            _cache[cacheKey] = texture;
        }
        return texture;
    }

    // Draws the keycap once into an offscreen viewport kept under the input manager.
    static Texture2D RenderKeycap(Node host, string label, float height)
    {
        Label text = new Label
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.AddThemeFontSizeOverride("font_size", (int)(height * 0.5f));
        text.AddThemeColorOverride("font_color", Colors.White);

        float width = Mathf.Max(height, text.GetMinimumSize().X + height * Padding * 2);
        Vector2I size = new Vector2I((int)width, (int)height);

        Panel cap = new Panel { Size = size };
        cap.AddThemeStyleboxOverride("panel", KeycapStyle(height));
        text.Size = size;
        cap.AddChild(text);

        SubViewport viewport = new SubViewport
        {
            Size = size,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        viewport.AddChild(cap);
        host.AddChild(viewport);
        return viewport.GetTexture();
    }

    static StyleBoxFlat KeycapStyle(float height)
    {
        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.12f, 0.9f),
            BorderColor = new Color(0.85f, 0.85f, 0.8f),
        };
        style.SetBorderWidthAll((int)Mathf.Max(2, height * 0.06f));
        style.SetCornerRadiusAll((int)(height * 0.2f));
        return style;
    }
}
