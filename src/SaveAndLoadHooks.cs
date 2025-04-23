using HarmonyLib;
using MonoMod.RuntimeDetour;
using Rewired;
using Rewired.Utils.Classes.Data;
using System;
using System.Collections.Generic;
using SerializedObject = Rewired.Utils.Classes.Data.SerializedObject;
using RewiredUserDataStore = Rewired.Data.RewiredUserDataStore;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Text.RegularExpressions;

namespace ImprovedInput
{
    internal static class SaveAndLoadHooks
    {
        internal static void InitHooks()
        {
            // removing modded actions from rewired saves
            new Hook(AccessTools.Method(typeof(ControllerMap), "OicAnvgQDQDAcJDLLshYCWbWDWcNA"), ControllerMap_Serialize_Hook);
            new Hook(AccessTools.Method(typeof(ControllerMapWithAxes), "OicAnvgQDQDAcJDLLshYCWbWDWcNA"), ControllerMapWithAxes_Serialize_Hook);
            new Hook(AccessTools.PropertyGetter(typeof(RewiredUserDataStore), "allActionIds"), RewiredUserDataStore_AllActionIds_Hook);

            // saving and loading modded actions
            new Hook(AccessTools.Method(typeof(RewiredUserDataStore), "SaveControllerMap", new Type[2] { typeof(Rewired.Player), typeof(ControllerMap)}), RewiredUserDataStore_SaveControllerMap_Hook);
            new Hook(AccessTools.Method(typeof(RewiredUserDataStore), "LoadControllerMap", new Type[4] { typeof(Rewired.Player), typeof(ControllerIdentifier), typeof(int), typeof(int) }), RewiredUserDataStore_LoadControllerMap_Hook);

            // saving and loading mouse keybinds
            On.Options.ControlSetup.ToString += ControlSetup_ToString;
            On.Options.ControlSetup.FromString += ControlSetup_FromString;
            IL.Options.ControlSetup.ToString += ControlSetup_ToStringIL;
        }

        // Preventing Rewired from saving modded actions
        private static void ControllerMap_Serialize_Hook(Action<ControllerMap, SerializedObject> orig, ControllerMap self, SerializedObject s)
        {   
            orig(self, s);
            
            AList<ActionElementMap> buttonMaps = self.tvTaSlZaJJsaHhTxcEdxOMRqgyDi;
            List<object> serializedButtonMaps = s.GetEntry("buttonMaps").value as List<object>;
            int length = self.buttonMapCount;
            int offset = 0;
            for (int i = 0; i < length; i++)
            {
                if (buttonMaps[i] == null)
                    offset--;
                else if (buttonMaps[i].actionId > Plugin.highestVanillaActionId)
                {
                    serializedButtonMaps.RemoveAt(i + offset);
                    offset--;
                }
            }
        }

        private static void ControllerMapWithAxes_Serialize_Hook(Action<ControllerMapWithAxes, SerializedObject> orig, ControllerMapWithAxes self, SerializedObject s)
        {
            orig(self, s);

            IList<ActionElementMap> axisMaps = self.xqQatlGNnpDMdyYOrXYgsrbRwfuj;
            List<object> serializedAxisMaps = s.GetEntry("axisMaps").value as List<object>;
            int length = self.axisMapCount;
            int offset = 0;
            for (int i = 0; i < length; i++)
            {
                if (axisMaps[i] == null)
                    offset--;
                else if (axisMaps[i].actionId > Plugin.highestVanillaActionId)
                {
                    serializedAxisMaps.RemoveAt(i + offset);
                    offset--;
                }
            }
        }

        // Exact copy, except removing all the modded input actions
        private static List<int> RewiredUserDataStore_AllActionIds_Hook(Func<RewiredUserDataStore, List<int>> orig, RewiredUserDataStore self)
        {
            if (self.__allActionIds != null)
            {
                return self.__allActionIds;
            }
            List<int> list = new List<int>();
            IList<InputAction> actions = Plugin.vanillaInputActions;
            for (int i = 0; i < actions.Count; i++)
            {
                list.Add(actions[i].id);
            }
            self.__allActionIds = list;
            return list;
        }

        static readonly string version = "1.3";

        // remember unloaded keybinds for each controller
        static readonly Dictionary<string, string[]> controllerUnknownUnboundKeys = new(); // TODO implement this later
        static readonly Dictionary<string, string[][]> controllerUnknownBoundKeys = new();
        
