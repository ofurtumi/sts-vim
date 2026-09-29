using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace StsVim;

// Translates vim keys into the game's own input actions. Unmapped keys fall through to the game.
[HarmonyPatch(typeof(NInputManager), "ProcessShortcutKeyInput")]
public static class VimInput
{
    // Repeat while held.
    internal static readonly Dictionary<Key, StringName> Motions = new()
    {
        { Key.H, MegaInput.left },
        { Key.J, MegaInput.down },
        { Key.K, MegaInput.up },
        { Key.L, MegaInput.right },
    };

    internal static readonly Dictionary<Key, StringName> Singles = new()
    {
        { Key.Space, MegaInput.select },
        { Key.P, MegaInput.topPanel },
        { Key.V, MegaInput.peek },
    };

    internal static readonly Dictionary<Key, StringName> ShiftSingles = new()
    {
        { Key.H, MegaInput.viewDeckAndTabLeft },
        { Key.L, MegaInput.viewExhaustPileAndTabRight },
    };

    // Second key after `g`.
    static readonly Dictionary<Key, StringName> GKeys = new()
    {
        { Key.M, MegaInput.viewMap },
        { Key.D, MegaInput.viewDeckAndTabLeft },
        { Key.A, MegaInput.viewDrawPile },
        { Key.S, MegaInput.viewDiscardPile },
        { Key.X, MegaInput.viewExhaustPileAndTabRight },
    };

    static bool _gPending;
    static readonly HashSet<Key> _swallowRelease = new();
    static readonly Dictionary<Key, StringName> _held = new();

    // Returns false to skip the game's own handling of this key.
    static bool Prefix(NInputManager __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey key || !CanSend())
        {
            return true;
        }
        if (key.Pressed && IsVimKey(key) && EnterFocusMode(__instance.ControllerManager))
        {
            _swallowRelease.Add(key.Keycode);
            return false;
        }
        if (!key.Pressed)
        {
            return HandleRelease(key);
        }
        if (_gPending)
        {
            return HandleG(key);
        }
        return HandlePress(key);
    }

    static bool HandlePress(InputEventKey key)
    {
        if (IsEscape(key))
        {
            if (!key.Echo)
            {
                Escape();
            }
            _swallowRelease.Add(key.Keycode);
            return false;
        }
        if (HasModifier(key))
        {
            return true;
        }
        if (key.ShiftPressed)
        {
            return TryHold(ShiftSingles, key);
        }
        if (key.Keycode == Key.G)
        {
            _gPending = !key.Echo;
            return false;
        }
        if (Motions.TryGetValue(key.Keycode, out StringName? motion))
        {
            // Echo = key held: send a fresh press so focus keeps moving.
            if (key.Echo)
            {
                Send(motion, false);
            }
            Hold(key.Keycode, motion);
            return false;
        }
        return TryHold(Singles, key);
    }

    static bool HandleG(InputEventKey key)
    {
        if (key.Echo || key.Keycode is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta)
        {
            return false;
        }
        _gPending = false;
        if (GKeys.TryGetValue(key.Keycode, out StringName? action))
        {
            Tap(action);
        }
        _swallowRelease.Add(key.Keycode);
        return false;
    }

    static bool HandleRelease(InputEventKey key)
    {
        if (_swallowRelease.Remove(key.Keycode) || key.Keycode == Key.G)
        {
            return false;
        }
        if (!_held.Remove(key.Keycode, out StringName? action))
        {
            return true;
        }
        Send(action, false);
        return false;
    }

    // Forwards a press; its release is matched later via _held.
    static bool TryHold(Dictionary<Key, StringName> map, InputEventKey key)
    {
        if (!map.TryGetValue(key.Keycode, out StringName? action))
        {
            return true;
        }
        if (!key.Echo)
        {
            Hold(key.Keycode, action);
        }
        return false;
    }

    static void Hold(Key keycode, StringName action)
    {
        _held[keycode] = action;
        Send(action, true);
    }

    static bool IsEscape(InputEventKey key)
    {
        if (key.Keycode == Key.Escape)
        {
            return !HasModifier(key);
        }
        return key.CtrlPressed && key.Keycode == Key.Bracketleft;
    }

    // The game binds Esc to both cancel and pause; pause only if nothing on screen takes cancel.
    static void Escape()
    {
        Tap(MegaInput.cancel);
        if (!HasHotkeyBinding(MegaInput.cancel))
        {
            Tap(MegaInput.pauseAndBack);
        }
    }

    static bool HasHotkeyBinding(StringName action)
    {
        Dictionary<StringName, List<Action>>? bindings = Traverse.Create(NHotkeyManager.Instance).Field("_hotkeyPressedBindings").GetValue<Dictionary<StringName, List<Action>>>();
        return bindings != null && bindings.ContainsKey(action);
    }

    static bool IsVimKey(InputEventKey key)
    {
        if (HasModifier(key))
        {
            return false;
        }
        if (key.ShiftPressed)
        {
            return ShiftSingles.ContainsKey(key.Keycode);
        }
        return Motions.ContainsKey(key.Keycode) || Singles.ContainsKey(key.Keycode);
    }

    // Focus navigation only runs in controller mode; enter it the way a gamepad press would.
    // Returns true if the mode was switched (this key press only focuses the default control).
    static bool EnterFocusMode(NControllerManager manager)
    {
        if (manager.IsUsingController)
        {
            return false;
        }
        InputEventAction fake = new InputEventAction { Action = Controller.dPadNorth, Pressed = true };
        Traverse.Create(manager).Method("CheckForControllerInput", new[] { typeof(InputEvent) }, new object[] { fake }).GetValue();
        return manager.IsUsingController;
    }

    static bool HasModifier(InputEventKey key) => key.CtrlPressed || key.AltPressed || key.MetaPressed;

    // Same guard the game applies before emitting shortcut actions.
    static bool CanSend() => NGame.Instance != null && !NGame.Instance.Transition.InTransition && NGame.IsGameFocusedWindow();

    static void Tap(StringName action)
    {
        Send(action, true);
        Send(action, false);
    }

    static void Send(StringName action, bool pressed)
    {
        InputEventAction e = new InputEventAction { Action = action, Pressed = pressed };
        Input.ParseInputEvent(e);
    }
}
