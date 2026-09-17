using UnityEngine;

/// <summary>Mueve la cabeza hacia un interés o barre los alrededores. La visión usa este Transform; los ojos solo ayudan a ver el movimiento en el boceto.</summary>
public sealed class AlienHeadScanner : MonoBehaviour
{
    [SerializeField] private Transform head;
    private float phase;
    private Quaternion rest;
    private bool initialized;
    public Transform Head => head != null ? head : transform;

    private void Awake()
    {
        InitializeHeadMotion();
    }

    public void Configure(Transform value)
    {
        head = value;
        InitializeHeadMotion();
    }

    private void InitializeHeadMotion()
    {
        if (head == null)
        {
            initialized = false;
            return;
        }

        rest = head.localRotation;
        phase = Random.Range(0f, 6f);
        initialized = true;
    }

    public void Look(bool scanning, Vector3? point)
    {
        if (head == null) return;
        if (!initialized) InitializeHeadMotion();
        Quaternion target;
        if (point.HasValue)
        {
            Vector3 direction = transform.InverseTransformDirection(point.Value - head.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -110f, 110f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -35f, 40f);
            target = rest * Quaternion.Euler(pitch, yaw, 0);
        }
        else
        {
            float wave = Mathf.Sin(Time.time * 1.8f + phase);
            target = rest * Quaternion.Euler(Mathf.Sin(Time.time + phase) * 7,
                wave * (scanning ? 105f : 18f), scanning ? Mathf.Sin(Time.time * 2 + phase) * 8 : 0);
        }
        head.localRotation = Quaternion.Slerp(head.localRotation, target, Time.deltaTime * (scanning ? 5f : 3f));
    }
}
