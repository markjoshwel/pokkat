using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;


public class FirebaseSignIn : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_InputField usernameInput;
    public TMP_Text statusText;
    public Button signInButton;
    public Button signUpButton;

    private FirebaseAuth auth;
    private DatabaseReference dbRef;

    // Presence
    private DatabaseReference presenceRef;
    private DatabaseReference lastOnlineRef;
    private DatabaseReference connectedRef;
    private EventHandler<ValueChangedEventArgs> connectedHandler;

    private FirebaseUser currentUser;

    // Popup System
    public PopupMessage popup;

    async void Start()
    {
        await InitializeFirebase();
        ValidateUiReferences();

        if (signInButton != null)
            signInButton.onClick.AddListener(OnSignInButton);
        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpButton);
    }

    private void ValidateUiReferences()
    {
        bool ok = true;
        if (emailInput == null) { Debug.LogError("[FirebaseSignIn] emailInput missing."); ok = false; }
        if (passwordInput == null) { Debug.LogError("[FirebaseSignIn] passwordInput missing."); ok = false; }
        if (usernameInput == null) { Debug.LogError("[FirebaseSignIn] usernameInput missing."); ok = false; }
        if (statusText == null) { Debug.LogError("[FirebaseSignIn] statusText missing."); ok = false; }

        if (!ok && statusText != null)
            statusText.text = "Missing UI references (see Console).";
    }

    private async Task InitializeFirebase()
    {
        try
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;

                try
                {
                    dbRef = FirebaseDatabase.DefaultInstance.RootReference;
                    Debug.Log("Firebase initialized successfully.");
                }
                catch (Exception dbEx)
                {
                    Debug.LogError("Failed to initialize Firebase Database: " + dbEx);
                }
            }
            else
            {
                Debug.LogError("Firebase dependencies missing: " + dependencyStatus);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("InitializeFirebase() exception: " + ex);
        }
    }

    // ---------------------------
    // Sign Up
    // ---------------------------

    public async void OnSignUpButton()
    {
        string email = emailInput.text;
        string password = passwordInput.text;
        string username = usernameInput.text;

        if (string.IsNullOrEmpty(email) ||
            string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(username))
        {
            popup?.ShowMessage("Please fill all fields.");
            return;
        }

        popup?.ShowMessage("Creating account...");
        if (statusText != null) statusText.text = "Creating account...";

        try
        {
            var result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            currentUser = result.User;

            if (currentUser != null)
            {
                await WriteUserData(currentUser.UserId, username, email);
                StartPresenceTracking(currentUser.UserId);

                popup?.ShowMessage("Account created!");
                if (statusText != null) statusText.text = "Account created!";
            }
        }
        catch (Exception ex)
        {
            popup?.ShowMessage("Sign Up Failed.");
            if (statusText != null) statusText.text = "Sign Up Failed: " + ex.Message;
            Debug.LogError(ex);
        }
    }

    // ---------------------------
    // Sign In
    // ---------------------------

    public async void OnSignInButton()
    {
        string email = emailInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            popup?.ShowMessage("Email or password missing.");
            return;
        }

        popup?.ShowMessage("Signing in...");
        if (statusText != null) statusText.text = "Signing in...";

        try
        {
            var result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            currentUser = result.User;

            if (currentUser != null)
            {
                popup?.ShowMessage("Logged in!");
                if (statusText != null) statusText.text = "Logged in as: " + currentUser.Email;

                StartPresenceTracking(currentUser.UserId);

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // ---- Database updates ----
                try
                {
                    await dbRef.Child("users").Child(currentUser.UserId).Child("lastLogin").SetValueAsync(now);
                    await dbRef.Child("users").Child(currentUser.UserId).Child("lastUpdated").SetValueAsync(now);

                    var userSnap = await dbRef.Child("users").Child(currentUser.UserId).GetValueAsync();
                    if (!userSnap.Exists || !userSnap.HasChild("createdAt"))
                    {
                        await dbRef.Child("users").Child(currentUser.UserId).Child("createdAt").SetValueAsync(now);
                    }

                    var statusSnap = await dbRef.Child("status").Child(currentUser.UserId).GetValueAsync();
                    if (!statusSnap.Exists)
                    {
                        await dbRef.Child("status").Child(currentUser.UserId).Child("state").SetValueAsync("online");
                        await dbRef.Child("status").Child(currentUser.UserId).Child("last_changed").SetValueAsync(ServerValue.Timestamp);
                    }
                }
                catch (Exception ex2)
                {
                    Debug.LogError("Failed to update timestamps or status: " + ex2);
                }
            }
        }
        catch (Exception ex)
        {
            popup?.ShowMessage("Sign In Failed.");
            if (statusText != null) statusText.text = "Sign In Failed: " + ex.Message;
            Debug.LogError(ex);
        }
    }

    // ---------------------------
    // Write User Data
    // ---------------------------

    private async Task WriteUserData(string userId, string username, string email)
    {
        if (dbRef == null) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        UserData data = new UserData(username, email, now, now, now);
        string json = JsonUtility.ToJson(data);

        try
        {
            await dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json);

            await dbRef.Child("status").Child(userId).Child("state").SetValueAsync("online");
            await dbRef.Child("status").Child(userId).Child("last_changed").SetValueAsync(ServerValue.Timestamp);
        }
        catch (Exception ex)
        {
            Debug.LogError("WriteUserData failed: " + ex);
        }
    }

    // ---------------------------
    // Presence Tracking
    // ---------------------------

    private void StartPresenceTracking(string uid)
    {
        var db = FirebaseDatabase.DefaultInstance;

        presenceRef = db.GetReference("status").Child(uid).Child("state");
        lastOnlineRef = db.GetReference("status").Child(uid).Child("last_changed");
        connectedRef = db.GetReference(".info/connected");

        if (connectedHandler != null)
            connectedRef.ValueChanged -= connectedHandler;

        connectedHandler = async (sender, e) =>
        {
            if (e.Snapshot == null || e.Snapshot.Value == null)
                return;

            bool isConnected = false;
            if (e.Snapshot.Value is bool b) isConnected = b;
            else bool.TryParse(e.Snapshot.Value.ToString(), out isConnected);

            if (isConnected)
            {
                presenceRef.OnDisconnect().SetValue("offline");
                lastOnlineRef.OnDisconnect().SetValue(ServerValue.Timestamp);

                await presenceRef.SetValueAsync("online");
                await lastOnlineRef.SetValueAsync(ServerValue.Timestamp);
            }
        };

        connectedRef.ValueChanged += connectedHandler;
    }

    // ---------------------------
    // Cleanup
    // ---------------------------

    void OnDestroy()
    {
        if (signInButton != null) signInButton.onClick.RemoveListener(OnSignInButton);
        if (signUpButton != null) signUpButton.onClick.RemoveListener(OnSignUpButton);

        if (connectedRef != null && connectedHandler != null)
            connectedRef.ValueChanged -= connectedHandler;
    }

    // ---------------------------
    // UserData Model
    // ---------------------------

    [Serializable]
    public class UserData
    {
        public string username;
        public string email;
        public long createdAt;
        public long lastLogin;
        public long lastUpdated;

        public UserData(string username, string email, long createdAt, long lastLogin, long lastUpdated)
        {
            this.username = username;
            this.email = email;
            this.createdAt = createdAt;
            this.lastLogin = lastLogin;
            this.lastUpdated = lastUpdated;
        }
    }

    public void OnPlayButtonPressed()
    {
        // Load scene
        SceneManager.LoadScene(1);
    }

}


