using System;
using NUnit.Framework;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Tests
{
    public class CellShapeTests
    {
        [Test]
        public void Constructor_NormalizesToTopLeft()
        {
            var shape = new CellShape(new[] { new Vector2Int(5, 7), new Vector2Int(6, 7) });

            Assert.AreEqual(2, shape.Width);
            Assert.AreEqual(1, shape.Height);
            Assert.IsTrue(shape.Contains(new Vector2Int(0, 0)));
            Assert.IsTrue(shape.Contains(new Vector2Int(1, 0)));
        }

        [Test]
        public void Constructor_IgnoresDuplicates()
        {
            var shape = new CellShape(new[] { Vector2Int.zero, Vector2Int.zero, Vector2Int.right });
            Assert.AreEqual(2, shape.Count);
        }

        [Test]
        public void Constructor_EmptyThrows()
        {
            Assert.Throws<ArgumentException>(() => new CellShape(Array.Empty<Vector2Int>()));
        }

        [Test]
        public void RotateCW_HorizontalBarBecomesVertical()
        {
            Assert.AreEqual("X/X/X", ShapePattern.ParseShape("XXX").RotateCW().ToString());
        }

        [Test]
        public void RotateCW_LShapeTurnsClockwise()
        {
            // X.      XXX
            // X.  ->  X..
            // XX
            Assert.AreEqual("XXX/X..", ShapePattern.ParseShape("X./X./XX").RotateCW().ToString());
        }

        [Test]
        public void RotateCW_FourTimesReturnsOriginal()
        {
            var shape = ShapePattern.ParseShape("XX./.XX/..X");
            Assert.AreEqual(shape, shape.RotateCW().RotateCW().RotateCW().RotateCW());
        }

        [TestCase("X", 1)]
        [TestCase("XX/XX", 1)]
        [TestCase(".X./XXX/.X.", 1)]   // plus
        [TestCase("XXX", 2)]           // bar
        [TestCase("XX./.XX", 2)]       // S / Z
        [TestCase("X./X./XX", 4)]      // L
        [TestCase("XXX/.X.", 4)]       // T
        public void ComputeUniqueRotations_DetectsSymmetry(string pattern, int expected)
        {
            Assert.AreEqual(expected, ShapePattern.ParseShape(pattern).ComputeUniqueRotations().Count);
        }

        [Test]
        public void ComputeUniqueRotations_IndexIsQuarterTurns()
        {
            var shape = ShapePattern.ParseShape("X./X./XX");
            var rotations = shape.ComputeUniqueRotations();

            Assert.AreEqual(shape, rotations[0]);
            Assert.AreEqual(shape.RotateCW(), rotations[1]);
            Assert.AreEqual(shape.RotateCW().RotateCW(), rotations[2]);
        }

        [Test]
        public void Equality_IgnoresOrderAndPosition()
        {
            var a = new CellShape(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) });
            var b = new CellShape(new[] { new Vector2Int(11, 3), new Vector2Int(10, 3) });

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void RotateCellCW_FollowsTheShape()
        {
            var shape = ShapePattern.ParseShape("X./X./XX");
            var foot = new Vector2Int(1, 2);                     // the bottom-right cell of the L
            var rotatedFoot = CellShape.RotateCellCW(foot, shape.Height);

            Assert.IsTrue(shape.RotateCW().Contains(rotatedFoot));
            Assert.AreEqual(new Vector2Int(0, 1), rotatedFoot);
        }

        [Test]
        public void ShapePattern_RoundTrips()
        {
            Assert.AreEqual("XX./.XX", ShapePattern.ParseShape("XX./.XX").ToString());
            Assert.AreEqual("X/X", ShapePattern.ToPattern(ShapePattern.Parse("#|#")));
        }

        [Test]
        public void ShapePattern_EmptyThrows()
        {
            Assert.Throws<ArgumentException>(() => ShapePattern.Parse("../.."));
        }
    }
}
