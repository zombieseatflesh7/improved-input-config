using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Menu;
using Mono.Cecil.Cil;
using Rewired;
using RWCustom;
using HarmonyLib;

namespace ImprovedInput
{
    internal static class InputMenuHooks
    {
        private static Options.ControlSetup[] Controls => RWCustom.Custom.rainWorld.options.controls;
        private static InputSelectButton[] keybindButtons;

        internal static void InitHooks()
        {
            // Input Settings Menu
            IL.Menu.InputOptionsMenu.ctor += AddCustomButtonsIL;
            On.Menu.InputOptionsMenu.ctor += FixVanillaButtons;
            IL.Menu.InputOptionsMenu.Update += FixUpdateIL;
            On.Menu.InputOptionsMenu.SetCurrentlySelectedOfSeries += FixSelection;
            On.Menu.InputOptionsMenu.RefreshInputGreyOut += RefreshButtons;
            On.Menu.InputOptionsMenu.UpdateInfoText += InputOptionsMenu_UpdateInfoText;
            On.Menu.InputOptionsMenu.Singal += LoadPreset;

            // Input testing
            On.Menu.InputTesterHolder.InputTester.ctor += InputTester_ctor;
            On.Menu.InputTesterHolder.InputTester.Update += InputTester_Update;
            On.Menu.InputTesterHolder.InputTester.UpdateTestButtons += InputTester_UpdateTestButtons;
            On.Menu.InputTesterHolder.InputTester.GetToPos += InputTester_GetToPos;
            On.Menu.InputOptionsMenu.PlayerButton.Update += PlayerButton_Update;
            On.Menu.InputTesterHolder.Back.Update += Back_Update;
        }

        // Helper Functions

        // Copied and modified from InputOptionsMenu.InputSelectButton
        // helper function for buttons, basically unchanged, only moved to a different class.
        internal static bool IsInputDeviceCurrentlyAvailable(Options.ControlSetup cs, bool gamepad)
        {
            if (cs.GetControlPreference() == Options.ControlSetup.ControlToUse.ANY)
                return false;
            if (!gamepad && !cs.player.controllers.hasKeyboard)
                return false;
            if (gamepad && (cs.GetActiveController() == null || cs.GetActiveController().type != ControllerType.Joystick))
                return false;

            return true;
        }

        // Copied and modified from InputOptionsMenu.InputSelectButton
        // Controls the button text. Changed to use PlayerKeybinds.
        internal static string ButtonText(int player, PlayerKeybind keybind, bool inputTesterDisplay, out Color? color)
        {
            color = null;

            Options.ControlSetup cs = Controls[player];
            bool gamepad = cs.gamePad;
            bool valid = cs.GetControlPreference() == Options.ControlSetup.ControlToUse.ANY && cs.GetActiveController() != null && inputTesterDisplay; // unassigned on the input test screen?
            if (!IsInputDeviceCurrentlyAvailable(cs, gamepad) && !valid)
                return "-";

            string key = keybind.gameAction + "," + (keybind.axisPositive ? "1" : "0");
            if (!gamepad && cs.mouseButtonMappings.ContainsKey(key) && cs.mouseButtonMappings[key] >= 0 && cs.mouseButtonMappings[key] < ReInput.controllers.Mouse.Buttons.Count)
            {
                int num = cs.mouseButtonMappings[key];
                return num switch { 0 => "Left Click", 1 => "Right Click", 2 => "Middle Click", _ => "Mouse " + (num + 1) };
            }

            ActionElementMap actionElementMap = cs.IicGetActionElement(keybind.gameAction, 0, keybind.axisPositive);
            string buttonName = "None";
            if (actionElementMap != null)
                buttonName = actionElementMap.elementIdentifierName;

            // Getting button colors
            Options.ControlSetup.Preset ty = Custom.rainWorld.options.controls[player].GetActivePreset();
            if (ty == Options.ControlSetup.Preset.XBox)
                color = buttonName switch
                {
                    "A" => new Color32(60, 219, 78, 255),
                    "B" => new Color32(208, 66, 66, 255),
                    "X" => new Color32(64, 204, 208, 255),
                    "Y" => new Color32(236, 219, 51, 255),
                    _ => null
                };
            else if (ty == Options.ControlSetup.Preset.PS4DualShock || ty == Options.ControlSetup.Preset.PS5DualSense)
                color = buttonName switch
                {
                    "Cross" => new Color32(137, 164, 244, 255),
                    "Circle" => new Color32(244, 102, 100, 255),
                    "Square" => new Color32(237, 137, 202, 255),
                    "Triangle" => new Color32(40, 237, 174, 255),
                    _ => null
                };

            return buttonName;
        }

