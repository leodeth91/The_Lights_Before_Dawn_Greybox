using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>Reproduce pasos y cinco tipos de voz del alien, con sonidos que después podemos reemplazar. Sus pasos y voces ambientales no alertan al compañero.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AlienMotor), typeof(AudioSource))]
public sealed class AlienAmbientAudio : MonoBehaviour
{
    [Header("Replace these with imported assets later")]
    [SerializeField] private AudioClip footstepClip;
    [FormerlySerializedAs("vocalClip")]
    [SerializeField] private AudioClip roamingClip;
    [SerializeField] private AudioClip questionClip;
    [SerializeField] private AudioClip responseClip;
    [SerializeField] private AudioClip callClip;
    [SerializeField] private AudioClip chaseClip;

    [Header("Footsteps")]
    [SerializeField, Min(0.2f)] private float stepDistance = 0.62f;
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.34f;
    [SerializeField, Min(0.1f)] private float teleportResetDistance = 1.5f;

    [Header("Vocalizations")]
    [SerializeField, Min(1f)] private float vocalIntervalMin = 5f;
    [SerializeField, Min(1f)] private float vocalIntervalMax = 20f;
    [SerializeField, Range(0f, 1f)] private float vocalVolume = 0.46f;

    [Header("3D sources")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource vocalSource;

    private AlienMotor motor;
    private AudioClip runtimeFootstepClip;
    private AudioClip runtimeRoamingClip;
    private AudioClip runtimeQuestionClip;
    private AudioClip runtimeResponseClip;
    private AudioClip runtimeCallClip;
    private AudioClip runtimeChaseClip;
    private Vector3 previousPosition;
    private float travelledSinceStep;
    private float nextVocalTime;
    private bool positionInitialized;
    private System.Random variationRandom;

    public AudioClip FootstepClip => footstepClip != null ? footstepClip : runtimeFootstepClip;
    public AudioClip RoamingClip => roamingClip != null ? roamingClip : runtimeRoamingClip;
    public AudioClip QuestionClip => questionClip != null ? questionClip : runtimeQuestionClip;
    public AudioClip ResponseClip => responseClip != null ? responseClip : runtimeResponseClip;
    public AudioClip CallClip => callClip != null ? callClip : runtimeCallClip;
    public AudioClip ChaseClip => chaseClip != null ? chaseClip : runtimeChaseClip;
    public bool IsReady => FootstepClip != null && RoamingClip != null
        && QuestionClip != null && ResponseClip != null && CallClip != null && ChaseClip != null
        && footstepSource != null && vocalSource != null
        && Mathf.Approximately(footstepSource.spatialBlend, 1f)
        && Mathf.Approximately(vocalSource.spatialBlend, 1f);

    private void Awake()
    {
        motor = GetComponent<AlienMotor>();
        variationRandom = new System.Random(unchecked(GetInstanceID() * 397 ^ 20260912));
        EnsureAudioSources();
        BuildMissingGreyboxClips();
        ResetTravelTracking();
        ScheduleNextVocal();
    }

    private void LateUpdate()
    {
        if (!IsGameRunning())
        {
            ResetTravelTracking();
            return;
        }

        UpdateFootsteps();
        if (Time.time >= nextVocalTime)
        {
            AlienController controller = GetComponent<AlienController>();
            if (controller == null || controller.AllowsRoamingVocalization)
                PlayRoamingVocalization();
            ScheduleNextVocal();
        }
    }

    private void UpdateFootsteps()
    {
        if (!positionInitialized)
        {
            ResetTravelTracking();
            return;
        }

        Vector3 displacement = transform.position - previousPosition;
        previousPosition = transform.position;
        displacement.y = 0f;
        float distance = displacement.magnitude;
        if (distance > teleportResetDistance)
        {
            travelledSinceStep = 0f;
            return;
        }

        NavMeshAgent agent = motor != null ? motor.Agent : null;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh
            || agent.velocity.sqrMagnitude < 0.0025f || distance < 0.0001f)
        {
            return;
        }

        travelledSinceStep += distance;
        if (travelledSinceStep < stepDistance) return;
        travelledSinceStep %= stepDistance;
        PlayFootstep();
    }

    public void PlayFootstep()
    {
        AudioClip clip = FootstepClip;
        if (footstepSource == null || clip == null) return;
        footstepSource.pitch = NextFloat(0.88f, 1.01f);
        footstepSource.PlayOneShot(clip, stepVolume);
    }

