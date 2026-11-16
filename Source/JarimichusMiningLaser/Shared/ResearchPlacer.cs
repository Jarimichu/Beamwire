using System.Linq;
using UnityEngine;
using Verse;

namespace JarimichusMiningLaser
{
    /// <summary>
    /// Places this mod's research project on the vanilla Main tab one column right of its rightmost prerequisite,
    /// moving down until the spot is free. Done at load time from the real tree, so it fits whatever layout the
    /// game version (and other mods) use.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ResearchPlacer
    {
        static ResearchPlacer()
        {
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("JML_MiningLaser");
            if (project?.prerequisites == null || project.prerequisites.Count == 0) return;
            ResearchProjectDef anchor = project.prerequisites.OrderByDescending(p => p.researchViewX).First();
            float x = anchor.researchViewX + 1f;
            float y = anchor.researchViewY;
            for (int i = 0; i < 40 && Occupied(project, x, y); i++)
            {
                y += 0.7f;
            }
            project.researchViewX = x;
            project.researchViewY = y;
        }

        private static bool Occupied(ResearchProjectDef self, float x, float y)
        {
            foreach (ResearchProjectDef other in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (other == self || other.tab != self.tab) continue;
                if (Mathf.Abs(other.researchViewX - x) < 0.5f && Mathf.Abs(other.researchViewY - y) < 0.5f) return true;
            }
            return false;
        }
    }
}
