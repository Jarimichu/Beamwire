using HarmonyLib;
using Verse;

namespace Beamwire
{
    /// <summary>
    /// Re-traces beams straight away when a wall (or other impassable building) is built or removed in a
    /// beam's path, rather than waiting for the next periodic re-trace.
    /// </summary>
    [HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup))]
    public static class Building_SpawnSetup_Patch
    {
        public static void Postfix(Building __instance, Map map)
        {
            if (map != null && __instance.def.passability == Traversability.Impassable)
            {
                map.GetComponent<MapComponent_BeamNetwork>()?.NotifyBuildingChanged(__instance.Position);
            }
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.DeSpawn))]
    public static class Building_DeSpawn_Patch
    {
        public static void Prefix(Building __instance)
        {
            Map map = __instance.Map;
            if (map != null && __instance.def.passability == Traversability.Impassable)
            {
                map.GetComponent<MapComponent_BeamNetwork>()?.NotifyBuildingChanged(__instance.Position);
            }
        }
    }
}
