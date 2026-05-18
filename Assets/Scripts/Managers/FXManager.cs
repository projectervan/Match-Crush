using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton yang menangani seluruh efek visual (VFX) dalam game.
/// Menyediakan fungsi untuk memunculkan partikel ledakan dan screen shake.
/// Tempatkan pada GameObject persistent di GameplayScene.
/// </summary>
public class FXManager : MonoBehaviour
{
    #region Singleton
    public static FXManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [Header("Explosion Particle")]
    [Tooltip("Prefab Particle System untuk efek ledakan.")]
    [SerializeField] private GameObject explosionPrefab;

    [Tooltip("Durasi hidup partikel sebelum auto-destroy (detik).")]
    [SerializeField] private float explosionLifetime = 1.5f;

    [Header("Screen Shake Defaults")]
    [SerializeField] private float defaultShakeDuration = 0.2f;
    [SerializeField] private float defaultShakeMagnitude = 0.1f;

    [Header("Haptic Feedback")]
    [Tooltip("Aktifkan getaran HP (Android) saat ledakan besar.")]
    [SerializeField] private bool enableHaptic = true;

    // State
    private Camera mainCamera;
    private Vector3 cameraOriginalPosition;
    private bool isShaking = false;

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraOriginalPosition = mainCamera.transform.localPosition;
        }
    }

    #region Explosion / Particle

    /// <summary>
    /// Memunculkan prefab Particle System di posisi tertentu.
    /// Partikel akan otomatis dihancurkan setelah lifetime habis.
    /// </summary>
    /// <param name="position">Posisi world spawn partikel.</param>
    public void PlayExplosion(Vector3 position)
    {
        if (explosionPrefab == null)
        {
            Debug.LogWarning("FXManager: explosionPrefab belum di-assign di Inspector.");
            return;
        }

        GameObject fx = Instantiate(explosionPrefab, position, Quaternion.identity);

        // Pastikan particle system berjalan
        ParticleSystem ps = fx.GetComponent<ParticleSystem>();
        if (ps != null && !ps.isPlaying)
        {
            ps.Play();
        }

        // Auto-destroy setelah lifetime
        Destroy(fx, explosionLifetime);
    }

    /// <summary>
    /// Overload: memunculkan partikel dengan warna tertentu (sesuai warna candy).
    /// </summary>
    /// <param name="position">Posisi world spawn.</param>
    /// <param name="color">Warna partikel.</param>
    public void PlayExplosion(Vector3 position, Color color)
    {
        if (explosionPrefab == null)
        {
            Debug.LogWarning("FXManager: explosionPrefab belum di-assign di Inspector.");
            return;
        }

        GameObject fx = Instantiate(explosionPrefab, position, Quaternion.identity);

        ParticleSystem ps = fx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // Set warna start
            var main = ps.main;
            main.startColor = color;

            if (!ps.isPlaying)
                ps.Play();
        }

        Destroy(fx, explosionLifetime);
    }

    #endregion

    #region Screen Shake

    /// <summary>
    /// Memulai screen shake dengan durasi dan magnitude yang ditentukan.
    /// Memanipulasi posisi lokal Camera utama.
    /// </summary>
    /// <param name="duration">Durasi shake dalam detik.</param>
    /// <param name="magnitude">Intensitas getaran (offset posisi maksimum).</param>
    public void ScreenShake(float duration, float magnitude)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("FXManager: Main Camera tidak ditemukan.");
                return;
            }
            cameraOriginalPosition = mainCamera.transform.localPosition;
        }

        // Jika sudah shaking, stop yang lama dan mulai yang baru
        if (isShaking)
        {
            StopCoroutine(nameof(ShakeCoroutine));
            mainCamera.transform.localPosition = cameraOriginalPosition;
        }

        StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    /// <summary>
    /// Screen shake dengan nilai default dari Inspector.
    /// </summary>
    public void ScreenShake()
    {
        ScreenShake(defaultShakeDuration, defaultShakeMagnitude);
    }

    /// <summary>
    /// Coroutine yang menggerakkan kamera secara acak selama durasi tertentu.
    /// </summary>
    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Offset acak dari posisi asli
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            mainCamera.transform.localPosition = new Vector3(
                cameraOriginalPosition.x + offsetX,
                cameraOriginalPosition.y + offsetY,
                cameraOriginalPosition.z
            );

            elapsed += Time.deltaTime;

            // Perlahan kurangi magnitude (ease out)
            magnitude = Mathf.Lerp(magnitude, 0f, elapsed / duration);

            yield return null;
        }

        // Kembalikan ke posisi asli
        mainCamera.transform.localPosition = cameraOriginalPosition;
        isShaking = false;
    }

    #endregion

    #region Haptic Feedback (Android)

    /// <summary>
    /// Memicu getaran HP di Android.
    /// Dipanggil bersama dengan ledakan besar atau combo tinggi.
    /// </summary>
    /// <param name="milliseconds">Durasi getaran dalam milidetik.</param>
    public void TriggerHaptic(long milliseconds = 50)
    {
        if (!enableHaptic) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (vibrator != null)
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"FXManager: Haptic failed — {e.Message}");
        }
#else
        Debug.Log($"FXManager: Haptic triggered (simulated) — {milliseconds}ms");
#endif
    }

    #endregion

    #region Combo / Convenience

    /// <summary>
    /// Efek gabungan: ledakan + screen shake + haptic.
    /// Digunakan untuk match besar atau line blast.
    /// </summary>
    /// <param name="position">Posisi ledakan.</param>
    /// <param name="intensity">Intensitas (0-1) menentukan magnitude shake dan durasi haptic.</param>
    public void PlayBigExplosion(Vector3 position, float intensity = 0.5f)
    {
        PlayExplosion(position);

        float shakeDuration = Mathf.Lerp(0.1f, 0.4f, intensity);
        float shakeMagnitude = Mathf.Lerp(0.05f, 0.2f, intensity);
        ScreenShake(shakeDuration, shakeMagnitude);

        long hapticMs = (long)Mathf.Lerp(30f, 100f, intensity);
        TriggerHaptic(hapticMs);
    }

    #endregion
}
