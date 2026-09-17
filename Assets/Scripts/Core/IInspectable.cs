using UnityEngine;

/// <summary>Permite que un alien revise distintos objetos con la misma orden. Cada objeto decide dónde debe pararse el alien y qué sucede al revisarlo.</summary>
public interface IInspectable
{
    string InspectionName { get; }
    Vector3 InspectionPosition { get; }
    bool TryBeginInspection(GameObject actor);
    void EndInspection(GameObject actor);
}