        // InputOptionMenu HOOKS

        private static void AddCustomButtonsIL(ILContext il)
        {
            ILCursor c = new(il);

            c.GotoNext(i => i.MatchNewobj<InputTesterHolder>());
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate(AddCustomButtons);
        }
        
        private static void AddCustomButtons(InputOptionsMenu self)
        {
            // --- Keybind buttons ---
            var keybinds = PlayerKeybind.GuiKeybinds();
            keybindButtons = new InputSelectButton[keybinds.Count];

            int columns = 1 + Mathf.CeilToInt((keybinds.Count - 9) / 10f); // 10 per row
            if (columns > 4)
            {
                throw new InvalidOperationException("How are there possibly more than 30 modded keybinds at one time?");
            }

            var s = self.pages[0].subObjects;
            var c = columns > 1; // compact mode
            var columnWidth = c ? 120 : 200;
            var o = columns == 1
                ? new Vector2(960, 642)
                : new Vector2(columns > 2 ? 1136 : 1024, 642);
            var y = 0f;

            // Start at 10, after all vanilla keybinds
            for (int i = PlayerKeybind.vanillaKeybindCount; i < keybinds.Count; i++)
            {
                PlayerKeybind keybind = keybinds[i];
                AddKeybindButton(self.pages[0], i, keybind, c, new Vector2(o.x, o.y - y));
                y += 40;
                if (y >= 40 * 10)
                {
                    y = 0;
                    o.x -= columnWidth;
                }
            }

            if (y != 0)
            {
                y = 0;
                o.x -= columnWidth;
            }

            MenuLabel GroupLabel(string text, Vector2 pos)
            {
                MenuLabel label = new(self, self.pages[0], text, pos + new Vector2(c ? 15 : 0, 0), Vector2.zero, false);
                label.label.anchorX = c ? 1f : 0.5f;
                label.label.anchorY = 1;
                label.label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
                return label;
            }

            // Add vanilla buttons
            s.Add(GroupLabel("PLAYER ONE", o + new Vector2(15, 30)));
            AddKeybindButton(self.pages[0], 0, PlayerKeybind.Pause, c, o - new Vector2(0, y += 20));

            s.Add(GroupLabel("MOVEMENT", o - new Vector2(0, y += 45) + new Vector2(15, 30)));
            AddKeybindButton(self.pages[0], 1, PlayerKeybind.Up, c, o - new Vector2(0, y += 20));
            AddKeybindButton(self.pages[0], 2, PlayerKeybind.Left, c, o - new Vector2(0, y += 40));
            AddKeybindButton(self.pages[0], 3, PlayerKeybind.Down, c, o - new Vector2(0, y += 40));
            AddKeybindButton(self.pages[0], 4, PlayerKeybind.Right, c, o - new Vector2(0, y += 40));

            s.Add(GroupLabel("VANILLA", o - new Vector2(0, y += 45) + new Vector2(15, 30)));
            AddKeybindButton(self.pages[0], 5, PlayerKeybind.Grab, c, o - new Vector2(0, y += 20));
            AddKeybindButton(self.pages[0], 6, PlayerKeybind.Jump, c, o - new Vector2(0, y += 40));
            AddKeybindButton(self.pages[0], 7, PlayerKeybind.Throw, c, o - new Vector2(0, y += 40));
            AddKeybindButton(self.pages[0], 8, PlayerKeybind.Special, c, o - new Vector2(0, y += 40));
            AddKeybindButton(self.pages[0], 9, PlayerKeybind.Map, c, o - new Vector2(0, y += 40));

            // --- Preset button ---
            self.pages[0].subObjects.Add(new SimpleButton(self, self.pages[0], self.Translate("PRESET"), "BIC CUSTOM PRESET", new(self.testButton.pos.x, 140), new(110, 30)));
        }

        // helper function that insures the buttons are stored in the correct order.
        private static void AddKeybindButton(Page page, int index, PlayerKeybind pk, bool compact, Vector2 pos)
        {
            InputSelectButton button = new InputSelectButton(page, index, pk, compact, pos);
            keybindButtons[index] = button;
            page.subObjects.Add(button);
        }

