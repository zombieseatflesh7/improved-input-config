
using RainMeadow;
using System.Linq;

namespace ImprovedInput;

sealed class Compat
{
    public static bool meadow = false;

    public static void CheckMods()
    {
        meadow = ModManager.ActiveMods.Any(x => x.id == "henpemaz_rainmeadow");
    }

    private static bool _meadowBlockInput => ChatTextBox.blockInput;
    public static bool MeadowBlockInput => meadow ? _meadowBlockInput : false;
}
