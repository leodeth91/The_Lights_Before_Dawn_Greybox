using System.Collections.Generic;
using UnityEngine;

/// <summary>Recuerda durante un tiempo quién produjo un sonido concreto. El compañero responde solo si la pregunta corresponde a ese mismo sonido.</summary>
[DisallowMultipleComponent]
public sealed class SoundResponsibility : MonoBehaviour
{
    private readonly Dictionary<int, float> responsibleSounds = new Dictionary<int, float>();
    private readonly List<int> expiredSounds = new List<int>();

    public bool IsActive
    {
        get { PruneExpired(); return responsibleSounds.Count > 0; }
    }

    public void Claim(int soundId, float duration)
    {
        PruneExpired();
        responsibleSounds[soundId] = Time.time + Mathf.Max(0.1f, duration);
    }

    public bool IsResponsibleFor(int soundId)
    {
        PruneExpired();
        return responsibleSounds.TryGetValue(soundId, out float until) && Time.time <= until;
    }

    public void Acknowledge(int soundId) => responsibleSounds.Remove(soundId);

    public void Clear()
    {
        responsibleSounds.Clear();
    }

    private void PruneExpired()
    {
        expiredSounds.Clear();
        foreach (var entry in responsibleSounds)
            if (entry.Value < Time.time) expiredSounds.Add(entry.Key);
        foreach (int soundId in expiredSounds) responsibleSounds.Remove(soundId);
    }
}