        private static void FixVanillaButtons(On.Menu.InputOptionsMenu.orig_ctor orig, InputOptionsMenu self, ProcessManager manager)
        {
            orig(self, manager);

            foreach (var setup in manager.rainWorld.options.controls)
            {
                if (setup.controlPreference == Options.ControlSetup.ControlToUse.ANY)
                {
                    setup.UpdateControlPreference(Options.ControlSetup.ControlToUse.KEYBOARD, false);
                }
            }

            // Remove old buttons
            string keyboard = self.Translate("KEYBOARD");
            string gamepad = self.Translate("GAMEPAD");

            for (int i = self.pages[0].subObjects.Count - 1; i >= 0; i--)
            {
                MenuObject sub = self.pages[0].subObjects[i];

                if (sub == self.deviceButtons[0]
                    || sub == self.keyboardDefaultsButton
                    || sub == self.gamepadDefaultsButton
                    || sub is InputOptionsMenu.InputSelectButton
                    || sub is MenuLabel label && (label.text == keyboard || label.text == gamepad || self.inputLabels.Contains(label)))
                {
                    self.pages[0].RemoveSubObject(sub);
                    sub.RemoveSprites();
                }
            }

            // Prevent self.gamepadDefaultsButton from being selected
            self.xInvCheck.nextSelectable[1] = null;

            // Move keyboard button up
            self.deviceButtons[1].pos.y += 90;
            self.deviceButtons[1].lastPos.y += 90;

            // Update player arrow positions after moving keyboard button
            foreach (InputOptionsMenu.PlayerButton plrButton in self.playerButtons)
            {
                plrButton.pointPos = plrButton.IdealPointHeight();
                plrButton.lastPointPos = plrButton.pointPos;
            }

            self.keyBoardKeysButtons = new InputOptionsMenu.InputSelectButton[0];
            self.gamePadButtonButtons = new InputOptionsMenu.InputSelectButton[0];

            // Remove device side-labels
            foreach (var btn in self.deviceButtons)
            {
                btn.displayName = "";
                btn.menuLabel.text = "";
            }

            // Move "invert x/y" checkboxes
            int found = 0;
            foreach (CheckBox checkBox in self.pages[0].subObjects.OfType<CheckBox>())
            {
                if (checkBox.IDString == "XINV")
                {
                    checkBox.pos = new Vector2(450, 50);
                    found++;
                }
                else if (checkBox.IDString == "YINV")
                {
                    checkBox.pos = new Vector2(450, 80);
                    found++;
                }
                if (found == 2) break;
            }
        }

        private static void FixUpdateIL(ILContext il)
        {
            ILCursor c = new(il);
            try
            {
                // selectedObject = ((settingInput.Value.x == KEYBOARD_ASSIGNMENT) ? keyBoardKeysButtons[settingInput.Value.y] : gamePadButtonButtons[settingInput.Value.y]);
                // fixes an index out of range error when accessing the vanilla InputSelectButtons. They don't exist, so we replace it with our own fuction.
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchLdarg(0),
                    i => i.MatchLdflda(AccessTools.Field(typeof(InputOptionsMenu), "settingInput"))
                    );
                int start = c.Index;
                c.GotoNext(
                    i => i.MatchStfld(AccessTools.Field(typeof(Menu.Menu), "selectedObject"))
                    );
                int end = c.Index + 1;
                c.Goto(start);
                c.RemoveRange(end - start);

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(FixSelectedObject);

                // mouse button assignment 
                c.GotoNext(
                    i => i.MatchLdloc(3),
                    i => i.MatchLdcI4(0)
                    );
                start = c.Index + 3;
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchCall(AccessTools.Method(typeof(InputOptionsMenu), "StopInputAssignment"))
                    );
                end = c.Index;
                c.Goto(start);
                c.RemoveRange(end - start);

                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_3);
                c.EmitDelegate(FixMouseMapping);