        private static void RewiredUserDataStore_SaveControllerMap_Hook(Action<RewiredUserDataStore, Rewired.Player, ControllerMap> orig, RewiredUserDataStore self, Rewired.Player player, ControllerMap map)
        {
            orig(self, player, map);
            if (!self.IsEnabled || map.categoryId != 0 || map.controllerType == ControllerType.Mouse)
                return;

            List<string> unbound = new List<string>();
            List<string[]> bound = new List<string[]>();
                
            // counting loaded keybinds
            List<PlayerKeybind> keybinds = PlayerKeybind.keybinds;
            for (int k = 10; k < keybinds.Count; k++)
            {
                ActionElementMap aem = map.GetFirstElementMapWithAction(keybinds[k].gameAction);
                if (aem == null)
                {
                    unbound.Add(keybinds[k].Id);
                }
                else
                {
                    string[] bind = new string[4];
                    bind[0] = keybinds[k].Id;
                    bind[1] = aem.elementIdentifierId.ToString();
                    bind[2] = aem.elementType.ToString();
                    bind[3] = aem.axisRange.ToString();
                    bound.Add(bind);
                }
            }

            // counting unloaded keybinds
            string key = "iic|" + self.GetControllerMapPlayerPrefsKey(player, map.controller.identifier, map.categoryId, map.layoutId, 2);
            if (controllerUnknownBoundKeys.ContainsKey(key))
                foreach (string[] mapping in controllerUnknownBoundKeys[key])
                    bound.Add(mapping);
            
            // serializing
            List<string> list = new List<string>();
            list.Add(version); // version
            list.Add(unbound.Count.ToString());
            list.Add(bound.Count.ToString());

            foreach (string id in unbound)
                list.Add(id);
            foreach (string[] mapping in bound)
                list.AddRange(mapping);
            
            // saving
            string value = string.Join("|", list);
            Plugin.Logger.LogInfo("saving controller: " + key + "\ndata: " + value);
            PlayerPrefs.SetString(key, value);
        }

        private static ControllerMap RewiredUserDataStore_LoadControllerMap_Hook(Func<RewiredUserDataStore, Rewired.Player, ControllerIdentifier, int, int, ControllerMap> orig, RewiredUserDataStore self, Rewired.Player player, ControllerIdentifier controllerIdentifier, int categoryId, int layoutId)
        {
            ControllerMap map = orig(self, player, controllerIdentifier, categoryId, layoutId);
            if (map == null || categoryId != 0 || controllerIdentifier.controllerType == ControllerType.Mouse)
                return map;

            string key = "iic|" + self.GetControllerMapPlayerPrefsKey(player, controllerIdentifier, categoryId, layoutId, 2);
            if (!PlayerPrefs.HasKey(key))
                return map;

            try
            {
                string value = PlayerPrefs.GetString(key);
                string[] data = value.Split('|');
                if (data[0] != version)
                    return map;

                Plugin.Logger.LogInfo("loading controller: " + key + "\ndata: " + value);

                // reading unbound keybinds
                int offset = 3;
                int numUnbound = int.Parse(data[1]);

                // TODO Implement unbound keybind checking when default keybinds are reimplemented

                // reading bound keybinds
                offset += numUnbound;
                int numBound = int.Parse(data[2]);
                List<string[]> unknownBounds = new List<string[]>();

                for (int i = offset; i < offset + numBound * 4; i += 4)
                {
                    PlayerKeybind keybind = PlayerKeybind.Get(data[i]);
                    if (keybind == null)
                    {
                        unknownBounds.Add(new string[] { data[i], data[i + 1], data[i + 2], data[i + 3] });
                        continue;
                    }

                    int elementId = int.Parse(data[i + 1]);
                    ControllerElementType type;
                    AxisRange range;

                    if (!Enum.TryParse(data[i + 2], out type) || !Enum.TryParse(data[i + 3], out range) || keybind.gameAction == -1)
                    {
                        Plugin.Logger.LogError("Failed to parse controller data for " + key);
                        continue;
                    }

                    map.CreateElementMap(keybind.gameAction, Pole.Positive, elementId, type, range, false);
                }

                controllerUnknownBoundKeys.Remove(key);
                controllerUnknownBoundKeys.Add(key, unknownBounds.ToArray());
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError("Failed to parse controller data for " + key);
                Debug.LogException(e);
            }
            return map;
        }

