using Rewired;
using System.Collections.Generic;
using UnityEngine;

namespace ImprovedInput;

/// <summary>
/// Extends the functionality of the vanilla input system.
/// </summary>
public static class CustomInputExt
{
    internal const int maxMaxPlayers = 16;
    internal static int maxPlayers = RainWorld.PlayerObjectBodyColors.Length;
    internal static int historyLength = 10;
    internal static bool historyLocked = false;

    /// <summary>
    /// Determines how many ticks of input are stored for <see cref="InputHistory(Player)"/> and <see cref="RawInputHistory(Player)"/>.
    /// </summary>
    /// <remarks>This value starts at 10 and can only be increased. Set it when your mod is being enabled. Avoid setting this to anything extremely high.</remarks>
    public static int HistoryLength
    {
        get => historyLength;
        set
        {
            if (historyLocked)
            {
                throw new System.InvalidOperationException("History length cannot be modified after the game has started.");
            }
            historyLength = Mathf.Max(historyLength, value);
        }
    }

    /// <summary>
    /// The number of players who could possibly be receiving input at the moment.
    /// </summary>
    /// <remarks>This value starts at <see cref="RainWorld.PlayerObjectBodyColors"/>.Length.</remarks>
    public static int MaxPlayers
    {
        get => maxPlayers;
        set
        {
            if (value < 4)
            {
                throw new System.InvalidOperationException("Max player count can't be less than four.");
            }
            if (value > maxMaxPlayers)
            {
                throw new System.InvalidOperationException($"Max player count can't be more than {maxMaxPlayers}.");
            }
            maxPlayers = value;
        }
    }

    /// <summary>Returns true if a given control setup uses a keyboard.</summary>
    public static bool UsingKeyboard(this Options.ControlSetup setup) => UsingKeyboard(setup.index);
    /// <summary>Returns true if a given control setup uses a gamepad.</summary>
    public static bool UsingGamepad(this Options.ControlSetup setup) => UsingGamepad(setup.index);

    /// <summary>Returns true if a given player is using a keyboard.</summary>
    public static bool UsingKeyboard(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= MaxPlayers)
        {
            throw new System.ArgumentOutOfRangeException(nameof(playerNumber), $"Player number {playerNumber} is not valid.");
        }
        return RWCustom.Custom.rainWorld.options.controls[playerNumber].controlPreference == Options.ControlSetup.ControlToUse.KEYBOARD;
    }
    /// <summary>Returns true if a given player is using a gamepad.</summary>
    public static bool UsingGamepad(int playerNumber)
    {
        if (playerNumber < 0 || playerNumber >= MaxPlayers)
        {
            throw new System.ArgumentOutOfRangeException(nameof(playerNumber), $"Player number {playerNumber} is not valid.");
        }
        return RWCustom.Custom.rainWorld.options.controls[playerNumber].controlPreference == Options.ControlSetup.ControlToUse.SPECIFIC_GAMEPAD;
    }

    /// <summary>
    /// Checks if <paramref name="key"/> is bound for <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is bound.</returns>
    public static bool IsKeyBound(this Player player, PlayerKeybind key)
    {
        if (player.AI != null || player.playerState.playerNumber < 0 || player.playerState.playerNumber >= MaxPlayers)
        {
            return false;
        }
        return key.Bound(player.playerState.playerNumber);
    }
    /// <summary>
    /// Checks if <paramref name="key"/> is unbound for <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is unbound.</returns>
    public static bool IsKeyUnbound(this Player player, PlayerKeybind key) => !player.IsKeyBound(key);

    /// <summary>
    /// Checks if <paramref name="key"/> is being pressed by <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is down.</returns>
    public static bool IsPressed(this Player player, PlayerKeybind key)
    {
        return player.Input()[key];
    }

    /// <summary>
    /// Checks if <paramref name="key"/> is just now being pressed by <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is down, but was not down last tick.</returns>
    public static bool JustPressed(this Player player, PlayerKeybind key)
    {
        var history = player.InputHistory();
        return history[0][key] && !history[1][key];
    }

    /// <summary>Gets all custom input for <paramref name="player"/> this tick.</summary>
    /// <remarks>Ignores keypresses made while unconscious, using the map, or being controlled. To use those keypresses, see <see cref="RawInput(Player)"/>.</remarks>
    public static CustomInput Input(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).input[0];
    }

    /// <summary>Gets all custom input for <paramref name="player"/> this tick, including suppressed inputs. Avoid modifying anything here.</summary>
    public static CustomInput RawInput(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).rawInput[0];
    }

    /// <summary>Gets all custom input for <paramref name="player"/> in recent history.</summary>
    /// <remarks>Ignores keypresses made while unconscious, using the map, or being controlled. To use those keypresses, see <see cref="RawInputHistory(Player)"/>.</remarks>
    public static CustomInput[] InputHistory(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).input;
    }

    /// <summary>Gets all custom input for <paramref name="player"/> in recent history, including suppressed inputs. Avoid modifying anything here.</summary>
    public static CustomInput[] RawInputHistory(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).rawInput;
    }

    internal static int GetMouseMapping(this Options.ControlSetup controlSetup, int actionID, bool axisPositive)
    {
        int mouse = -1;
        string key = actionID + "," + (axisPositive ? "1" : "0");
        if (!controlSetup.mouseButtonMappings.TryGetValue(key, out mouse))
            mouse = -1;
        return mouse;
    }

    internal static void SetMouseMapping(this Options.ControlSetup controlSetup, int actionID, bool axisPositive, int mouseIndex)
    {
        string key = actionID + "," + (axisPositive ? "1" : "0");
        controlSetup.mouseButtonMappings[key] = mouseIndex;
    }

    internal static ActionElementMap IicGetActionElement(this Options.ControlSetup controlSetup, int actionId, int categoryId, bool axisPositive)
    {
        ControllerMap cmap = categoryId == 0 ? controlSetup.gameControlMap : controlSetup.uiControlMap;

        if (cmap == null)
            return null;

        IEnumerable<ActionElementMap> enumerable = cmap.ElementMapsWithAction(actionId);
        ActionElementMap actionElementMap = null;
        foreach (ActionElementMap item in enumerable)
        {
            if (item.axisContribution == Pole.Positive && axisPositive)
            {
                actionElementMap = item;
            }
            else if (item.axisContribution == Pole.Negative && !axisPositive)
            {
                actionElementMap = item;
            }
        }

        return actionElementMap;
    }
}
