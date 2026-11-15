using System.Collections.Generic;
using Beamwire.Core;
using Verse;

namespace Beamwire
{
    public class CompProperties_BeamNode : CompProperties
    {
        public NodeKind kind = NodeKind.Mirror;

        /// <summary>Emitter: the throughput limits the player can cycle through, in watts.</summary>
        public List<float> throughputSteps = new List<float> { 250f, 500f, 1000f, 2000f };

        /// <summary>Emitter: index into <see cref="throughputSteps"/> for newly built emitters.</summary>
        public int defaultThroughputIndex = 2;

        /// <summary>Receiver: the most it will ever ask for, in watts.</summary>
        public float maxIntake = 2000f;

        /// <summary>Relay: watts it consumes from the beam while passing power on.</summary>
        public float upkeep = 30f;

        /// <summary>Whether players can rotate the node in place with a gizmo.</summary>
        public bool rotatable = true;

        /// <summary>Capacitor: the most energy it stores, in watt-days.</summary>
        public float capacity = 600f;

        /// <summary>Capacitor: fastest charge rate, in watts going into storage.</summary>
        public float maxCharge = 1000f;

        /// <summary>Capacitor: fastest discharge rate, in watts.</summary>
        public float maxDischarge = 1000f;

        /// <summary>Capacitor: fraction of charging power that ends up stored.</summary>
        public float chargeEfficiency = 0.85f;

        /// <summary>Chance that an explosion knocks this node out of alignment (random rotation).</summary>
        public float misalignChance = 0.5f;

        public CompProperties_BeamNode()
        {
            compClass = typeof(CompBeamNode);
        }
    }

    public class CompProperties_OpticalDust : CompProperties
    {
        /// <summary>Dust gained per day outdoors (1 = fully dusty).</summary>
        public float dustPerDay = 0.12f;

        /// <summary>Multiplier on <see cref="dustPerDay"/> under a roof.</summary>
        public float roofedFactor = 0.3f;

        /// <summary>Efficiency lost when fully dusty.</summary>
        public float maxEfficiencyLoss = 0.5f;

        /// <summary>Colonists clean the node once dust reaches this level.</summary>
        public float cleanThreshold = 0.25f;

        public int cleanTicks = 240;

        public CompProperties_OpticalDust()
        {
            compClass = typeof(CompOpticalDust);
        }
    }
}
