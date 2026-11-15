using System.Collections.Generic;
using Beamwire.Core;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>While placing an emitter or relay, shows where its beam will go until something blocks it.</summary>
    public class PlaceWorker_BeamPreview : PlaceWorker
    {
        private static readonly List<IntVec3> Cells = new List<IntVec3>();

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            Dir8 dir = (Dir8)(rot.AsInt * 2);
            if (thing is ThingWithComps twc && twc.GetComp<CompBeamNode>() is CompBeamNode node)
            {
                dir = (Dir8)(node.Orientation / 2);
            }

            var grid = new MapBeamGrid(map, BeamwireMod.Settings);
            float range = BeamwireMod.Settings.maxRange;
            float travelled = 0f;
            GridPos p = MapBeamGrid.ToGrid(center);
            Cells.Clear();
            while (true)
            {
                p = p.Step(dir);
                travelled += dir.StepLength();
                if (travelled > range || !grid.InBounds(p) || grid.IsBlocked(p)) break;
                Cells.Add(MapBeamGrid.ToCell(p));
            }
            GenDraw.DrawFieldEdges(Cells, BeamRenderer.BeamColor);
        }
    }
}
