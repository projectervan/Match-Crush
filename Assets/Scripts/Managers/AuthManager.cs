using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase;
using Firebase.Auth;
using Google;

/// <summary>
/// Singleton yang menangani inisialisasi Firebase dan autentikasi Google Sign-In.
/// Tempatkan pada GameObject persistent di LoginScene.
/// </summary>
public class AuthManager : MonoBehaviour
{
    #region Singleton
    public static AuthManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    [Header("Google Sign-In Config")]
    [Tooltip("Web Client ID dari Firebase Console > Authentication > Sign-in method > Google")]
    [SerializeField] private string webClientId = "YOUR_WEB_CLIENT_ID_HERE";

    // Firebase references
    private FirebaseAuth auth;
    private FirebaseUser currentUser;

    // Events untuk UI binding
    public event Action<string> OnStatusMessage;
    public event Action<FirebaseUser> OnSignInSuccess;
    public event Action<string> OnSignInFailed;

    /// <summary>
    /// User yang sedang login. Null jika belum autentikasi.
    /// </summary>
    public FirebaseUser CurrentUser => currentUser;

    /// <summary>
    /// Apakah user sudah terautentikasi.
    /// </summary>
    public bool IsAuthenticated => currentUser != null;

    private void Start()
    {
        InitializeFirebase();
    }

    /// <summary>
    /// Inisialisasi Firebase dan cek dependency.
    /// </summary>
    private void InitializeFirebase()
    {
        OnStatusMessage?.Invoke("Menginisialisasi Firebase...");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                auth.StateChanged += OnAuthStateChanged;

                // Cek apakah user sudah login sebelumnya
                CheckExistingUser();
            }
            else
            {
                Debug.LogError($"Firebase dependency error: {dependencyStatus}");
                OnStatusMessage?.Invoke($"Error: Firebase tidak tersedia ({dependencyStatus})");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Cek apakah ada session login sebelumnya (auto-login).
    /// </summary>
    private void CheckExistingUser()
    {
        if (auth.CurrentUser != null)
        {
            currentUser = auth.CurrentUser;
            Debug.Log($"Auto-login: {currentUser.DisplayName}");
            OnStatusMessage?.Invoke($"Selamat datang kembali, {currentUser.DisplayName}!");
            OnSignInSuccess?.Invoke(currentUser);

            // Langsung navigasi ke MainMenu
            SceneManager.LoadScene("MainMenuScene");
        }
        else
        {
            OnStatusMessage?.Invoke("Silakan login untuk melanjutkan.");
        }
    }

    /// <summary>
    /// Listener perubahan state autentikasi Firebase.
    /// </summary>
    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        if (auth.CurrentUser != currentUser)
        {
            bool signedIn = (auth.CurrentUser != null);
            if (!signedIn && currentUser != null)
            {
                Debug.Log("User signed out.");
                currentUser = null;
            }

            if (signedIn)
            {
                currentUser = auth.CurrentUser;
                Debug.Log($"Auth state changed: {currentUser.DisplayName}");
            }
        }
    }

    /// <summary>
    /// Dipanggil oleh tombol "Login with Google" di UI.
    /// </summary>
    public void SignInWithGoogle()
    {
        OnStatusMessage?.Invoke("Memproses login...");

        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestIdToken = true
        };

        GoogleSignIn.DefaultInstance.SignIn().ContinueWith((System.Threading.Tasks.Task<GoogleSignInUser> task) =>
        {
            if (task.IsCanceled)
            {
                OnStatusMessage?.Invoke("Login dibatalkan.");
                OnSignInFailed?.Invoke("Login dibatalkan oleh user.");
                return;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"Google Sign-In error: {task.Exception}");
                OnStatusMessage?.Invoke("Login gagal. Coba lagi.");
                OnSignInFailed?.Invoke(task.Exception?.Message ?? "Unknown error");
                return;
            }

            // Berhasil mendapat token dari Google, lanjut autentikasi ke Firebase
            GoogleSignInUser signInUser = task.Result;
            string idToken = signInUser.IdToken;
            FirebaseSignInWithGoogle(idToken);

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Autentikasi ke Firebase menggunakan Google ID Token.
    /// </summary>
    private void FirebaseSignInWithGoogle(string idToken)
    {
        Credential credential = GoogleAuthProvider.GetCredential(idToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWith(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError($"Firebase sign-in error: {task.Exception}");
                OnStatusMessage?.Invoke("Autentikasi Firebase gagal.");
                OnSignInFailed?.Invoke(task.Exception?.Message ?? "Firebase auth failed");
                return;
            }

            currentUser = task.Result;
            Debug.Log($"Firebase sign-in success: {currentUser.DisplayName}");
            OnStatusMessage?.Invoke($"Login berhasil! Halo, {currentUser.DisplayName}");
            OnSignInSuccess?.Invoke(currentUser);

            // Navigasi ke MainMenu setelah login berhasil
            SceneManager.LoadScene("MainMenuScene");

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Sign out dari Firebase dan Google.
    /// </summary>
    public void SignOut()
    {
        auth.SignOut();
        GoogleSignIn.DefaultInstance.SignOut();
        currentUser = null;

        OnStatusMessage?.Invoke("Berhasil logout.");
        Debug.Log("User signed out.");

        // Kembali ke LoginScene
        SceneManager.LoadScene("LoginScene");
    }

    private void OnDestroy()
    {
        if (auth != null)
        {
            auth.StateChanged -= OnAuthStateChanged;
        }
    }
}