        private static void ControlSetup_ToStringIL(ILContext il)
        {
            ILCursor c = new(il);
            try
            {
                c.GotoNext(
                    i => i.MatchLdcI4(5)
                    );
                int start = c.Index;
                c.GotoNext(
                    i => i.MatchStloc(0)
                    );
                int end = c.Index + 1;
                c.Goto(start);
                c.RemoveRange(end - start);

                c.Emit(OpCodes.Ldloc_0);
                c.Emit(OpCodes.Ldloc_2);
                c.EmitDelegate(ControlSetup_WriteMouseButtonMapping);
                c.Emit(OpCodes.Stloc_0);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Failed to patch ControlSetup_ToString");
                Plugin.Logger.LogError(ex.Message);
            }
        }

        private static List<string> moddedMouseMappings;
        private static Dictionary<Options.ControlSetup, string[]> unknownMouseButtonMappings = new Dictionary<Options.ControlSetup, string[]>();

        private static string ControlSetup_WriteMouseButtonMapping(string text, KeyValuePair<string, int> mapping)
        {
            string[] key = mapping.Key.Split(',');
            int actionId = int.Parse(key[0]);
            bool axisPositive = key[1] == "1";
            PlayerKeybind keybind = PlayerKeybind.Get(actionId, axisPositive);
            if (keybind == null)
                return text;

            if (keybind.IsVanilla)
                return text + mapping.Key + ":" + mapping.Value + "<ctrlB>";

            moddedMouseMappings.Add(keybind.Id + "|" + mapping.Value);
            return text;
        }

        private static string ControlSetup_ToString(On.Options.ControlSetup.orig_ToString orig, Options.ControlSetup self)
        {
            moddedMouseMappings = new List<string>();

            string text = orig(self);

            if (unknownMouseButtonMappings.ContainsKey(self))
                moddedMouseMappings.AddRange(unknownMouseButtonMappings[self]);
            if (moddedMouseMappings.Count > 0)
            {
                Plugin.Logger.LogInfo("saving iic mouse maps for player: " + self.player.name);
                string iicMouseMap = "iic:mousemaps" + "<ctrlB>" + version + "<ctrlB>" + string.Join("<ctrlB>", moddedMouseMappings);
                Plugin.Logger.LogInfo("maps: " + iicMouseMap);
                text += "<ctrlA>" + iicMouseMap;
            }
            return text;
        }

        private static void ControlSetup_FromString(On.Options.ControlSetup.orig_FromString orig, Options.ControlSetup self, string s)
        {
            orig(self, s);

            if (self.unrecognizedControlAttrs == null || self.unrecognizedControlAttrs.Length == 0)
                return;

            string iicMapString = null;
            List<string> attributes = self.unrecognizedControlAttrs.ToList();

            for (int i = attributes.Count - 1; i >= 0; i--)
                if (attributes[i].StartsWith("iic:mousemaps"))
                {
                    if (iicMapString == null)
                        iicMapString = attributes[i];
                    attributes.RemoveAt(i);
                }
            if (iicMapString == null)
                return;

            self.unrecognizedControlAttrs = attributes.ToArray();
            if (self.unrecognizedControlAttrs.Length == 0)
                self.unrecognizedControlAttrs = null;

            try
            {
                Plugin.Logger.LogInfo("loading iic mouse maps for player: " + self.player.name);
                Plugin.Logger.LogInfo("maps: " + iicMapString);
                string[] map = Regex.Split(iicMapString, "<ctrlB>");
                if (map[1] != version)
                    return;

                List<string> unknowns = new List<string>();
                for (int i = 2; i < map.Length; i++)
                {
                    string[] mapping = map[i].Split('|');
                    PlayerKeybind keybind = PlayerKeybind.Get(mapping[0]);
                    if (keybind != null)
                    {
                        string key = keybind.gameAction + "," + (keybind.axisPositive ? "1" : "0");
                        self.mouseButtonMappings[key] = int.Parse(mapping[1]);
                    }
                    else
                    {
                        unknowns.Add(map[i]);
                    }
                }

                unknownMouseButtonMappings.Remove(self);
                if (unknowns.Count > 0)
                    unknownMouseButtonMappings.Add(self, unknowns.ToArray());
            }
            catch (Exception ex) 
            {
                Plugin.Logger.LogError("Failed to parse control setup for player: " + self.player.name);
                Plugin.Logger.LogError(ex.Message);
            }
        }
    }
}
