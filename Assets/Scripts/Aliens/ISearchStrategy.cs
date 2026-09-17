using System.Collections.Generic;
using UnityEngine;

public interface ISearchStrategy
{
    AlienSearchPoint Select(IReadOnlyList<AlienSearchPoint> points,
        Vector3 origin, Vector3 direction, Vector3 alienPosition);
}

/// <summary>Da preferencia a puntos cercanos, que estén delante y que no se hayan visitado recientemente.</summary>
public sealed class DirectionalSearchStrategy : ISearchStrategy
{
    public AlienSearchPoint Select(IReadOnlyList<AlienSearchPoint> points,
        Vector3 origin, Vector3 direction, Vector3 alienPosition)
    {
        AlienSearchPoint best = null;
        float bestScore = float.NegativeInfinity;
        direction.y = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            AlienSearchPoint point = points[i];
            if (point == null || !point.gameObject.activeInHierarchy) continue;
            Vector3 offset = point.SearchPosition - origin;
            offset.y = 0f;
            float distanceScore = 2.5f / (1f + offset.magnitude);
            float directionScore = direction.sqrMagnitude > .01f && offset.sqrMagnitude > .01f
                ? Vector3.Dot(direction.normalized, offset.normalized) : 0f;
            // Es más razonable revisar un escondite o una salida hacia donde escapaba que una esquina cualquiera.
            float hidingScore = point.Kind == AlienSearchPointKind.HidingSpot ? 1.2f : 0f;
            float exitScore = point.Kind == AlienSearchPointKind.Door ? .9f : 0f;
            float approachScore = 1f / (1f + Vector3.Distance(alienPosition, point.SearchPosition));
            float recentPenalty = point.TimeSinceVisited < 20f ? 3f : 0f;
            float randomness = Random.Range(0f, .35f);
            float score = point.Weight + distanceScore + directionScore * 2.5f + hidingScore + exitScore + approachScore
                + randomness - recentPenalty;
            if (score > bestScore) { bestScore = score; best = point; }
        }
        return best;
    }
}
