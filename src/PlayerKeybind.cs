﻿using Rewired;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace ImprovedInput;

/// <summary>
/// A simple per-player keybind.
/// </summary>
public sealed class PlayerKeybind
{
    private static Options.ControlSetup[] Controls => RWCustom.Custom.rainWorld.options.controls;

    /// <summary>Every keybind currently registered, including vanilla and modded keybinds.</summary>
    public static IReadOnlyList<PlayerKeybind> Keybinds() => keybindsReadonly;

    internal static readonly List<PlayerKeybind> keybinds = new();
    internal static readonly ReadOnlyCollection<PlayerKeybind> keybindsReadonly = new(keybinds);

    internal static List<PlayerKeybind> GuiKeybinds()
    {
        List<PlayerKeybind> ret = new(keybinds);
        ret.RemoveAll(p => p.HideConfig);
        return ret;
    }

    /// <summary>
    /// Gets a keybind given its <paramref name="id"/>.
    /// </summary>
    /// <returns>The keybind, or <see langword="null"/> if none was found.</returns>
    public static PlayerKeybind Get(string id) => keybinds.FirstOrDefault(k => k.Id == id);

    internal static PlayerKeybind Get(int actionId, bool axisPositive)
    {
        if (actionId == -1) return null;

        foreach (PlayerKeybind keybind in keybinds)
            if ((keybind.gameAction == actionId || keybind.uiAction == actionId) && keybind.axisPositive == axisPositive)
                return keybind;

        return null;
    }

    internal const int highestVanillaActionId = 34;
    internal const int vanillaKeybindCount = 10;
    private static int moddedActionIdCounter = highestVanillaActionId + 1; // counter for modded keybinds

    // Don't move these. The indices matter for the input menu. These will always be called before any other mods, because calling "Register" will load this class, which initializes these.

    /// <summary>The PAUSE button. Usually ignored for anyone but the first player.</summary>
    public static readonly PlayerKeybind Pause = new("vanilla:pause", "Vanilla", "Pause", 5);

    /// <summary>The GRAB button.</summary>
    public static readonly PlayerKeybind Grab = new("vanilla:grab", "Vanilla", "Grab", 3);
    /// <summary>The JUMP button.</summary>
    public static readonly PlayerKeybind Jump = new("vanilla:jump", "Vanilla", "Jump", 0, 8);
    /// <summary>The THROW button.</summary>
    public static readonly PlayerKeybind Throw = new("vanilla:throw", "Vanilla", "Throw", 4, 9);
    /// <summary>The SPECIAL button.</summary>
    public static readonly PlayerKeybind Special = new("vanilla:special", "Vanilla", "Special", 34);
    /// <summary>The MAP button.</summary>
    public static readonly PlayerKeybind Map = new("vanilla:map", "Vanilla", "Map", 11);

    /// <summary>The UP button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Up = new("vanilla:up", "Vanilla", "Up", 2, 7);
    /// <summary>The LEFT button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Left = new("vanilla:left", "Vanilla", "Left", 1, 6, true);
    /// <summary>The DOWN button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Down = new("vanilla:down", "Vanilla", "Down", 2, 7, true);
    /// <summary>The RIGHT button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Right = new("vanilla:right", "Vanilla", "Right", 1, 6);

