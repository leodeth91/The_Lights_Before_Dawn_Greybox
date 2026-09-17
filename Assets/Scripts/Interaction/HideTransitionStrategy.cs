using UnityEngine;

public interface IHideTransitionStrategy
{
    Vector3 EvaluatePosition(Vector3 start, Vector3 destination, float normalizedTime);
    Quaternion EvaluateRotation(Quaternion start, Quaternion destination,
        float normalizedTime);
}

public static class HideTransitionStrategyFactory
{
    public static IHideTransitionStrategy Create(HideTransitionType type)
    {
        switch (type)
        {
            case HideTransitionType.ClimbInside:
                return new ArcHideTransitionStrategy(0.52f);
            case HideTransitionType.StepOverAndHide:
                return new ArcHideTransitionStrategy(0.48f);
            default:
                return new UnderBedHideTransitionStrategy();
        }
    }
}

/// <summary>Desliza al jugador suavemente bajo la cama. Más adelante se puede reemplazar por una animación sin cambiar la lógica del escondite.</summary>
public sealed class UnderBedHideTransitionStrategy : IHideTransitionStrategy
{
    public Vector3 EvaluatePosition(Vector3 start, Vector3 destination, float normalizedTime)
    {
        return Vector3.Lerp(start, destination, Smooth(normalizedTime));
    }

    public Quaternion EvaluateRotation(Quaternion start, Quaternion destination,
        float normalizedTime)
    {
        return Quaternion.Slerp(start, destination, Smooth(normalizedTime));
    }

    internal static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}

public sealed class ArcHideTransitionStrategy : IHideTransitionStrategy
{
    private readonly float arcHeight;

    public ArcHideTransitionStrategy(float height)
    {
        arcHeight = Mathf.Max(0f, height);
    }

    public Vector3 EvaluatePosition(Vector3 start, Vector3 destination, float normalizedTime)
    {
        float smooth = UnderBedHideTransitionStrategy.Smooth(normalizedTime);
        Vector3 position = Vector3.Lerp(start, destination, smooth);
        position.y += Mathf.Sin(smooth * Mathf.PI) * arcHeight;
        return position;
    }

    public Quaternion EvaluateRotation(Quaternion start, Quaternion destination,
        float normalizedTime)
    {
        return Quaternion.Slerp(start, destination,
            UnderBedHideTransitionStrategy.Smooth(normalizedTime));
    }
}
