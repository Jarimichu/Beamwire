using System.Collections.Generic;
using Beamwire.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Beamwire
{
    /// <summary>
    /// Makes a building part of the beam network. A single comp class covers every node kind; the
    /// kind comes from <see cref="CompProperties_BeamNode.kind"/>.
    /// </summary>
    public class CompBeamNode : ThingComp
    {
        private int throughputIndex = -1;
        private int orientation = -1;

        private CompPowerTrader_BeamEmitter emitterPower;
        private CompBeamReceiver receiverPower;
        private CompFlickable flickable;
        private CompBreakdownable breakdownable;
        private CompOpticalDust dust;
        private Sustainer hum;

        // Latest solver results, for the inspect pane and the hum sound.
        public float LastWatts;
        public float LastDemand;

        public CompProperties_BeamNode Props => (CompProperties_BeamNode)props;

        public NodeKind Kind => Props.kind;

        /// <summary>Which of the 8 directions the node points (45° steps, 0 = North, clockwise).</summary>
        public Dir8 Facing => (Dir8)(orientation < 0 ? parent.Rotation.AsInt * 2 : orientation);

        /// <summary>The angle the top sprite is drawn at.</summary>
        public float AngleDegrees => (int)Facing * 45f;

        public float Efficiency => dust != null && BeamwireMod.Settings.dustEnabled ? dust.Efficiency : 1f;

        public CompBeamReceiver Receiver => receiverPower;

        public float MaxThroughput
        {
            get
            {
                var steps = Props.throughputSteps;
                if (steps == null || steps.Count == 0) return 0f;
                return steps[Mathf.Clamp(throughputIndex, 0, steps.Count - 1)];
            }
        }

        public bool Operational
        {
            get
            {
                if (flickable != null && !flickable.SwitchIsOn) return false;
                if (breakdownable != null && breakdownable.BrokenDown) return false;
                if (Kind == NodeKind.Emitter) return emitterPower != null && emitterPower.PowerOn;
                if (Kind == NodeKind.Receiver) return receiverPower != null && receiverPower.PowerNet != null;
                return true;
            }
        }

        private MapComponent_BeamNetwork Network => parent.Map?.GetComponent<MapComponent_BeamNetwork>();

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            emitterPower = parent.GetComp<CompPowerTrader_BeamEmitter>();
            receiverPower = parent.GetComp<CompBeamReceiver>();
            flickable = parent.GetComp<CompFlickable>();
            breakdownable = parent.GetComp<CompBreakdownable>();
            dust = parent.GetComp<CompOpticalDust>();
            if (throughputIndex < 0)
            {
                throughputIndex = Props.defaultThroughputIndex;
            }
            if (orientation < 0)
            {
                // Newly placed: start from the rotation chosen while placing it (R key).
                orientation = parent.Rotation.AsInt * 2;
            }
            Network?.Register(this);
        }

#if RW16
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            OnDeSpawn(map);
        }
#else
        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            OnDeSpawn(map);
        }
#endif

        private void OnDeSpawn(Map map)
        {
            map.GetComponent<MapComponent_BeamNetwork>()?.Deregister(this);
            EndHum();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref throughputIndex, "beamwireThroughputIndex", -1);
            Scribe_Values.Look(ref orientation, "beamwireOrientation", -1);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            // Switching on or off, breaking down and being repaired all change the beam paths.
            Network?.MarkDirty();
        }

        /// <summary>Called by the network after each solve.</summary>
        public void ApplySolution(BeamNode solved)
        {
            switch (Kind)
            {
                case NodeKind.Emitter:
                    LastWatts = solved.DrawWatts;
                    emitterPower?.SetBeamDraw(solved.DrawWatts);
                    break;
                case NodeKind.Receiver:
                    LastWatts = solved.DeliveredWatts;
                    receiverPower?.SetDelivered(solved.DeliveredWatts);
                    break;
                default:
                    LastWatts = solved.InputWatts;
                    break;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Kind != NodeKind.Emitter)
            {
                return;
            }
            if (Operational && LastWatts > 1f)
            {
                if (hum == null || hum.Ended)
                {
                    hum = BeamwireDefOf.Beamwire_EmitterHum.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
                }
                hum?.Maintain();
            }
            else
            {
                EndHum();
            }
        }

        private void EndHum()
        {
            if (hum != null && !hum.Ended)
            {
                hum.End();
            }
            hum = null;
        }

        /// <summary>Turns the node by <paramref name="steps"/> x 45° (positive = clockwise).</summary>
        public void Rotate(int steps)
        {
            orientation = (int)Facing.Rotate(steps);
            Network?.MarkDirty();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (Props.rotatable && parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Beamwire_RotateLeft".Translate(),
                    defaultDesc = "Beamwire_RotateDesc".Translate(),
                    icon = TexUI.RotLeftTex,
                    action = () => Rotate(-1),
                };
                yield return new Command_Action
                {
                    defaultLabel = "Beamwire_RotateRight".Translate(),
                    defaultDesc = "Beamwire_RotateDesc".Translate(),
                    icon = TexUI.RotRightTex,
                    action = () => Rotate(1),
                };
            }

            if (Kind == NodeKind.Emitter && parent.Faction == Faction.OfPlayer && Props.throughputSteps.Count > 1)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Beamwire_Throughput".Translate(MaxThroughput.ToString("0")),
                    defaultDesc = "Beamwire_ThroughputDesc".Translate(),
                    icon = BeamwireTex.Throughput,
                    action = () =>
                    {
                        throughputIndex = (throughputIndex + 1) % Props.throughputSteps.Count;
                        Network?.MarkDirty();
                    },
                };
            }

            yield return new Command_Toggle
            {
                defaultLabel = "Beamwire_ShowBeams".Translate(),
                defaultDesc = "Beamwire_ShowBeamsDesc".Translate(),
                icon = BeamwireTex.ShowBeams,
                isActive = () => BeamwireMod.Settings.showBeams,
                toggleAction = () => BeamwireMod.Settings.showBeams = !BeamwireMod.Settings.showBeams,
            };
        }

        public override string CompInspectStringExtra()
        {
            switch (Kind)
            {
                case NodeKind.Emitter:
                    return "Beamwire_InspectEmitter".Translate(LastWatts.ToString("0"), MaxThroughput.ToString("0"));
                case NodeKind.Receiver:
                    return "Beamwire_InspectReceiver".Translate(LastWatts.ToString("0"), LastDemand.ToString("0"));
                case NodeKind.Relay:
                    return "Beamwire_InspectRelay".Translate(LastWatts.ToString("0"), Props.upkeep.ToString("0"));
                default:
                    return "Beamwire_InspectPassive".Translate(LastWatts.ToString("0"));
            }
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            if (!parent.Spawned || !Props.rotatable || dinfo.Def == null || !dinfo.Def.isExplosive)
            {
                return;
            }
            if (Rand.Chance(Props.misalignChance))
            {
                Rotate(Rand.Bool ? 1 : -1);
                if (parent.Faction == Faction.OfPlayer)
                {
                    Messages.Message("Beamwire_Misaligned".Translate(parent.LabelShort), parent, MessageTypeDefOf.NegativeEvent);
                }
            }
        }
    }
}
