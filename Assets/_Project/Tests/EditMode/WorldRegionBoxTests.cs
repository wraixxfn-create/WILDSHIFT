using NUnit.Framework;
using UnityEngine;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>Verifies half-open containment and separating-axis overlap for region boxes. Pure math; no scene needed.</summary>
    public sealed class WorldRegionBoxTests
    {
        [Test]
        public void AxisAlignedContainmentIncludesMinimumFaceAndExcludesMaximumFace()
        {
            WorldRegionBox box = AxisAligned(Vector3.zero, Vector3.one);

            Assert.That(box.Contains(new Vector3(-1f, 0f, 0f)), Is.True, "Minimum face belongs to the box.");
            Assert.That(box.Contains(new Vector3(0.999f, 0f, 0f)), Is.True);
            Assert.That(box.Contains(new Vector3(1f, 0f, 0f)), Is.False, "Maximum face does not belong to the box.");
            Assert.That(box.Contains(new Vector3(-1.001f, 0f, 0f)), Is.False);
            Assert.That(box.Contains(new Vector3(0f, 0f, 1f)), Is.False);
        }

        [Test]
        public void SharedFaceBelongsToExactlyOneOfTwoAdjacentBoxes()
        {
            WorldRegionBox left = AxisAligned(Vector3.zero, Vector3.one);
            WorldRegionBox right = AxisAligned(new Vector3(2f, 0f, 0f), Vector3.one);
            Vector3 sharedFace = new Vector3(1f, 0.25f, -0.25f);

            Assert.That(left.Contains(sharedFace), Is.False);
            Assert.That(right.Contains(sharedFace), Is.True);
        }

        [Test]
        public void RotatedBoxContainsPointsThatAnAxisAlignedBoxWouldMiss()
        {
            // A square rotated 45 degrees about Z has its corners on the X and Y axes at distance sqrt(2).
            WorldRegionBox rotated = Rotated(Vector3.zero, Quaternion.Euler(0f, 0f, 45f), new Vector3(1f, 1f, 1f));

            Assert.That(rotated.Contains(new Vector3(1.2f, 0f, 0f)), Is.True,
                "Inside the diamond, although outside the unrotated square.");
            Assert.That(rotated.Contains(new Vector3(1.3f, 1.3f, 0f)), Is.False,
                "Inside the unrotated square's corner, but outside the diamond.");
        }

        [Test]
        public void ZeroSizeBoxContainsNothing()
        {
            WorldRegionBox empty = AxisAligned(Vector3.zero, Vector3.zero);

            Assert.That(empty.Contains(Vector3.zero), Is.False);
        }

        [Test]
        public void OverlappingBoxesReportOverlap()
        {
            WorldRegionBox a = AxisAligned(Vector3.zero, Vector3.one * 2f);
            WorldRegionBox b = AxisAligned(new Vector3(1f, 0f, 0f), Vector3.one * 2f);

            Assert.That(a.Overlaps(b), Is.True);
            Assert.That(b.Overlaps(a), Is.True, "Overlap is symmetric.");
        }

        [Test]
        public void BoxesThatOnlyTouchAtAFaceDoNotOverlap()
        {
            WorldRegionBox a = AxisAligned(Vector3.zero, Vector3.one * 2f);
            WorldRegionBox b = AxisAligned(new Vector3(2f, 0f, 0f), Vector3.one * 2f);

            Assert.That(a.Overlaps(b), Is.False);
            Assert.That(b.Overlaps(a), Is.False);
        }

        [Test]
        public void SeparatingAxisRejectsADiagonalFalsePositiveThatAxisAlignedBoundsWouldReport()
        {
            // The rotated square's axis-aligned bounds reach x = 1.414, which overlaps the square at x >= 1.2.
            // Along the rotated face normal (1, 1, 0) / sqrt(2), the two boxes are separated, so they do not overlap.
            WorldRegionBox diamond = Rotated(Vector3.zero, Quaternion.Euler(0f, 0f, 45f), Vector3.one * 2f);
            WorldRegionBox square = AxisAligned(new Vector3(2.2f, 2.2f, 0f), Vector3.one * 2f);

            Assert.That(diamond.Overlaps(square), Is.False);
            Assert.That(square.Overlaps(diamond), Is.False);
        }

        [Test]
        public void RotatedBoxOverlapsABoxItActuallyIntersects()
        {
            WorldRegionBox diamond = Rotated(Vector3.zero, Quaternion.Euler(0f, 0f, 45f), Vector3.one * 2f);
            WorldRegionBox square = AxisAligned(new Vector3(1f, 0f, 0f), Vector3.one * 2f);

            Assert.That(diamond.Overlaps(square), Is.True);
            Assert.That(square.Overlaps(diamond), Is.True);
        }

        [Test]
        public void BoxesFarApartDoNotOverlap()
        {
            WorldRegionBox a = AxisAligned(Vector3.zero, Vector3.one);
            WorldRegionBox b = AxisAligned(new Vector3(0f, 50f, 0f), Vector3.one);

            Assert.That(a.Overlaps(b), Is.False);
        }

        private static WorldRegionBox AxisAligned(Vector3 center, Vector3 size)
        {
            return Rotated(center, Quaternion.identity, size);
        }

        private static WorldRegionBox Rotated(Vector3 center, Quaternion rotation, Vector3 size)
        {
            return new WorldRegionBox(
                center,
                rotation * Vector3.right,
                rotation * Vector3.up,
                rotation * Vector3.forward,
                size * 0.5f);
        }
    }
}
