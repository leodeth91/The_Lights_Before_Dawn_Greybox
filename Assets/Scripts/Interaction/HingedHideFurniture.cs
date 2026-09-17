using System.Collections;
using UnityEngine;

/// <summary>Abre y cierra tapas o puertas alrededor de sus pivotes. Tanto el Player como los aliens usan IOpenable para pedir el mismo movimiento.</summary>
[DisallowMultipleComponent]
public sealed class HingedHideFurniture : MonoBehaviour, IOpenable
{
    [SerializeField] private Transform[] pivots;
    [SerializeField] private Vector3[] closedEulerAngles;
    [SerializeField] private Vector3[] openEulerAngles;
    [SerializeField, Min(0.05f)] private float movementDuration = 0.22f;

    private Coroutine movementRoutine;
    public bool IsOpen { get; private set; }

    public void Configure(Transform[] hingePivots, Vector3[] closedAngles,
        Vector3[] openAngles, float duration = 0.22f)
    {
        pivots = hingePivots;
        closedEulerAngles = closedAngles;
        openEulerAngles = openAngles;
        movementDuration = Mathf.Max(0.05f, duration);
        Apply(closedEulerAngles);
    }

    public void Open() { IsOpen = true; MoveTo(openEulerAngles); }
    public void Close() { IsOpen = false; MoveTo(closedEulerAngles); }
    public bool TryOpen(GameObject actor)
    {
        if (!HasValidConfiguration(openEulerAngles)) return false;
        Open();
        return true;
    }
    public bool TryClose(GameObject actor)
    {
        if (!HasValidConfiguration(closedEulerAngles)) return false;
        Close();
        return true;
    }

    private void MoveTo(Vector3[] targetAngles)
    {
        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
        }
        movementRoutine = StartCoroutine(Animate(targetAngles));
    }

    private IEnumerator Animate(Vector3[] targetAngles)
    {
        if (!HasValidConfiguration(targetAngles))
        {
            yield break;
        }

        Quaternion[] starts = new Quaternion[pivots.Length];
        Quaternion[] destinations = new Quaternion[pivots.Length];
        for (int index = 0; index < pivots.Length; index++)
        {
            starts[index] = pivots[index].localRotation;
            destinations[index] = Quaternion.Euler(targetAngles[index]);
        }

        float elapsed = 0f;
        while (elapsed < movementDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / movementDuration);
            normalized = normalized * normalized * (3f - 2f * normalized);
            for (int index = 0; index < pivots.Length; index++)
            {
                pivots[index].localRotation = Quaternion.Slerp(starts[index],
                    destinations[index], normalized);
            }
            yield return null;
        }

        Apply(targetAngles);
        movementRoutine = null;
    }

    private bool HasValidConfiguration(Vector3[] angles)
    {
        return pivots != null && angles != null && pivots.Length > 0
            && pivots.Length == angles.Length;
    }

    private void Apply(Vector3[] angles)
    {
        if (!HasValidConfiguration(angles))
        {
            return;
        }

        for (int index = 0; index < pivots.Length; index++)
        {
            if (pivots[index] != null)
            {
                pivots[index].localRotation = Quaternion.Euler(angles[index]);
            }
        }
    }
}