    /// <summary>
    /// Registers a new keybind.
    /// </summary>
    /// <param name="id">The unique ID for the keybind.</param>
    /// <param name="mod">The display name of the mod that registered this keybind.</param>
    /// <param name="name">A short name to show in the Input Settings screen.</param>
    /// <param name="keyboardPreset">The default value for keyboards.</param>
    /// <param name="gamepadPreset">The default value for controllers.</param>
    /// <returns>A new <see cref="PlayerKeybind"/> to be used like <c>player.JustPressed(keybind)</c>.</returns>
    /// <exception cref="ArgumentException">The <paramref name="id"/> is invalid or already taken.</exception>
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset)
    {
        Validate(id, mod, name);
        PlayerKeybind k = new(id, mod, name, moddedActionIdCounter++, -1, false, keyboardPreset, gamepadPreset, gamepadPreset);
        SaveAndLoadHooks.LateLoadKeybindData(k);
        return k;
    }

    /// <summary>
    /// Registers a new keybind.
    /// </summary>
    /// <param name="id">The unique ID for the keybind.</param>
    /// <param name="mod">The display name of the mod that registered this keybind.</param>
    /// <param name="name">A short name to show in the Input Settings screen.</param>
    /// <param name="keyboardPreset">The default value for keyboards.</param>
    /// <param name="gamepadPreset">The default value for PlayStation, Switch Pro, and other controllers.</param>
    /// <param name="xboxPreset">The default value for Xbox controllers.</param>
    /// <returns>A new <see cref="PlayerKeybind"/> to be used like <c>player.JustPressed(keybind)</c>.</returns>
    /// <exception cref="ArgumentException">The <paramref name="id"/> is invalid or already taken.</exception>
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset, KeyCode xboxPreset)
    {
        Validate(id, mod, name);
        PlayerKeybind k = new(id, mod, name, moddedActionIdCounter++, -1, false, keyboardPreset, gamepadPreset, xboxPreset);
        SaveAndLoadHooks.LateLoadKeybindData(k);
        return k;
    }

    /// <summary>
    /// Registers a new keybind.
    /// </summary>
    /// <param name="id">The unique ID for the keybind.</param>
    /// <param name="mod">The display name of the mod that registered this keybind.</param>
    /// <param name="name">A short name to show in the Input Settings screen.</param>
    /// <returns>A new <see cref="PlayerKeybind"/> to be used like <c>player.JustPressed(keybind)</c>.</returns>
    /// <exception cref="ArgumentException">The <paramref name="id"/> is invalid or already taken.</exception>
    private static PlayerKeybind Register(string id, string mod, string name)
    {
        Validate(id, mod, name);
        PlayerKeybind k = new(id, mod, name, moddedActionIdCounter++, -1, false);
        SaveAndLoadHooks.LateLoadKeybindData(k);
        return k;
    }

    private static void Validate(string id, string mod, string name)
    {
        ArgumentException e = null;
        if (string.IsNullOrWhiteSpace(id) || id.Contains("<optA>") || id.Contains("<optB>") || id.Contains("|")) {
            e = new ArgumentException($"The keybind id \"{id}\" is invalid.");
        }
        else if (string.IsNullOrWhiteSpace(mod)) {
            e = new ArgumentException($"The keybind mod \"{mod}\" is invalid.");
        }
        else if (string.IsNullOrWhiteSpace(name)) {
            e = new ArgumentException($"The keybind mod \"{name}\" is invalid.");
        }
        else if (keybinds.Any(k => k.Id == id)) {
            e = new ArgumentException($"A keybind with the id {id} has already been registered.");
        }
        if (e != null) {
            Debug.Log($"[ERROR] {e.Message}");
            throw e;
        }
    }

    private PlayerKeybind(string id, string mod, string name, int gameAction, int uiAction = -1, bool invert = false, KeyCode kbPreset = KeyCode.None, KeyCode gpPreset = KeyCode.None, KeyCode xbPreset = KeyCode.None)
    {
        index = keybinds.Count;
        keybinds.Add(this);

        Id = id;
        Mod = mod;
        Name = name;

        this.gameAction = gameAction;
        this.uiAction = uiAction;
        this.axisPositive = !invert;

        //Rewrite preset code
        KeyboardPreset = kbPreset;
        GamepadPreset = gpPreset;
        XboxPreset = xbPreset;
    }

    internal readonly int index;

    /// <summary>A unique ID.</summary>
    public string Id { get; }
    /// <summary>The display name of the mod that registered this keybind.</summary>
    public string Mod { get; }
    /// <summary>The display name of the keybind.</summary>
    public string Name { get; }

    internal readonly int gameAction;
    internal readonly int uiAction;
    internal readonly bool axisPositive;

    // TODO deprecate and replace this preset code

    /// <summary>The default value for keyboards.</summary>
    public KeyCode KeyboardPreset { get; } = KeyCode.None;
    /// <summary>The default value for PlayStation, Switch Pro, and other controllers.</summary>
    public KeyCode GamepadPreset { get; } = KeyCode.None;
    /// <summary>The default value for Xbox controllers.</summary>
    public KeyCode XboxPreset { get; } = KeyCode.None;

    /// <summary>A longer description to show at the bottom of the screen when configuring the keybind.</summary>
    public string Description { get; set; }

    /// <summary>If true, using the map suppresses the keybind.</summary>
    public bool MapSuppressed { get; set; } = true;
    /// <summary>If true, sleeping suppresses the keybind.</summary>
    public bool SleepSuppressed { get; set; } = true;
    /// <summary>If true, the keybind will not be configurable through the Input Settings screen.</summary>
    public bool HideConfig { get; set; } = false;
    /// <summary>If true, the conflict warning will be hidden when this key conflicts with the given key.</summary>
    /// <remarks>May be null.</remarks>
    public Func<PlayerKeybind, bool> HideConflict { get; set; }

    /// <summary>Checks if this keybind is from a mod.</summary>
    internal bool IsModded => gameAction > highestVanillaActionId;

    /// <summary>Checks if this keybind is from vanilla.</summary>
    internal bool IsVanilla => !IsModded;

    /// <summary>True if the binding for <paramref name="playerNumber"/> is set.</summary>
    public bool Bound(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(playerNumber));

        Options.ControlSetup cs = Controls[playerNumber];
        if (cs == null)
            return false;

        if (cs.gameControlMap.ContainsAction(gameAction))
            return true;

        if (cs.GetMouseMapping(gameAction, axisPositive) > -1)
            return true;

        return false;
    }

    /// <summary>True if the binding for <paramref name="playerNumber"/> is not set.</summary>
    public bool Unbound(int playerNumber) => !Bound(playerNumber);

    /// <summary>The name of the button currently bound for this <paramref name="playerNumber"/>. Returns "None" if unbound.</summary>
    /// <remarks>Added in IIC:E v2.0.3</remarks>
    public string CurrentBindingName(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(playerNumber));

        Options.ControlSetup cs = Controls[playerNumber];

        ActionElementMap actionElementMap = cs.IicGetActionElement(gameAction, 0, axisPositive);
        if (actionElementMap != null)
            return actionElementMap.elementIdentifierName;

        int mouseButton = cs.GetMouseMapping(gameAction, axisPositive);
        if (!cs.gamePad && mouseButton > -1)
            return mouseButton switch { 0 => "Left Click", 1 => "Right Click", 2 => "Middle Click", _ => "Mouse " + (mouseButton + 1) };

        return "None";
    }

    /// <summary>
    /// Checks if <see langword="this"/> for <paramref name="playerNumber"/> conflicts with <paramref name="other"/> for <paramref name="otherPlayerNumber"/>. This ignores <see cref="HideConflict"/>.
    /// </summary>
    public bool ConflictsWith(int playerNumber, PlayerKeybind other, int otherPlayerNumber = -1)
    {
        if (otherPlayerNumber == -1)
            otherPlayerNumber = playerNumber;

        if (playerNumber == otherPlayerNumber && this == other)
            return false;

        Options.ControlSetup[] controls = RWCustom.Custom.rainWorld.options.controls;
        if (controls[playerNumber].controlPreference != controls[otherPlayerNumber].controlPreference)
            return false;
        
        if (controls[playerNumber].UsingGamepad() && controls[otherPlayerNumber].UsingGamepad() && controls[playerNumber].gamePadNumber != controls[otherPlayerNumber].gamePadNumber)
            return false;

        ActionElementMap aem1 = controls[playerNumber].GetActionElement(gameAction, 0, axisPositive);
        int mouse1 = -1;
        if (!controls[playerNumber].gamePad)
            mouse1 = controls[playerNumber].GetMouseMapping(gameAction, axisPositive);
            
        if (aem1 == null && mouse1 == -1)
            return false;

        ActionElementMap aem2 = controls[otherPlayerNumber].GetActionElement(other.gameAction, 0, other.axisPositive);
        int mouse2 = -1;
        if (!controls[playerNumber].gamePad)
            mouse2 = controls[otherPlayerNumber].GetMouseMapping(other.gameAction, other.axisPositive);

        if (aem2 == null && mouse2 == -1)
            return false;

        return (mouse1 == mouse2 && mouse1 != -1) || aem1 != null && aem2 != null && aem1.CheckForAssignmentConflict(aem2);
    }

    internal bool VisiblyConflictsWith(int playerNumber, PlayerKeybind other, int otherPlayerNumber)
    {
        return ConflictsWith(playerNumber, other, otherPlayerNumber) && !(HideConflict?.Invoke(other) ?? false) && !(other.HideConflict?.Invoke(this) ?? false);
    }
    
    /// <summary>Checks if the key is currently being pressed by <paramref name="playerNumber"/>.</summary>
    public bool CheckRawPressed(int playerNumber)
    {
        return RWCustom.Custom.rainWorld.options.controls[playerNumber].GetButton(gameAction);
    }

    /// <summary>
    /// Returns <see cref="Id"/>.
    /// </summary>
    public override string ToString() => Id;

    // DEPRECATED STUFF

    /// <summary>
    /// The current keycode configured for the given <paramref name="playerNumber"/> on keyboard.
    /// </summary>
    [Obsolete("KeyCode returning methods are deprecated in IIC:E. Keyboard(int) only works if the player has selected keyboard input.")]
    public KeyCode Keyboard(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(playerNumber));

        if (this == Pause) playerNumber = 0;
        var cs = RWCustom.Custom.rainWorld.options.controls[playerNumber];
        if (cs.gamePad)
            return KeyCode.None;

        int mouseButton = cs.GetMouseMapping(gameAction, axisPositive);
        if (mouseButton > -1 && mouseButton <= 6)
            return mouseButton switch { 0 => KeyCode.Mouse0, 1 => KeyCode.Mouse1, 2 => KeyCode.Mouse2, 3 => KeyCode.Mouse3, 4 => KeyCode.Mouse4, 5 => KeyCode.Mouse5, 6 => KeyCode.Mouse6, _ => KeyCode.None};

        return cs.KeyCodeFromAction(gameAction, 0, axisPositive);
    }

    /// <summary>
    /// The current keycode configured for the given <paramref name="playerNumber"/> on a controller.
    /// </summary>
    [Obsolete("KeyCode returning methods are deprecated in IIC:E. Gamepad(int) will always return None.")]
    public KeyCode Gamepad(int playerNumber)
    {
        return KeyCode.None;
    }

    /// <summary>The current recognized keycode for the given <paramref name="playerNumber"/>.</summary>
    [Obsolete("KeyCode returning methods are deprecated in IIC:E. CurrentBinding(int) only works if the player has selected keyboard input.")]
    public KeyCode CurrentBinding(int playerNumber)
    {
        return Keyboard(playerNumber);
    }
}
