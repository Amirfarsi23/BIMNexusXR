using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;

// ─────────────────────────────────────────────────────────────
// APIManager.cs
// Handles ALL communication between Unity and the Flask backend.
// Attach this to an empty GameObject called "APIManager" in your scene.
// ─────────────────────────────────────────────────────────────

public class APIManager : MonoBehaviour
{
    // ── SETTINGS ─────────────────────────────────────────────
    // Change this to your ngrok URL when testing on Quest
    // Example: "https://xxxx.ngrok-free.app"
    // Or your local IP when on same WiFi: "http://192.168.1.45:5000"
    [Header("Backend URL")]
    public string backendURL = "http://192.168.1.45:5000";

    // Singleton — so any script can call APIManager.Instance.GetBuilding()
    public static APIManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────
    // GET BUILDING MODEL
    // Downloads the GLB file from Flask /get-ifc endpoint
    // Called by ModelLoader.cs when the scene starts
    // ─────────────────────────────────────────────────────────
    public IEnumerator GetBuildingModel(Action<byte[]> onSuccess, Action<string> onError)
    {
        string url = backendURL + "/get-ifc";
        Debug.Log("Fetching building from: " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 60; // 60 second timeout for large files
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Building downloaded: " + request.downloadHandler.data.Length + " bytes");
                onSuccess(request.downloadHandler.data);
            }
            else
            {
                Debug.LogError("Failed to download building: " + request.error);
                onError(request.error);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // GET ELEMENT INFO
    // When user taps a wall, Unity sends the expressId here
    // Flask looks it up in Neo4j and returns properties
    // Called by ElementInspector.cs
    // ─────────────────────────────────────────────────────────
    public IEnumerator GetElementInfo(int expressId, Action<string> onSuccess, Action<string> onError)
    {
        string url = backendURL + "/element-info/" + expressId;
        Debug.Log("Fetching element info for ID: " + expressId);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess(request.downloadHandler.text);
            }
            else
            {
                onError(request.error);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // ASK AI QUESTION
    // Sends user's spoken question to /chat endpoint
    // Claude answers based on Neo4j building data
    // Called by VoiceAgent.cs
    // ─────────────────────────────────────────────────────────
    public IEnumerator AskQuestion(string question, Action<string> onSuccess, Action<string> onError)
    {
        string url = backendURL + "/chat";
        string jsonBody = "{\"question\": \"" + question + "\"}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess(request.downloadHandler.text);
            }
            else
            {
                onError(request.error);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // GET PROJECT LIST
    // Fetches all uploaded buildings for the lobby screen
    // Called by ProjectSelector.cs (Step 6)
    // ─────────────────────────────────────────────────────────
    public IEnumerator GetProjects(Action<string> onSuccess, Action<string> onError)
    {
        string url = backendURL + "/projects";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess(request.downloadHandler.text);
            }
            else
            {
                onError(request.error);
            }
        }
    }
}
