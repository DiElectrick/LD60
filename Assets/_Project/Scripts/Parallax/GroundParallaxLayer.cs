using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First layer: a looped ground strip. Two-three sprite instances slide left and the
/// one that leaves the view is teleported to the right end of the chain, so the strip
/// never ends. The layer transform itself stays in place - its segments form the conveyor.
/// </summary>
public class GroundParallaxLayer : ParallaxLayer
{
    [Tooltip("Sprite instances that make up the conveyor (2-3 works best).")]
    public List<SpriteRenderer> segments = new();

    void Start()
    {
        ArrangeSegments();
    }

    protected override void Move(float distance)
    {
        if (segments.Count == 0) return;

        var offset = Vector3.left * distance;
        foreach (var segment in segments)
        {
            segment.transform.position += offset;
        }

        // The segment that scrolled out on the left jumps behind the rightmost one.
        foreach (var segment in segments)
        {
            if (segment.bounds.max.x < ViewLeft) TeleportToEnd(segment);
        }
    }

    void TeleportToEnd(SpriteRenderer segment)
    {
        float rightEdge = float.MinValue;
        foreach (var other in segments)
        {
            if (other != segment) rightEdge = Mathf.Max(rightEdge, other.bounds.max.x);
        }

        if (rightEdge == float.MinValue) return;

        // Shift the segment so that its left edge touches the right edge of the chain.
        float shift = rightEdge - segment.bounds.min.x;
        segment.transform.position += new Vector3(shift, 0f, 0f);
    }

    /// <summary>Lays the segments out edge to edge, starting at the left edge of the view.</summary>
    public void ArrangeSegments()
    {
        if (segments.Count == 0) return;

        float x = ViewLeft;
        foreach (var segment in segments)
        {
            float width = segment.bounds.size.x;

            // Sprites may have a non-centred pivot, so account for the offset
            // between the transform and the sprite bounds.
            float centerOffset = segment.bounds.center.x - segment.transform.position.x;

            var position = segment.transform.position;
            position.x = x + width * 0.5f - centerOffset;
            segment.transform.position = position;

            x += width;
        }
    }
}
