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
    public enum ShutterMode
    {
        Auto,
        Open,
        Closed,
    }

    public class CompBeamNode : ThingComp
    {
        private int throughputIndex = -1;
        private int orientation = -1;
        private bool autoAim = true;
        private int realignAtTick = -1;

        // Shutter
        private ShutterMode shutterMode = ShutterMode.Auto;
        private bool shutterClosed;

        // Capacitor
        private float stored;
        private float storedDeltaWatts;

        private CompPowerTrader_BeamEmitter emitterPower;
        private CompBeamReceiver receiverPower;
        private CompFlickable flickable;
        private CompBreakdownable breakdownable;
        private CompOpticalDust dust;
        private CompBeamPowered beamPowered;
        private Sustainer hum;

        // Latest solver results, for the inspect pane and the hum sound.
        public float LastWatts;
        public float LastDemand;

        public CompProperties_BeamNode Props => (CompProperties_BeamNode)props;

        public NodeKind Kind => Props.kind;

        /// <summary>Which of the 8 directions the node points (45° steps, 0 = North, clockwise).</summary>
        public Dir8 Facing => (Dir8)(orientation < 0 ? parent.Rotation.AsInt * 2 : orientation);

        /// <summary>Whether the network may aim this node (the player's Auto/Manual choice).</summary>
        public bool AutoAim => autoAim && Props.rotatable;

        /// <summary>
        /// Auto-aim is in charge right now: the node is set to Auto, auto-aim is on in the settings, and the node
        /// isn't still shaken from an explosion.
        /// </summary>
        public bool AutoAimActive =>
            AutoAim && BeamwireMod.Settings.autoAim && (realignAtTick < 0 || Find.TickManager.TicksGame >= realignAtTick);

        public bool ShutterClosed => Kind == NodeKind.Shutter && shutterClosed;

        public float Stored => stored;

        public float StoredFraction => Props.capacity > 0f ? stored / Props.capacity : 0f;

        /// <summary>Capacitor: adds the last solve's charge or drain over the ticks since then.</summary>
        public void IntegrateStorage(int elapsedTicks)
        {
            if (Kind != NodeKind.Capacitor || elapsedTicks <= 0) return;
            stored = Mathf.Clamp(stored + storedDeltaWatts * elapsedTicks / (float)GenDate.TicksPerDay, 0f, Props.capacity);
        }

        /// <summary>Receiver only: the last plan found no route to it.</summary>
        public bool NoRoute;

        /// <summary>The angle the top sprite is drawn at.</summary>
        public float AngleDegrees => (int)Facing * 45f;

        public float Efficiency => dust != null && BeamwireMod.Settings.dustEnabled ? dust.Efficiency : 1f;

        public CompBeamReceiver Receiver => receiverPower;

        /// <summary>Set when this node is another mod's beam-powered device (see <see cref="CompBeamPowered"/>).</summary>
        public CompBeamPowered BeamPowered => beamPowered != null && beamPowered.Bound ? beamPowered : null;

        /// <summary>
        /// Part of the beam-powered devices API: other mods' lasers treat this node as solid while it is true
        /// (a closed safety shutter).
        /// </summary>
        public bool BlocksLasers => ShutterClosed;

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
                if (BeamPowered != null) return BeamPowered.WantedWatts > 0f;
                if (Kind == NodeKind.Receiver) return receiverPower != null && receiverPower.PowerNet != null;
                return true;
            }
        }

        /// <summary>Switched on and not broken down. Unlike <see cref="Operational"/>, ignores whether an emitter has power.</summary>
        public bool PlannableNow
        {
            get
            {
                if (flickable != null && !flickable.SwitchIsOn) return false;
                if (breakdownable != null && breakdownable.BrokenDown) return false;
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
            beamPowered = parent.GetComp<CompBeamPowered>();
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
            Scribe_Values.Look(ref autoAim, "beamwireAutoAim", true);
            Scribe_Values.Look(ref realignAtTick, "beamwireRealignAt", -1);
            Scribe_Values.Look(ref shutterMode, "beamwireShutterMode", ShutterMode.Auto);
            Scribe_Values.Look(ref shutterClosed, "beamwireShutterClosed", false);
            Scribe_Values.Look(ref stored, "beamwireStored", 0f);
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            // Switching on or off, breaking down and being repaired all change the beam paths and the plan.
            Network?.MarkPlanDirty();
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
                    BeamPowered?.SetBeamWatts(solved.DeliveredWatts);
                    break;
                case NodeKind.Capacitor:
                    LastWatts = solved.InputWatts;
                    storedDeltaWatts = solved.StoredDeltaWatts;
                    break;
                default:
                    LastWatts = solved.InputWatts;
                    break;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (realignAtTick >= 0 && Find.TickManager.TicksGame >= realignAtTick)
            {
                // The shaken node has settled; let auto-aim straighten it out.
                realignAtTick = -1;
                Network?.MarkPlanDirty();
            }
            if (Kind == NodeKind.Shutter && parent.IsHashIntervalTick(ShutterSensorInterval))
            {
                UpdateShutter();
            }
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

        private const int ShutterSensorInterval = 15;

        /// <summary>Opens or closes the shutter for its mode. In Auto, it closes while a non-hostile pawn is in its beam's path.</summary>
        private void UpdateShutter()
        {
            bool closed;
            switch (shutterMode)
            {
                case ShutterMode.Open: closed = false; break;
                case ShutterMode.Closed: closed = true; break;
                default: closed = Network != null && Network.FriendlyInPath(this); break;
            }
            if (closed != shutterClosed)
            {
                shutterClosed = closed;
                Network?.MarkDirty(); // re-trace only: auto-aim plans shutters as open
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
            Network?.MarkPlanDirty();
        }

        /// <summary>The player turns the node by hand, which locks it to manual aim.</summary>
        private void RotateByHand(int steps)
        {
            autoAim = false;
            Rotate(steps);
        }

        /// <summary>Auto-aim turns the node. Returns true if it moved.</summary>
        public bool ApplyPlannedFacing(Dir8 facing)
        {
            if (facing == Facing) return false;
            orientation = (int)facing;
            return true;
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
                    action = () => RotateByHand(-1),
                };
                yield return new Command_Action
                {
                    defaultLabel = "Beamwire_RotateRight".Translate(),
                    defaultDesc = "Beamwire_RotateDesc".Translate(),
                    icon = TexUI.RotRightTex,
                    action = () => RotateByHand(1),
                };
            }

            if (Props.rotatable && parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Beamwire_AutoAim".Translate(),
                    defaultDesc = "Beamwire_AutoAimDesc".Translate(),
                    icon = BeamwireTex.AutoAim,
                    isActive = () => autoAim,
                    toggleAction = () =>
                    {
                        autoAim = !autoAim;
                        Network?.MarkPlanDirty();
                    },
                };
            }

            if (Kind == NodeKind.Shutter && parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    defaultLabel = ("Beamwire_ShutterMode_" + shutterMode).Translate(),
                    defaultDesc = "Beamwire_ShutterModeDesc".Translate(),
                    icon = shutterClosed ? BeamwireTex.ShutterClosed : BeamwireTex.ShutterOpen,
                    action = () =>
                    {
                        shutterMode = (ShutterMode)(((int)shutterMode + 1) % 3);
                        UpdateShutter();
                    },
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
            string main = MainInspectLine();
            if (Props.rotatable)
            {
                string aim = !AutoAim ? "Beamwire_InspectAimManual".Translate()
                    : realignAtTick >= 0 && Find.TickManager.TicksGame < realignAtTick ? "Beamwire_InspectAimShaken".Translate()
                    : BeamwireMod.Settings.autoAim ? "Beamwire_InspectAimAuto".Translate()
                    : "Beamwire_InspectAimAutoOff".Translate();
                main += "\n" + aim;
            }
            if (Kind == NodeKind.Receiver && NoRoute && BeamwireMod.Settings.autoAim)
            {
                main += "\n" + "Beamwire_InspectNoRoute".Translate();
            }
            return main;
        }

        private string MainInspectLine()
        {
            switch (Kind)
            {
                case NodeKind.Emitter:
                    return "Beamwire_InspectEmitter".Translate(LastWatts.ToString("0"), MaxThroughput.ToString("0"));
                case NodeKind.Receiver:
                    return "Beamwire_InspectReceiver".Translate(LastWatts.ToString("0"), LastDemand.ToString("0"));
                case NodeKind.Relay:
                    return "Beamwire_InspectRelay".Translate(LastWatts.ToString("0"), Props.upkeep.ToString("0"));
                case NodeKind.Shutter:
                    return ("Beamwire_ShutterMode_" + shutterMode).Translate() + ": " +
                           (shutterClosed ? "Beamwire_ShutterIsClosed" : "Beamwire_ShutterIsOpen").Translate();
                case NodeKind.Capacitor:
                {
                    string flow = storedDeltaWatts > 0.5f ? "Beamwire_CapCharging".Translate(storedDeltaWatts.ToString("0"))
                        : storedDeltaWatts < -0.5f ? "Beamwire_CapDischarging".Translate((-storedDeltaWatts).ToString("0"))
                        : "Beamwire_CapIdle".Translate();
                    return "Beamwire_InspectCapacitor".Translate(stored.ToString("0"), Props.capacity.ToString("0"), flow);
                }
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
                // Auto-aim leaves it knocked askew for about an hour before re-aiming it.
                realignAtTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                if (parent.Faction == Faction.OfPlayer)
                {
                    Messages.Message("Beamwire_Misaligned".Translate(parent.LabelShort), parent, MessageTypeDefOf.NegativeEvent);
                }
            }
        }
    }
}