    public void PlayVocalization() => PlayRoamingVocalization();
    public void PlayRoamingVocalization() => PlayVoice(RoamingClip, .86f, 1.10f, 1f);
    public void PlayQuestionVocalization() => PlayVoice(QuestionClip, .95f, 1.05f, 1.05f);
    public void PlayResponseVocalization() => PlayVoice(ResponseClip, .94f, 1.06f, .98f);
    public void PlayCallVocalization() => PlayVoice(CallClip, .91f, 1.04f, 1.14f);
    public void PlayChaseVocalization() => PlayVoice(ChaseClip, .93f, 1.08f, 1.18f);

    private void PlayVoice(AudioClip clip, float pitchMin, float pitchMax,
        float volumeMultiplier)
    {
        if (vocalSource == null || clip == null) return;
        vocalSource.Stop();
        vocalSource.pitch = NextFloat(pitchMin, pitchMax);
        vocalSource.PlayOneShot(clip, vocalVolume * volumeMultiplier);
    }

    public void EnsureAudioSources()
    {
        AudioSource[] existing = GetComponents<AudioSource>();
        if (footstepSource == null)
        {
            footstepSource = existing.Length > 0
                ? existing[0] : gameObject.AddComponent<AudioSource>();
        }
        if (vocalSource == null || vocalSource == footstepSource)
        {
            vocalSource = existing.Length > 1
                ? existing[1] : gameObject.AddComponent<AudioSource>();
        }

        ConfigureSource(footstepSource, 0.9f, 14f);
        ConfigureSource(vocalSource, 1.2f, 18f);
    }

    private static void ConfigureSource(AudioSource source, float minimumDistance,
        float maximumDistance)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minimumDistance;
        source.maxDistance = maximumDistance;
    }

    private void BuildMissingGreyboxClips()
    {
        if (footstepClip == null)
        {
            runtimeFootstepClip = ProceduralGreyboxAudio.CreateImpact(
                "Greybox_AlienFootstep", 0.18f, 49f, 0.28f, 2.25f, 2201);
        }
        if (roamingClip == null) runtimeRoamingClip = ProceduralGreyboxAudio.CreateChoppedAlienVoice(
            "Greybox_AlienRoaming", .82f, 47f, 4, -.16f, 2202);
        if (questionClip == null) runtimeQuestionClip = ProceduralGreyboxAudio.CreateChoppedAlienVoice(
            "Greybox_AlienQuestion", .72f, 54f, 3, .48f, 2203);
        if (responseClip == null) runtimeResponseClip = ProceduralGreyboxAudio.CreateChoppedAlienVoice(
            "Greybox_AlienResponse", .68f, 51f, 4, -.30f, 2204);
        if (callClip == null) runtimeCallClip = ProceduralGreyboxAudio.CreateChoppedAlienVoice(
            "Greybox_AlienCall", 1.02f, 58f, 6, .34f, 2205);
        if (chaseClip == null) runtimeChaseClip = ProceduralGreyboxAudio.CreateChoppedAlienVoice(
            "Greybox_AlienChase", .92f, 62f, 7, .19f, 2206);
    }

    private bool IsGameRunning()
    {
        return GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying;
    }

    private void ScheduleNextVocal()
    {
        float maximum = Mathf.Max(vocalIntervalMin, vocalIntervalMax);
        nextVocalTime = Time.time + NextFloat(vocalIntervalMin, maximum);
    }

    private float NextFloat(float minimum, float maximum)
    {
        if (variationRandom == null)
            variationRandom = new System.Random(unchecked(GetInstanceID() * 397 ^ 20260912));
        return Mathf.Lerp(minimum, maximum, (float)variationRandom.NextDouble());
    }

    private void ResetTravelTracking()
    {
        previousPosition = transform.position;
        travelledSinceStep = 0f;
        positionInitialized = true;
    }

    private void OnEnable()
    {
        ResetTravelTracking();
        if (variationRandom != null) ScheduleNextVocal();
    }

    private void OnDisable()
    {
        travelledSinceStep = 0f;
    }

    private void OnDestroy()
    {
        if (runtimeFootstepClip != null) Destroy(runtimeFootstepClip);
        if (runtimeRoamingClip != null) Destroy(runtimeRoamingClip);
        if (runtimeQuestionClip != null) Destroy(runtimeQuestionClip);
        if (runtimeResponseClip != null) Destroy(runtimeResponseClip);
        if (runtimeCallClip != null) Destroy(runtimeCallClip);
        if (runtimeChaseClip != null) Destroy(runtimeChaseClip);
    }
}
