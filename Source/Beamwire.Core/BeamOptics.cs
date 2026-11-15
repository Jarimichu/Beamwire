using System.Collections.Generic;

namespace Beamwire.Core
{
    /// <summary>How each node kind redirects a beam. Pure functions, easy to unit-test.</summary>
    /// <remarks>
    /// Angles are in 22.5° units ("sixteenths"): 0 = North, clockwise. Beams only travel in the 8 even
    /// sixteenths (multiples of 45°). A mirror's orientation is the normal of its reflective face. Reflecting
    /// direction d off normal n gives 2n - d + 8, which is always even, so every reflection lands on one of the
    /// 8 beam directions.
    /// </remarks>
    public static class BeamOptics
    {
        /// <summary>Which side of a mirror pane a beam hits.</summary>
        public enum Contact
        {
            Front,
            Back,
            Grazing,
        }

        /// <summary>
        /// Works out the directions the beam travels after hitting <paramref name="node"/> while travelling
        /// in <paramref name="travel"/>. An empty result means the node absorbs the beam.
        /// </summary>
        public static void Outputs(BeamNode node, Dir8 travel, List<Dir8> result)
        {
            result.Clear();
            if (!node.Operational)
            {
                return;
            }

            switch (node.Kind)
            {
                case NodeKind.Mirror:
                    switch (ContactOf(node.Orientation, travel))
                    {
                        case Contact.Front:
                            result.Add(Reflect(node.Orientation, travel));
                            break;
                        case Contact.Grazing:
                            result.Add(travel); // the beam slips past the mirror's edge
                            break;
                    }
                    break;

                case NodeKind.Splitter:
                {
                    result.Add(travel); // the transmitted part
                    Contact c = ContactOf(node.Orientation, travel);
                    if (c == Contact.Front)
                    {
                        AddDistinct(result, Reflect(node.Orientation, travel));
                    }
                    else if (c == Contact.Back)
                    {
                        AddDistinct(result, Reflect(node.Orientation + 8, travel));
                    }
                    break;
                }

                case NodeKind.Relay:
                    // A relay won't take a beam fired straight into its output lens.
                    if (travel != node.Facing.Opposite())
                    {
                        result.Add(node.Facing);
                    }
                    break;

                case NodeKind.Emitter:
                case NodeKind.Receiver:
                default:
                    break;
            }
        }

        /// <summary>Which side of a pane with normal <paramref name="normal16"/> a beam travelling in <paramref name="travel"/> hits.</summary>
        public static Contact ContactOf(int normal16, Dir8 travel)
        {
            // A beam hits the front when it travels against the normal: the angle between the travel
            // direction and the reversed normal is less than 90° (4 sixteenths).
            int rel = Dir8Ext.Signed16(travel.To16() - (normal16 + 8));
            int abs = rel < 0 ? -rel : rel;
            if (abs < 4) return Contact.Front;
            if (abs > 4) return Contact.Back;
            return Contact.Grazing;
        }

        /// <summary>Mirror reflection of <paramref name="travel"/> off a pane with normal <paramref name="normal16"/>.</summary>
        public static Dir8 Reflect(int normal16, Dir8 travel)
        {
            int out16 = Dir8Ext.Mod(2 * normal16 - travel.To16() + 8, 16);
            return (Dir8)(out16 / 2);
        }

        private static void AddDistinct(List<Dir8> list, Dir8 d)
        {
            if (!list.Contains(d)) list.Add(d);
        }
    }
}
