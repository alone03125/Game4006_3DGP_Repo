using UnityEngine;

public class LazerToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject laserRoot;

    [Header("SFX - One Shot")]
    [Tooltip(" Turn On/Off Sound ")]
    [SerializeField] private AudioSource oneShotAudioSource;
    [SerializeField] private AudioClip turnOnSfx;
    [SerializeField] private AudioClip turnOffSfx;

    [Header("SFX - Ambient Loop (3D)")]
    [Tooltip(" Ambient Loop Sound ")]
    [SerializeField] private AudioSource ambientLoopAudioSource;
    [SerializeField] private AudioClip ambientLoopSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Ambient 3D Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float ambientSpatialBlend = 1f; // 1 = 3D
    [SerializeField] private float ambientMinDistance = 2f;  //  distance for full volume radius
    [SerializeField] private float ambientMaxDistance = 12f; //  after which almost impossible to hear
    [SerializeField] private AudioRolloffMode ambientRolloffMode = AudioRolloffMode.Logarithmic;

    private bool _isOn;

    private void Awake()
    {
        if (laserRoot == null)
            laserRoot = gameObject;

        EnsureAudioSources();

        _isOn = laserRoot != null && laserRoot.activeSelf;
        RefreshAmbientLoop();
    }

    private void EnsureAudioSources()
    {
        if (oneShotAudioSource == null)
        {
            oneShotAudioSource = GetComponent<AudioSource>();
            if (oneShotAudioSource == null)
            {
                oneShotAudioSource = gameObject.AddComponent<AudioSource>();
                oneShotAudioSource.playOnAwake = false;
            }
        }
        oneShotAudioSource.loop = false;

        if (ambientLoopAudioSource == null)
        {
           
            ambientLoopAudioSource = gameObject.AddComponent<AudioSource>();
            ambientLoopAudioSource.playOnAwake = false;
        }

        ambientLoopAudioSource.loop = true;
        ambientLoopAudioSource.spatialBlend = ambientSpatialBlend;
        ambientLoopAudioSource.minDistance = ambientMinDistance;
        ambientLoopAudioSource.maxDistance = ambientMaxDistance;
        ambientLoopAudioSource.rolloffMode = ambientRolloffMode;
        ambientLoopAudioSource.volume = sfxVolume;
    }

    public void TurnOn()
    {
        if (_isOn) return; 
        _isOn = true;

        if (laserRoot != null)
            laserRoot.SetActive(true);

        PlayOneShot(turnOnSfx);
        RefreshAmbientLoop();
    }

    public void TurnOff()
    {
        if (!_isOn) return; 
        _isOn = false;

        if (laserRoot != null)
            laserRoot.SetActive(false);

        PlayOneShot(turnOffSfx);
        RefreshAmbientLoop();
    }

    private void RefreshAmbientLoop()
    {
        if (ambientLoopAudioSource == null) return;

        if (_isOn && ambientLoopSfx != null)
        {
            if (ambientLoopAudioSource.clip != ambientLoopSfx)
                ambientLoopAudioSource.clip = ambientLoopSfx;

            if (!ambientLoopAudioSource.isPlaying)
                ambientLoopAudioSource.Play();
        }
        else
        {
            if (ambientLoopAudioSource.isPlaying)
                ambientLoopAudioSource.Stop();
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotAudioSource == null) return;
        oneShotAudioSource.PlayOneShot(clip, sfxVolume);
    }

   
    private void OnValidate()
    {
        if (ambientLoopAudioSource != null)
        {
            ambientLoopAudioSource.spatialBlend = ambientSpatialBlend;
            ambientLoopAudioSource.minDistance = Mathf.Max(0f, ambientMinDistance);
            ambientLoopAudioSource.maxDistance = Mathf.Max(ambientLoopAudioSource.minDistance, ambientMaxDistance);
            ambientLoopAudioSource.rolloffMode = ambientRolloffMode;
            ambientLoopAudioSource.volume = sfxVolume;
        }
    }
}