using Rewired;
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
    public static readonly PlayerKeybind Pause = Register("vanilla:pause", "Vanilla", "Pause", 5);

    /// <summary>The GRAB button.</summary>
    public static readonly PlayerKeybind Grab = Register("vanilla:grab", "Vanilla", "Grab", 3);
    /// <summary>The JUMP button.</summary>
    public static readonly PlayerKeybind Jump = Register("vanilla:jump", "Vanilla", "Jump", 0, 8);
    /// <summary>The THROW button.</summary>
    public static readonly PlayerKeybind Throw = Register("vanilla:throw", "Vanilla", "Throw", 4, 9);
    /// <summary>The SPECIAL button.</summary>
    public static readonly PlayerKeybind Special = Register("vanilla:special", "Vanilla", "Special", 34);
    /// <summary>The MAP button.</summary>
    public static readonly PlayerKeybind Map = Register("vanilla:map", "Vanilla", "Map", 11);

    /// <summary>The UP button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Up = Register("vanilla:up", "Vanilla", "Up", 2, 7);
    /// <summary>The LEFT button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Left = Register("vanilla:left", "Vanilla", "Left", 1, 6, true);
    /// <summary>The DOWN button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Down = Register("vanilla:down", "Vanilla", "Down", 2, 7, true);
    /// <summary>The RIGHT button. Unconfigurable for controllers.</summary>
    public static readonly PlayerKeybind Right = Register("vanilla:right", "Vanilla", "Right", 1, 6);

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
    //[Obsolete("Keycodes are no longer supported. They will be ignored")]
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset)
    {
        return Register(id, mod, name);
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
//[Obsolete("Keycodes are no longer supported. They will be ignored")]
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset, KeyCode xboxPreset)
    {
        return Register(id, mod, name);
    }

    /// <summary>
    /// Registers a new keybind.
    /// </summary>
    /// <param name="id">The unique ID for the keybind.</param>
    /// <param name="mod">The display name of the mod that registered this keybind.</param>
    /// <param name="name">A short name to show in the Input Settings screen.</param>
    /// <returns>A new <see cref="PlayerKeybind"/> to be used like <c>player.JustPressed(keybind)</c>.</returns>
    /// <exception cref="ArgumentException">The <paramref name="id"/> is invalid or already taken.</exception>
    public static PlayerKeybind Register(string id, string mod, string name)
    {
        Validate(id, mod, name);
        return Register(id, mod, name, moddedActionIdCounter++);
    }

    private static PlayerKeybind Register(string id, string mod, string name, int gameAction, int uiAction = -1, bool invert = false)
    {
        PlayerKeybind k = new(id, mod, name, gameAction, uiAction, invert);
        if (Plugin.Logger != null)
            Plugin.Logger.LogInfo("Registered PlayerKeybind: \"" + id + "\"");
        SaveAndLoadHooks.LateLoadKeybindData(k);
        return k;
    }

    private static void Validate(string id, string mod, string name)
    {
        ArgumentException e = null;
        if (string.IsNullOrWhiteSpace(id) || id.Contains("<optA>") || id.Contains("<optB>")) {
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

    private PlayerKeybind(string id, string mod, string name, int gameAction, int uiAction, bool invert)
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
        KeyboardPreset = KeyCode.None;
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

    /// <summary>The default value for keyboards.</summary>
    [Obsolete]
    public KeyCode KeyboardPreset { get; } = KeyCode.None;
    /// <summary>The default value for PlayStation, Switch Pro, and other controllers.</summary>
    [Obsolete]
    public KeyCode GamepadPreset { get; } = KeyCode.None;
    /// <summary>The default value for Xbox controllers.</summary>
    [Obsolete]
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

    /// <summary>True if the binding for <paramref name="playerNumber"/> is set.</summary>
    //public bool Bound(int playerNumber) => Controls[playerNumber].gameControlMap.ContainsAction(gameAction);
    public bool Bound(int playerNumber)
    {
        Options.ControlSetup controlSetup = Controls[playerNumber];
        if (controlSetup == null)
            return false;

        if (controlSetup.gameControlMap.ContainsAction(gameAction))
            return true;

        string key = gameAction + "," + (axisPositive ? "1" : "0");
        if (controlSetup.mouseButtonMappings.ContainsKey(key))
            return controlSetup.mouseButtonMappings[key] > -1;

        return false;
    }

    /// <summary>True if the binding for <paramref name="playerNumber"/> is not set.</summary>
    public bool Unbound(int playerNumber) => !Bound(playerNumber);

    /// <summary>Checks if this keybind is from a mod.</summary>
    internal bool IsModded => gameAction > highestVanillaActionId;

    /// <summary>Checks if this keybind is from vanilla.</summary>
    internal bool IsVanilla => !IsModded;

    /// <summary>
    /// The current keycode configured for the given <paramref name="playerNumber"/> on keyboard.
    /// Returns None if the player is on controller or uses mouse for input.
    /// </summary>
    [Obsolete("IIC 2 does not use KeyCodes. This method is unreliable")]
    public KeyCode Keyboard(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        if (this == Pause) playerNumber = 0;
        var cs = RWCustom.Custom.rainWorld.options.controls[playerNumber];
        if (cs.gamePad)
            return KeyCode.None;
        return cs.KeyCodeFromAction(gameAction, 0);
    }

    /// <summary>
    /// The current keycode configured for the given <paramref name="playerNumber"/> on a controller.
    /// Returns None if the player is on keyboard or using axis inputs.
    /// </summary>
    [Obsolete("IIC 2 does not use KeyCodes. This method is unreliable")]
    public KeyCode Gamepad(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        if (this == Pause) playerNumber = 0;
        var cs = RWCustom.Custom.rainWorld.options.controls[playerNumber];
        if (!cs.gamePad)
            return KeyCode.None;
        return cs.KeyCodeFromAction(gameAction, 0);
    }

    /// <summary>The current recognized keycode for the given <paramref name="playerNumber"/>.</summary>
    [Obsolete("IIC 2 does not use KeyCodes. This method is unreliable")]
    public KeyCode CurrentBinding(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        if (this == Pause) playerNumber = 0;
        
        return RWCustom.Custom.rainWorld.options.controls[playerNumber].KeyCodeFromAction(gameAction, 0);
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
}
