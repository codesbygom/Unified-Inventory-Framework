using System;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>Where an item sits inside one grid.</summary>
    public readonly struct Placement : IEquatable<Placement>
    {
        /// <summary>Grid cell of the top-left corner of the item's (rotated) bounds.</summary>
        public readonly Vector2Int Origin;

        /// <summary>Index into the item definition's unique rotations (= number of clockwise quarter turns).</summary>
        public readonly int Rotation;

        public Placement(Vector2Int origin, int rotation = 0)
        {
            Origin = origin;
            Rotation = rotation;
        }

        public Placement WithOrigin(Vector2Int origin) => new Placement(origin, Rotation);
        public Placement WithRotation(int rotation) => new Placement(Origin, rotation);

        public bool Equals(Placement other) => Origin == other.Origin && Rotation == other.Rotation;
        public override bool Equals(object obj) => obj is Placement other && Equals(other);
        public override int GetHashCode() => (Origin.GetHashCode() * 397) ^ Rotation;
        public static bool operator ==(Placement a, Placement b) => a.Equals(b);
        public static bool operator !=(Placement a, Placement b) => !a.Equals(b);

        public override string ToString() => $"{Origin} rot {Rotation}";
    }
}
