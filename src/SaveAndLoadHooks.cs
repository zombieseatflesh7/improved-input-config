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

            // late loading
            On.Options.Load += Options_Load;
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
                else if (buttonMaps[i].actionId > PlayerKeybind.highestVanillaActionId)
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
                else if (axisMaps[i].actionId > PlayerKeybind.highestVanillaActionId)
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

        static readonly string version = "1.5";

        private struct CmData
        {
            public CmData(ControllerMap cmap)
            {
                this.cmap = cmap;
                unbound = new List<string>();
                bound = new List<string[]>();
            }
            public ControllerMap cmap { get; }
            public List<string> unbound { get; } // unloaded, unbound keybindings
            public List<string[]> bound { get; } // unloaded, bound keybindings - {keybind id, elementID, elementType, axisRange}
        }

        private static readonly Dictionary<string, CmData> allControllerMapData = new();
        
        // saving to controller map
        private static void RewiredUserDataStore_SaveControllerMap_Hook(Action<RewiredUserDataStore, Rewired.Player, ControllerMap> orig, RewiredUserDataStore self, Rewired.Player player, ControllerMap cmap)
        {
            orig(self, player, cmap);
            if (!self.IsEnabled || cmap.categoryId != 0 || cmap.controllerType == ControllerType.Mouse)
                return;

            List<string> unbound = new List<string>();
            List<string[]> bound = new List<string[]>();
                
            // counting loaded keybinds
            List<PlayerKeybind> keybinds = PlayerKeybind.keybinds;
            for (int k = 10; k < keybinds.Count; k++)
            {
                ActionElementMap aem = cmap.GetFirstElementMapWithAction(keybinds[k].gameAction);
                if (aem == null)
                {
                    unbound.Add(keybinds[k].Id);
                }
                else
                {
                    string[] mapping = new string[4];
                    mapping[0] = keybinds[k].Id;
                    mapping[1] = aem.elementIdentifierId.ToString();
                    mapping[2] = aem.elementType.ToString();
                    mapping[3] = aem.axisRange.ToString();
                    bound.Add(mapping);
                }
            }

            // counting unloaded keybinds
            string key = "iic|" + self.GetControllerMapPlayerPrefsKey(player, cmap.controller.identifier, cmap.categoryId, cmap.layoutId, 2);
            CmData cmapData;
            if (allControllerMapData.TryGetValue(key, out cmapData))
            {
                unbound.AddRange(cmapData.unbound);
                bound.AddRange(cmapData.bound);
            }
                
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
            Plugin.Logger.LogDebug("saving iic controller map: " + key);
            Plugin.Logger.LogDebug("map: " + value);
            PlayerPrefs.SetString(key, value);
        }

        // loading controller map
        private static ControllerMap RewiredUserDataStore_LoadControllerMap_Hook(Func<RewiredUserDataStore, Rewired.Player, ControllerIdentifier, int, int, ControllerMap> orig, RewiredUserDataStore self, Rewired.Player player, ControllerIdentifier controllerIdentifier, int categoryId, int layoutId)
        {
            // orig / creating the map
            ControllerMap cmap = orig(self, player, controllerIdentifier, categoryId, layoutId);
            if (cmap == null || categoryId != 0 || controllerIdentifier.controllerType == ControllerType.Mouse || player.name == "PlayerTemplate")
                return cmap;

            string key = "iic|" + self.GetControllerMapPlayerPrefsKey(player, controllerIdentifier, categoryId, layoutId, 2);
            CmData cmapData = new CmData(cmap);
            allControllerMapData.Remove(key);
            allControllerMapData.Add(key, cmapData);

            if (!PlayerPrefs.HasKey(key))
                return cmap;

            // interpreting string data
            try
            {
                string value = PlayerPrefs.GetString(key);
                string[] data = value.Split('|');
                if (data[0] != version)
                {
                    PlayerPrefs.DeleteKey(key);
                    return cmap;
                }

                Plugin.Logger.LogDebug($"loading controller: " + key + "\ndata: " + value);

                // reading unbound keybinds
                int offset = 3;
                int numUnbound = int.Parse(data[1]);

                // TODO Implement unbound keybind checking when default keybinds are reimplemented
                    // if unbound and unloaded, add to unknowns
                    // if unbound and loaded, ignore

                // reading bound keybinds
                offset += numUnbound;
                int numBound = int.Parse(data[2]);

                for (int i = offset; i < offset + numBound * 4; i += 4)
                {
                    PlayerKeybind keybind = PlayerKeybind.Get(data[i]);
                    if (keybind == null) // unloaded
                    {
                        cmapData.bound.Add(new string[] { data[i], data[i + 1], data[i + 2], data[i + 3] });
                        continue;
                    }

                    AddControllerMapping(cmap, keybind, data[i + 1], data[i + 2], data[i + 3]);
                }

                // if not in list, and loaded, set default

            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Failed to parse controller data for " + key);
                Debug.LogException(ex);
            }
            return cmap;
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
                Debug.LogException(ex);
            }
        }

        private static List<string> moddedMouseMappings;
        private static Dictionary<Options.ControlSetup, List<string>> unknownMouseButtonMappings = new Dictionary<Options.ControlSetup, List<string>>();

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
            else if (mapping.Value != -1) // don't save unbound modded keys for mouse
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
                Plugin.Logger.LogDebug("saving iic mouse maps for player: " + self.player.name);
                string iicMouseMap = "iic:mousemaps" + "<ctrlB>" + version + "<ctrlB>" + string.Join("<ctrlB>", moddedMouseMappings);
                Plugin.Logger.LogDebug("map: " + iicMouseMap);
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
                string[] map = Regex.Split(iicMapString, "<ctrlB>");
                if (map[1] != version)
                    return;
                    
                Plugin.Logger.LogDebug("loading iic mouse map for player: " + self.player.name);
                Plugin.Logger.LogDebug("map: " + iicMapString);

                List<string> unknowns = new List<string>();
                for (int i = 2; i < map.Length; i++)
                {
                    string[] mapping = map[i].Split('|');
                    PlayerKeybind keybind = PlayerKeybind.Get(mapping[0]);
                    if (keybind != null)
                    {
                        AddMouseMapping(self, keybind, mapping[1]);
                    }
                    else
                    {
                        unknowns.Add(map[i]);
                    }
                }

                unknownMouseButtonMappings.Remove(self);
                if (unknowns.Count > 0)
                    unknownMouseButtonMappings.Add(self, unknowns);
            }
            catch (Exception ex) 
            {
                Plugin.Logger.LogError("Failed to parse control setup for player: " + self.player.name);
                Debug.LogException(ex);
            }
        }

        private static bool hasLoadedOptions = false;
        private static void Options_Load(On.Options.orig_Load orig, Options self)
        {
            orig(self);
            hasLoadedOptions = true;
        }

        internal static void LateLoadKeybindData(PlayerKeybind pk)
        {
            if (!hasLoadedOptions || pk.IsVanilla)
                return;

            Plugin.Logger.LogWarning($"PlayerKeybind \"{pk.Id}\" was registered late! It should be registered during OnEnable!");

            foreach (CmData cmapData in allControllerMapData.Values)
            {
                ControllerMap cmap = cmapData.cmap;
                if (cmap.playerId >= RWCustom.Custom.rainWorld.options.controls.Length || cmap.playerId < 0) // Rewired sometimes loads maps with invalid player ids
                {
                    Plugin.Logger.LogError("Wrong playerId: " + cmap.playerId);
                    continue;
                }

                bool usePreset = true;

                // load controller mappings
                for (int i = cmapData.unbound.Count - 1; i >= 0; i--)
                    if (cmapData.unbound[i] == pk.Id)
                    {
                        usePreset = false;
                        cmapData.unbound.RemoveAt(i);
                    }
                for (int i = cmapData.bound.Count - 1; i >= 0; i--)
                    if (cmapData.bound[i][0] == pk.Id)
                    {
                        usePreset = false;
                        string[] mapping = cmapData.bound[i];
                        AddControllerMapping(cmap, pk, mapping[1], mapping[2], mapping[3]);
                        cmapData.bound.RemoveAt(i);
                    }

                // load mouse mappings
                Options.ControlSetup cs = RWCustom.Custom.rainWorld.options.controls[cmap.playerId];
                if (cmapData.cmap.controllerType == ControllerType.Keyboard && unknownMouseButtonMappings.ContainsKey(cs))
                {
                    List<string> mappings = unknownMouseButtonMappings[cs];
                    for (int i = mappings.Count - 1; i >= 0; i--)
                    {
                        string[] mapping = mappings[i].Split('|');
                        if (mapping[0] == pk.Id)
                        {
                            usePreset = false;
                            AddMouseMapping(cs, pk, mapping[1]);
                            mappings.RemoveAt(i);
                        }
                    }
                }

                // assign preset
                if (usePreset)
                {
                    // TODO assign preset
                }
            }
        }

        private static void AddControllerMapping(ControllerMap map, PlayerKeybind pk, string elementIdString, string elementTypeString, string axisRangeString)
        {
            int elementId = int.Parse(elementIdString);
            ControllerElementType type;
            AxisRange range;

            if (!Enum.TryParse(elementTypeString, out type) || !Enum.TryParse(axisRangeString, out range))
                return;

            map.DeleteElementMapsWithAction(pk.gameAction);
            map.CreateElementMap(pk.gameAction, Pole.Positive, elementId, type, range, false);
        }
    
        private static void AddMouseMapping(Options.ControlSetup cs, PlayerKeybind pk, string mouseButton)
        {
            string key = pk.gameAction + "," + (pk.axisPositive ? "1" : "0");
            cs.mouseButtonMappings[key] = int.Parse(mouseButton);
        }
    
        internal static void ClearUnloadedKeys(Options.ControlSetup cs)
        {
            if (!cs.gamePad)
                unknownMouseButtonMappings.Remove(cs);

            RewiredUserDataStore ruds = (RewiredUserDataStore)ReInput.userDataStore;
            string key = "iic|" + ruds.GetControllerMapPlayerPrefsKey(cs.player, cs.recentController.identifier, 0, 0, 2);
            allControllerMapData.Remove(key);
        }
    }
}
