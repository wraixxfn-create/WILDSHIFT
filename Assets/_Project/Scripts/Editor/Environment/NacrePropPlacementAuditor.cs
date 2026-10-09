using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wildshift.Editor.Environment
{
    /// <summary>
    /// Reads the authored prop groups under the Nacre region root and reports whether the layout still obeys the
    /// placement rules documented in <c>docs/nacre-graybox-props.md</c>. It is a read-only audit: it never moves,
    /// creates, or destroys scene objects, and it is editor-only, so it has no effect in a build or in Play Mode.
    /// </summary>
    /// <remarks>
    /// <para>The rules are geometric and conservative. They use world-space axis-aligned bounds, so a rotated prop is
    /// judged by the footprint it actually occupies. They cannot replace walking the routes in Play Mode; they exist
    /// to catch the mistakes that are easy to make while nudging props — a prop on a route strip, a prop that seals a
    /// passage, a prop that floats above its surface, and a decorative prop that carries a collider it does not need.</para>
    /// <para><b>Errors</b> mean the layout blocks or hides the routes: a collider prop inside a route corridor or the
    /// spawn clearance, a thin marker placed across a route centre line, a decorative prop covering a painted route
    /// strip, two collider props that intersect, a collider under the step offset, or a prop left floating above or
    /// sunk into the surface under it.</para>
    /// <para><b>Warnings</b> are style and budget concerns: a decorative prop that still carries a collider, a prop
    /// whose name does not describe the prefab it came from, more props in a group than the budget allows, or a
    /// collider prop closer to an obstacle than the spacing the rest of the layout keeps.</para>
    /// </remarks>
    public static class NacrePropPlacementAuditor
    {
        /// <summary>World-space half-width of the player capsule, from <c>ThirdPersonPlayerMovement</c>.</summary>
        private const float PlayerRadius = 0.5f;

        /// <summary>Largest step the controller can climb, so anything above this is an obstacle rather than a surface.</summary>
        private const float StepOffset = 0.3f;

        /// <summary>How far a prop may float above the surface under it before it looks wrong.</summary>
        private const float MaxFloat = 0.15f;

        /// <summary>How far a prop may sink below the surface under it before it looks wrong.</summary>
        private const float MaxSink = 0.35f;

        /// <summary>
        /// Corridors a prop with a collider must stay out of. Each one is a route widened by the player capsule plus a
        /// working margin, so a prop inside it can be stepped into or can pinch the passage. Values are world-space XZ
        /// rectangles, taken from the routes in <c>docs/nacre-graybox-region.md</c>.
        /// </summary>
        private static readonly Corridor[] RouteCorridors =
        {
            new Corridor("main route south leg", new Vector2(51.0f, -14.75f), new Vector2(69.0f, -11.25f)),
            new Corridor("main route north leg", new Vector2(66.25f, -12.0f), new Vector2(69.75f, 12.5f)),
            new Corridor("plateau route leg", new Vector2(69.0f, 11.25f), new Vector2(71.0f, 14.75f)),
            new Corridor("ramp arrival plaza", new Vector2(66.0f, 11.5f), new Vector2(75.0f, 13.5f)),
            new Corridor("optional gully route", new Vector2(47.0f, -8.0f), new Vector2(50.5f, 9.6f)),
            new Corridor("overlook ramp", new Vector2(47.0f, 8.9f), new Vector2(51.0f, 11.2f)),
            new Corridor("start safe zone", new Vector2(44.0f, -17.2f), new Vector2(51.0f, -10.8f)),
        };

        /// <summary>
        /// The painted route strips themselves. A decorative prop has no collider, so it cannot block anything, but it
        /// must not cover a strip: the strips are the navigation cue the player follows.
        /// </summary>
        private static readonly Corridor[] RouteStrips =
        {
            new Corridor("main route south strip", new Vector2(51.0f, -14.0f), new Vector2(69.0f, -12.0f)),
            new Corridor("main route north strip", new Vector2(67.0f, -12.0f), new Vector2(69.0f, -0.6f)),
            new Corridor("plateau route strip A", new Vector2(67.0f, 6.0f), new Vector2(69.0f, 12.5f)),
            new Corridor("plateau route strip B", new Vector2(69.0f, 12.5f), new Vector2(71.0f, 13.5f)),
            new Corridor("optional gully strip", new Vector2(47.0f, -7.9f), new Vector2(50.5f, 9.0f)),
            new Corridor("start pad", new Vector2(44.0f, -17.0f), new Vector2(51.0f, -11.0f)),
        };

        /// <summary>Route centre lines a thin marker may sit beside, but never across.</summary>
        private static readonly Segment[] RouteCentreLines =
        {
            new Segment(new Vector2(51.0f, -13.0f), new Vector2(69.0f, -13.0f)),
            new Segment(new Vector2(68.0f, -12.0f), new Vector2(68.0f, 12.5f)),
            new Segment(new Vector2(69.0f, 13.0f), new Vector2(71.0f, 13.0f)),
            new Segment(new Vector2(48.75f, -7.9f), new Vector2(48.75f, 9.0f)),
        };

        /// <summary>World spawn point and the radius kept clear around it.</summary>
        private static readonly Vector3 SpawnPoint = new Vector3(47.5f, 1.05f, -14.0f);

        private const float SpawnClearance = 3.0f;

        /// <summary>Maximum prop count per authored group. Keeps the layout modest by construction.</summary>
        private const int PropsPerGroupBudget = 20;

        /// <summary>Spacing a collider prop should keep from other obstacles, in metres.</summary>
        private const float PreferredObstacleGap = 1.0f;

        /// <summary>
        /// Audits every authored prop group under <paramref name="regionRoot"/>. The root is expected to be
        /// <c>NACRE_GRAYBOX_REGION_ROOT</c>; pass any parent that contains the <c>NACRE_*_Props_*</c> groups.
        /// </summary>
        /// <param name="regionRoot">The Nacre region root, or null.</param>
        /// <param name="errors">Receives rule violations that block or hide the routes.</param>
        /// <param name="warnings">Receives style and budget concerns.</param>
        /// <returns>True when <paramref name="errors"/> received nothing.</returns>
        public static bool Audit(Transform regionRoot, List<string> errors, List<string> warnings)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            if (regionRoot == null)
            {
                errors.Add("No region root is selected. Select NACRE_GRAYBOX_REGION_ROOT, or a prop group under it.");
                return false;
            }

            List<Transform> groups = new List<Transform>();
            CollectPropGroups(regionRoot, groups);
            if (groups.Count == 0)
            {
                errors.Add("No prop group was found under '" + regionRoot.name + "'. Prop groups are named " +
                           "NACRE_*_Props_* and hold the reusable prop prefab instances.");
                return false;
            }

            List<PropRecord> props = new List<PropRecord>();
            List<PropRecord> terrain = new List<PropRecord>();
            CollectRecords(regionRoot, props, terrain);

            for (int i = 0; i < groups.Count; i++)
            {
                AuditGroup(groups[i], props, terrain, errors, warnings);
            }

            return errors.Count == 0;
        }

        private static void CollectPropGroups(Transform root, List<Transform> groups)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root)
                {
                    continue;
                }

                if (child.name.IndexOf("_Props_", StringComparison.Ordinal) >= 0)
                {
                    groups.Add(child);
                }
            }
        }

        private static void CollectRecords(Transform root, List<PropRecord> props, List<PropRecord> terrain)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PropRecord record = PropRecord.Create(child, renderer);
                if (record == null)
                {
                    continue;
                }

                if (IsProp(record))
                {
                    props.Add(record);
                }
                else
                {
                    terrain.Add(record);
                }
            }
        }

        /// <summary>
        /// A prop is a direct child of a group whose name contains <c>_Props_</c>. Everything else under the region
        /// root is treated as terrain or as an existing layout object, which props must respect.
        /// </summary>
        private static bool IsProp(PropRecord record)
        {
            Transform parent = record.Transform.parent;
            return parent != null && parent.name.IndexOf("_Props_", StringComparison.Ordinal) >= 0;
        }

        private static void AuditGroup(Transform group, List<PropRecord> props, List<PropRecord> terrain,
            List<string> errors, List<string> warnings)
        {
            List<PropRecord> members = new List<PropRecord>();
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i].Transform.parent == group)
                {
                    members.Add(props[i]);
                }
            }

            if (members.Count == 0)
            {
                warnings.Add("Group '" + group.name + "' holds no props. Delete the empty group or add props to it.");
                return;
            }

            if (members.Count > PropsPerGroupBudget)
            {
                warnings.Add("Group '" + group.name + "' holds " + members.Count + " props, above the budget of " +
                             PropsPerGroupBudget + ". Add more groups rather than more props, or merge nearby props.");
            }

            for (int i = 0; i < members.Count; i++)
            {
                AuditProp(members[i], group, props, terrain, errors, warnings);
            }
        }

        private static void AuditProp(PropRecord prop, Transform group, List<PropRecord> props,
            List<PropRecord> terrain, List<string> errors, List<string> warnings)
        {
            string label = "'" + prop.Name + "' in '" + group.name + "'";

            if (prop.GroupHint != null && prop.Name.IndexOf(prop.GroupHint, StringComparison.OrdinalIgnoreCase) < 0)
            {
                warnings.Add(label + " is named '" + prop.Name + "' but comes from prefab '" + prop.PrefabName +
                             "'. Give it a descriptive name that says what it is in the layout.");
            }

            AuditCollision(prop, label, errors, warnings);
            AuditRoutes(prop, label, errors);
            AuditSpacing(prop, props, terrain, label, errors, warnings);
            AuditSeating(prop, terrain, label, errors);
        }

        private static void AuditCollision(PropRecord prop, string label, List<string> errors, List<string> warnings)
        {
            if (prop.Collider == null)
            {
                // Ground detail is expected to have no collider, so there is nothing to check here.
                return;
            }

            // Decorative props are identified by their prefab: shell plates and pebbles are visual only.
            bool decorative = prop.PrefabName == "NacreShellPlate" || prop.PrefabName == "NacrePebble";
            if (decorative)
            {
                warnings.Add(label + " uses the decorative prefab '" + prop.PrefabName + "' but carries a collider. " +
                             "Remove the collider: it is ground detail, and a low collider here can snag the player.");
                return;
            }

            if (prop.Bounds.size.y <= StepOffset)
            {
                errors.Add(label + " has a collider but is only " + prop.Bounds.size.y.ToString("0.00") + " m tall, " +
                           "under the " + StepOffset + " m step offset. The player will step onto it, so it reads as a " +
                           "lip rather than an obstacle. Remove the collider or make it taller.");
            }
        }

        private static void AuditRoutes(PropRecord prop, string label, List<string> errors)
        {
            if (prop.Collider == null)
            {
                // A decorative prop cannot block anything, but it must not cover a painted route strip.
                for (int i = 0; i < RouteStrips.Length; i++)
                {
                    Corridor strip = RouteStrips[i];
                    if (strip.Overlaps(prop.Bounds))
                    {
                        errors.Add(label + " (decorative, no collider) covers the " + strip.Name +
                                   ". Move it off the strip so the route stays readable.");
                    }
                }

                return;
            }

            bool thin = prop.Bounds.size.x <= PlayerRadius * 2.0f && prop.Bounds.size.z <= PlayerRadius * 2.0f;

            for (int i = 0; i < RouteCorridors.Length; i++)
            {
                Corridor corridor = RouteCorridors[i];
                if (!corridor.Overlaps(prop.Bounds))
                {
                    continue;
                }

                if (thin)
                {
                    // A thin marker is allowed at a route edge, but never across the centre line.
                    float centre = CentreLineDistance(prop.Centre);
                    if (centre < PlayerRadius + 0.7f)
                    {
                        errors.Add(label + " sits " + centre.ToString("0.00") + " m from a route centre line, inside the " +
                                   PlayerRadius + " m capsule plus its clearance. Move it to the route edge.");
                    }

                    continue;
                }

                errors.Add(label + " overlaps the " + corridor.Name + " corridor with a collider. Move it outside the " +
                           "corridor so the route stays passable.");
            }

            float spawnDistance = Vector2.Distance(new Vector2(prop.Centre.x, prop.Centre.z),
                new Vector2(SpawnPoint.x, SpawnPoint.z));
            if (spawnDistance < SpawnClearance + Mathf.Max(prop.Bounds.extents.x, prop.Bounds.extents.z))
            {
                errors.Add(label + " is " + spawnDistance.ToString("0.00") + " m from the spawn, inside the " +
                           SpawnClearance + " m clearance. Move it away from the spawn.");
            }
        }

        private static void AuditSpacing(PropRecord prop, List<PropRecord> props, List<PropRecord> terrain,
            string label, List<string> errors, List<string> warnings)
        {
            float best = float.MaxValue;
            string nearest = null;

            // Intersecting the existing layout is always a mistake, whatever the neighbour is.
            for (int i = 0; i < terrain.Count; i++)
            {
                PropRecord other = terrain[i];

                // The surface a prop rests on is expected to touch it; only obstacles beside it can be intersected.
                if (!BlocksStanding(prop, other))
                {
                    continue;
                }

                if (prop.Collider != null && other.Collider != null && Gap(prop.Bounds, other.Bounds) <= 0.0f)
                {
                    errors.Add(label + " intersects '" + other.Name + "'. Separate them, or sink the prop into the " +
                               "surface it rests on instead of into an obstacle beside it.");
                }
            }

            // Spacing is only measured between props. The crevices in the pre-existing layout are already listed in
            // the crevice table in docs/nacre-graybox-region.md; re-reporting them here would only add noise.
            for (int i = 0; i < props.Count; i++)
            {
                PropRecord other = props[i];
                if (ReferenceEquals(other, prop))
                {
                    continue;
                }

                float gap = Gap(prop.Bounds, other.Bounds);

                if (prop.Collider != null && other.Collider != null && gap <= 0.0f)
                {
                    errors.Add(label + " intersects the prop '" + other.Name + "'. Two collider props must not overlap.");
                }

                // Only two solid obstacles can pinch the capsule between them. Ground detail is meant to sit at the
                // base of a rock, so measuring its distance would only report the composition as a problem.
                if (!IsObstacle(prop) || !IsObstacle(other))
                {
                    continue;
                }

                if (gap < best)
                {
                    best = gap;
                    nearest = other.Name;
                }
            }

            if (prop.Collider == null || best >= float.MaxValue || best >= PreferredObstacleGap)
            {
                return;
            }

            warnings.Add(label + " is " + best.ToString("0.00") + " m from '" + nearest + "', below the preferred " +
                         PreferredObstacleGap + " m spacing. Two props this close can leave a crevice the player " +
                         "capsule cannot enter but can snag on.");
        }

        private static void AuditSeating(PropRecord prop, List<PropRecord> terrain, string label, List<string> errors)
        {
            float surface = float.NegativeInfinity;
            for (int i = 0; i < terrain.Count; i++)
            {
                PropRecord other = terrain[i];
                if (other.Collider == null)
                {
                    continue;
                }

                if (!ContainsXZ(other.Bounds, prop.Centre))
                {
                    continue;
                }

                if (other.Bounds.max.y > prop.Bounds.min.y + MaxFloat)
                {
                    continue;
                }

                if (other.Bounds.max.y > surface)
                {
                    surface = other.Bounds.max.y;
                }
            }

            if (float.IsNegativeInfinity(surface))
            {
                errors.Add(label + " has no surface under its centre. It is floating over a gap or over another prop.");
                return;
            }

            float offset = prop.Bounds.min.y - surface;
            if (offset > MaxFloat)
            {
                errors.Add(label + " floats " + offset.ToString("0.00") + " m above the surface under it (" +
                           surface.ToString("0.00") + "). Lower it so it rests on the surface.");
            }
            else if (offset < -MaxSink)
            {
                errors.Add(label + " sinks " + (-offset).ToString("0.00") + " m below the surface under it. Raise it so " +
                           "it is not buried.");
            }
        }

        /// <summary>
        /// True when a prop is a solid obstacle: it has a collider and is taller than the step offset, so the player
        /// has to walk around it rather than over it.
        /// </summary>
        private static bool IsObstacle(PropRecord prop)
        {
            return prop.Collider != null && prop.Bounds.size.y > StepOffset;
        }

        private static bool BlocksStanding(PropRecord prop, PropRecord other)
        {
            if (other.Collider == null)
            {
                return false;
            }

            // Surfaces the prop stands on are not obstacles beside it.
            return other.Bounds.max.y > prop.Bounds.min.y + StepOffset;
        }

        private static bool ContainsXZ(Bounds bounds, Vector3 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.z >= bounds.min.z && point.z <= bounds.max.z;
        }

        private static float CentreLineDistance(Vector3 centre)
        {
            float best = float.MaxValue;
            Vector2 point = new Vector2(centre.x, centre.z);
            for (int i = 0; i < RouteCentreLines.Length; i++)
            {
                float distance = RouteCentreLines[i].Distance(point);
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private static float Gap(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(b.min.x - a.max.x, a.min.x - b.max.x, 0.0f);
            float dz = Mathf.Max(b.min.z - a.max.z, a.min.z - b.max.z, 0.0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>One renderer in the region, with the bounds and flags the audit needs.</summary>
        private sealed class PropRecord
        {
            public Transform Transform;
            public string Name;
            public string PrefabName;
            public string GroupHint;
            public Bounds Bounds;
            public Vector3 Centre;
            public Collider Collider;

            public static PropRecord Create(Transform transform, Renderer renderer)
            {
                PropRecord record = new PropRecord
                {
                    Transform = transform,
                    Name = transform.name,
                    Bounds = renderer.bounds,
                    Centre = renderer.bounds.center,
                    Collider = transform.GetComponent<Collider>(),
                };

                record.PrefabName = PrefabNameOf(transform);
                record.GroupHint = GroupHintOf(record.PrefabName);
                return record;
            }

            private static string PrefabNameOf(Transform transform)
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(transform.gameObject);
                if (source == null)
                {
                    source = PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject);
                }

                return source != null ? source.name : transform.name;
            }

            private static string GroupHintOf(string prefabName)
            {
                switch (prefabName)
                {
                    case "NacreRockSlab":
                    case "NacreRockShard":
                        return "Rock";
                    case "NacreShellPlate":
                        return "Shell";
                    case "NacrePebble":
                        return "Pebble";
                    case "NacreSurveyCase":
                        return "Case";
                    case "NacreSurveyStake":
                        return "Marker";
                    default:
                        return null;
                }
            }
        }

        /// <summary>A world-space XZ corridor the routes occupy.</summary>
        private struct Corridor
        {
            public readonly string Name;
            public readonly Vector2 Min;
            public readonly Vector2 Max;

            public Corridor(string name, Vector2 min, Vector2 max)
            {
                Name = name;
                Min = min;
                Max = max;
            }

            public bool Overlaps(Bounds bounds)
            {
                return bounds.min.x < Max.x && bounds.max.x > Min.x &&
                       bounds.min.z < Max.y && bounds.max.z > Min.y;
            }
        }

        /// <summary>A route centre-line segment, used to keep thin markers off the walking line.</summary>
        private struct Segment
        {
            public readonly Vector2 A;
            public readonly Vector2 B;

            public Segment(Vector2 a, Vector2 b)
            {
                A = a;
                B = b;
            }

            public float Distance(Vector2 point)
            {
                Vector2 ab = B - A;
                float lengthSquared = ab.sqrMagnitude;
                if (lengthSquared <= float.Epsilon)
                {
                    return Vector2.Distance(point, A);
                }

                float t = Mathf.Clamp01(Vector2.Dot(point - A, ab) / lengthSquared);
                return Vector2.Distance(point, A + ab * t);
            }
        }
    }
}
