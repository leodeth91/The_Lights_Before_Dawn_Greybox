using UnityEngine;

/// <summary>Crea sonidos provisionales en memoria para el greybox. Más adelante se pueden reemplazar por archivos de audio sin cambiar las mecánicas.</summary>
public static class ProceduralGreyboxAudio
{
    public static AudioClip CreateImpact(string clipName, float duration, float frequency,
        float noiseMix, float decayPower, int seed)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        var random = new System.Random(seed);
        float filteredNoise = 0f;

        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)sampleRate;
            float progress = index / (float)sampleCount;
            float attack = Mathf.Clamp01(progress / 0.035f);
            float envelope = attack * Mathf.Pow(1f - progress, decayPower);
            float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time);
            float harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 1.72f * time) * 0.24f;
            float rawNoise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, rawNoise, 0.16f);
            float tone = (fundamental + harmonic) * (1f - noiseMix)
                + filteredNoise * noiseMix;
            samples[index] = Mathf.Clamp(tone * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreateGurgle(string clipName, float duration,
        float baseFrequency, int seed)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        var random = new System.Random(seed);
        float phase = 0f;
        float filteredNoise = 0f;

        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)sampleRate;
            float progress = index / (float)sampleCount;
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.10f));
            float release = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((1f - progress) / 0.22f));
            float modulation = 1f + Mathf.Sin(2f * Mathf.PI * 2.7f * time) * 0.19f
                + Mathf.Sin(2f * Mathf.PI * 6.1f * time) * 0.07f;
            phase += 2f * Mathf.PI * baseFrequency * modulation / sampleRate;

            float throat = Mathf.Sin(phase) + Mathf.Sin(phase * 0.51f) * 0.42f;
            float bubbles = Mathf.Sin(phase * 2.35f
                + Mathf.Sin(2f * Mathf.PI * 8.4f * time) * 1.8f);
            float bubbleGate = 0.35f + Mathf.Pow(
                Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 4.2f * time)), 2f) * 0.65f;
            float rawNoise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, rawNoise, 0.09f);
            float signal = throat * 0.48f + bubbles * bubbleGate * 0.25f
                + filteredNoise * 0.16f;
            samples[index] = Mathf.Clamp(signal * attack * release, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>Forma la voz con pulsos y pequeños silencios para que suene entrecortada y extraña.</summary>
    public static AudioClip CreateChoppedAlienVoice(string clipName, float duration,
        float baseFrequency, int pulseCount, float pitchTravel, int seed)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        var random = new System.Random(seed);
        float phase = 0f;
        pulseCount = Mathf.Max(1, pulseCount);

        for (int index = 0; index < sampleCount; index++)
        {
            float progress = index / (float)sampleCount;
            float pulsePosition = progress * pulseCount;
            float withinPulse = pulsePosition - Mathf.Floor(pulsePosition);
            float gate = withinPulse < .66f
                ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(withinPulse / .66f)) : 0f;
            gate *= gate;

            float steppedPitch = Mathf.Floor(pulsePosition) / Mathf.Max(1f, pulseCount - 1f);
            float frequency = baseFrequency * (1f + pitchTravel * (steppedPitch - .5f));
            frequency *= 1f + Mathf.Sin(progress * Mathf.PI * 19f) * .035f;
            phase += 2f * Mathf.PI * frequency / sampleRate;

            float throat = Mathf.Sin(phase) * .62f
                + Mathf.Sin(phase * 1.97f) * .22f
                + Mathf.Sign(Mathf.Sin(phase * .49f)) * .10f;
            float click = withinPulse < .035f
                ? ((float)random.NextDouble() * 2f - 1f) * (1f - withinPulse / .035f)
                : 0f;
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .035f))
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / .06f));
            samples[index] = Mathf.Clamp((throat * gate + click * .28f) * envelope,
                -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