                // input mapper assignment
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchLdsfld(AccessTools.Field(typeof(SoundID), "MENU_Button_Successfully_Assigned"))
                    );
                start = c.Index;
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchCall(AccessTools.Method(typeof(InputOptionsMenu), "StopInputAssignment"))
                    );
                end = c.Index;
                c.Goto(start);
                c.RemoveRange(end - start);

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(FixInputMapping);
            }
            catch (KeyNotFoundException ex) 
            {
                Plugin.Logger.LogError("Failed to patch InputOptionsMenu.Update");
                Debug.LogError(ex.Message);
            }
        }
         
        // part 2: Replaced access with the appropriate modded InputSelectButton
        private static void FixSelectedObject(InputOptionsMenu self)
        {
            self.selectedObject = keybindButtons[self.settingInput.Value.y];
        }

        private static void FixMouseMapping(InputOptionsMenu self, int mouseIndex)
        {
            InputMapper.Context[] mc = self.mappingContexts;
            Options options = self.manager.rainWorld.options;
            Options.ControlSetup cs = options.controls[options.playerToSetInputFor];

            // unbinding if reassigning same value
            string key = mc[0].actionId + "," + ((mc[0].actionRange == AxisRange.Positive) ? "1" : "0");
            if (cs.mouseButtonMappings.ContainsKey(key) && cs.mouseButtonMappings[key] == mouseIndex)
            {
                mouseIndex = -1;
                self.PlaySound(SoundID.MENU_Checkbox_Uncheck);
            }
            else
                self.PlaySound(SoundID.MENU_Button_Successfully_Assigned);

            // vanilla code
            for (int l = 0; l < mc.Length; l++)
            {
                if (mc[l] != null)
                {
                    Pole axisContribution = ((mc[l].actionRange != AxisRange.Positive) ? Pole.Negative : Pole.Positive);
                    string text = ((mc[l].actionRange == AxisRange.Positive) ? "1" : "0");
                    if (mc[l].actionElementMapToReplace != null)
                        mc[l].controllerMap.DeleteElementMap(mc[l].actionElementMapToReplace.id);
                    cs.mouseButtonMappings[mc[l].actionId + "," + text] = mouseIndex;
                }
            }
        }

        internal static int remappingElementId;
        private static void FixInputMapping(InputOptionsMenu self)
        {
            InputMapper.Context[] mc = self.mappingContexts;
            Options options = self.manager.rainWorld.options;
            Options.ControlSetup cs = options.controls[options.playerToSetInputFor];

            for (int m = 0; m < mc.Length; m++)
            {
                if (mc[m] == null)
                    continue;

                ActionElementMap aem = cs.IicGetActionElement(mc[m].actionId, m, (mc[m].actionRange == AxisRange.Positive) ? true : false);
                if (aem != null && remappingElementId == aem.elementIdentifierId)
                {
                    cs.gameControlMap.DeleteElementMap(aem.id);
                    if (m == 0)
                        self.PlaySound(SoundID.MENU_Checkbox_Uncheck);
                }
                else
                {
                    if (!cs.gamePad)
                    {
                        string key = mc[m].actionId + "," + ((mc[m].actionRange == AxisRange.Positive) ? "1" : "0");
                        cs.mouseButtonMappings.Remove(key);
                    }
                    if (m == 0)
                        self.PlaySound(SoundID.MENU_Button_Successfully_Assigned);
                }
            }
        }

        private static void FixSelection(On.Menu.InputOptionsMenu.orig_SetCurrentlySelectedOfSeries orig, InputOptionsMenu self, string series, int to)
        {
            if (series == "DeviceButtons" && to == 0)
                to = 1;

            orig(self, series, to);
        }

        private static void RefreshButtons(On.Menu.InputOptionsMenu.orig_RefreshInputGreyOut orig, InputOptionsMenu self)
        {
            orig(self);
            foreach (InputSelectButton button in keybindButtons)
            {
                button.RefreshKeyDisplay();
            }
        }

        private static string InputOptionsMenu_UpdateInfoText(On.Menu.InputOptionsMenu.orig_UpdateInfoText orig, InputOptionsMenu self)
        {
            if (self.selectedObject is SimpleButton button && button.signalText == "BIC CUSTOM PRESET")
            {
                return Regex.Replace(self.Translate("Assign player <X> to the default controls"), "<X>", (self.manager.rainWorld.options.playerToSetInputFor + 1).ToString());
            }
            return self.selectedObject is InputSelectButton t ? t.HoverText() : orig(self);
        }

        // handle loading preset. TODO rewrite
        private static void LoadPreset(On.Menu.InputOptionsMenu.orig_Singal orig, InputOptionsMenu self, MenuObject sender, string message)
        {
            if (message == "BIC CUSTOM PRESET")
            {
                if (self.CurrentControlSetup.gamePad)
                    message = "GAMEPAD_DEFAULTS";
                else
                    message = "KEYBOARD_DEFAULTS";

                // load preset keys
                SaveAndLoadHooks.ClearUnloadedKeys(self.CurrentControlSetup);
                orig(self, sender, message); 
                SaveAndLoadHooks.LoadPresetMappings(self.CurrentControlSetup);

                // TODO revisit this code
                foreach (InputSelectButton b in keybindButtons)
                {
                    if ((!b.PlayerOneOnly || self.CurrentControlSetup.index == 0)
                        && !(b.MovementKey && b.Gamepad))
                    {
                        b.Flash();
                    }
                }


            }
            else
                orig(self, sender, message);
        }

        // InputTester HOOKS

        private static void InputTester_ctor(On.Menu.InputTesterHolder.InputTester.orig_ctor orig, InputTesterHolder.InputTester self, Menu.Menu menu, MenuObject owner, int playerIndex)
        {
            orig(self, menu, owner, playerIndex);

            // Remove buttons. Leave the first four ( the arrow keys ).
            foreach (var testButton in self.testButtons)
            {
                if (testButton.buttonIndex is 6 or 7 or 8 or 9) continue;

                self.RemoveSubObject(testButton);
                testButton.RemoveSprites();
            }

            var keybinds = PlayerKeybind.GuiKeybinds();

            // Added keybinds
            float x = 120 + (menu.CurrLang == InGameTranslator.LanguageID.French || menu.CurrLang == InGameTranslator.LanguageID.German ? 30 : 0);
            int row = 0;
            int btn = 4;
            Array.Resize(ref self.testButtons, Mathf.Max(self.testButtons.Length, keybinds.Count - 1)); // exclude pause button
            foreach (PlayerKeybind keybind in keybinds)
            {
                if (keybind.index is 0 or 6 or 7 or 8 or 9)
                {
                    continue;
                }

                self.subObjects.Add(self.testButtons[btn++] = new(menu, self, new Vector2(x, 45 - row * 30), null, 0, menu.Translate(keybind.Name), keybind.index, playerIndex));

                row += 1;
                if (row >= CustomInputExt.MaxPlayers)
                {
                    row = 0;
                    x += 150;
                }
            }
        }

        private static void InputTester_Update(On.Menu.InputTesterHolder.InputTester.orig_Update orig, InputTesterHolder.InputTester self)
        {
            orig(self);

            foreach (var btn in self.testButtons)
            {
                if (btn.buttonIndex >= PlayerKeybind.keybinds.Count)
                    return;

                PlayerKeybind kb = PlayerKeybind.keybinds[btn.buttonIndex];

                if (btn.buttonIndex is not 6 and not 7 and not 8 and not 9)
                {
                    btn.pressed = kb.CheckRawPressed(self.playerIndex);
                }
                btn.playerAssignedToAnything = self.playerAssignedToAnything && (kb.Bound(self.playerIndex) || Controls[self.playerIndex].GetMouseMapping(kb.gameAction, kb.axisPositive) != -1);
            }
        }

        private static void InputTester_UpdateTestButtons(On.Menu.InputTesterHolder.InputTester.orig_UpdateTestButtons orig, InputTesterHolder.InputTester self)
        {
            foreach (var btn in self.testButtons)
            {
                if (btn.menuLabel == null)
                {
                    continue;
                }

                string text = ButtonText(self.playerIndex, PlayerKeybind.keybinds[btn.buttonIndex], true, out _);

                if (text == "None" || text == "< N / A >" || text == "-" || text == "???")
                {
                    btn.menuLabel.text = btn.labelText;
                }
                else
                {
                    btn.menuLabel.text = $"{btn.labelText} ({text})";
                }
            }
        }

        private static Vector2 InputTester_GetToPos(On.Menu.InputTesterHolder.InputTester.orig_GetToPos orig, InputTesterHolder.InputTester self)
        {
            orig(self);

            var menu = self.menu as InputOptionsMenu;
            var fin = Custom.SCurve(1f - self.inPlace, 0.6f);
            var a = new Vector2(self.playerIndex % 2 == 0 ? 200f : 260f, menu.playerButtons[self.playerIndex].size.y / 2f) + new Vector2(1500f * fin, 0);

            return menu.playerButtons[self.playerIndex].pos + a - new Vector2((1366 - self.menu.manager.rainWorld.options.ScreenSize.x) / 2, 0);
        }

        private static void PlayerButton_Update(On.Menu.InputOptionsMenu.PlayerButton.orig_Update orig, InputOptionsMenu.PlayerButton self)
        {
            orig(self);
            self.pos = self.originalPos + new Vector2(0, 50 * (self.menu as InputOptionsMenu).inputTesterHolder.darkness);
        }

        private static void Back_Update(On.Menu.InputTesterHolder.Back.orig_Update orig, InputTesterHolder.Back self)
        {
            orig(self);
            if (self.holder.active)
                self.holdButton.held = RWInput.CheckPauseButton(0);
        }
    }
}
