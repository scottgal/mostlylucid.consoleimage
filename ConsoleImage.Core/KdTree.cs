// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// K-D Tree for fast nearest-neighbor search in 6D shape vector space

namespace ConsoleImage.Core;

/// <summary>
/// A k-dimensional tree for fast nearest-neighbor search of shape vectors.
/// Optimized for 6-dimensional shape vectors used in ASCII character matching.
/// </summary>
public class KdTree
{
    private const int Dimensions = 6;
    private readonly KdNode? _root;
    private readonly CharacterEntry[] _entries;

    public readonly struct CharacterEntry
    {
        public readonly char Character;
        public readonly ShapeVector Vector;

        public CharacterEntry(char character, ShapeVector vector)
        {
            Character = character;
            Vector = vector;
        }
    }

    private class KdNode
    {
        public int EntryIndex;
        public int SplitDimension;
        public float SplitValue;
        public KdNode? Left;
        public KdNode? Right;
    }

    public KdTree(IEnumerable<CharacterEntry> entries)
    {
        _entries = entries.ToArray();
        if (_entries.Length == 0)
            throw new ArgumentException("At least one entry is required", nameof(entries));

        var indices = Enumerable.Range(0, _entries.Length).ToArray();
        _root = BuildTree(indices, 0);
    }

    private KdNode? BuildTree(int[] indices, int depth)
    {
        if (indices.Length == 0)
            return null;

        int dimension = depth % Dimensions;

        // Sort indices by the current dimension
        Array.Sort(indices, (a, b) =>
            GetDimensionValue(_entries[a].Vector, dimension)
                .CompareTo(GetDimensionValue(_entries[b].Vector, dimension)));

        int medianIndex = indices.Length / 2;

        var node = new KdNode
        {
            EntryIndex = indices[medianIndex],
            SplitDimension = dimension,
            SplitValue = GetDimensionValue(_entries[indices[medianIndex]].Vector, dimension)
        };

        var leftIndices = indices[..medianIndex];
        var rightIndices = indices[(medianIndex + 1)..];

        node.Left = BuildTree(leftIndices, depth + 1);
        node.Right = BuildTree(rightIndices, depth + 1);

        return node;
    }

    private static float GetDimensionValue(in ShapeVector vector, int dimension) => vector[dimension];

    /// <summary>
    /// Find the nearest character to the given shape vector
    /// </summary>
    public char FindNearest(in ShapeVector target)
    {
        if (_root == null)
            throw new InvalidOperationException("Tree is empty");

        float bestDistSq = float.MaxValue;
        int bestIndex = 0;

        SearchNearest(_root, target, ref bestDistSq, ref bestIndex);

        return _entries[bestIndex].Character;
    }

    /// <summary>
    /// Find the nearest character and return both the character and distance
    /// </summary>
    public (char Character, float Distance) FindNearestWithDistance(in ShapeVector target)
    {
        if (_root == null)
            throw new InvalidOperationException("Tree is empty");

        float bestDistSq = float.MaxValue;
        int bestIndex = 0;

        SearchNearest(_root, target, ref bestDistSq, ref bestIndex);

        return (_entries[bestIndex].Character, MathF.Sqrt(bestDistSq));
    }

    private void SearchNearest(KdNode? node, in ShapeVector target,
                               ref float bestDistSq, ref int bestIndex)
    {
        if (node == null)
            return;

        // Check current node
        float distSq = target.DistanceSquaredTo(_entries[node.EntryIndex].Vector);
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            bestIndex = node.EntryIndex;
        }

        // Determine which side to search first
        float targetValue = GetDimensionValue(target, node.SplitDimension);
        float diff = targetValue - node.SplitValue;

        KdNode? nearSide = diff < 0 ? node.Left : node.Right;
        KdNode? farSide = diff < 0 ? node.Right : node.Left;

        // Search near side first
        SearchNearest(nearSide, target, ref bestDistSq, ref bestIndex);

        // Only search far side if it could contain a closer point
        if (diff * diff < bestDistSq)
        {
            SearchNearest(farSide, target, ref bestDistSq, ref bestIndex);
        }
    }
}
