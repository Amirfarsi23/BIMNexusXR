using System.Collections;
using UnityEngine;
using System.IO;

// ─────────────────────────────────────────────────────────────
// ModelLoader.cs
// Downloads the building model from Flask and renders it in VR.
//
// HOW IT WORKS:
// 1. Calls APIManager to download the IFC/GLB file from Flask
// 2. Saves it temporarily to device storage
// 3. Uses GLTFUtility to parse and render the 3D geometry
// 4. Centers the building in the scene
// 5. Shows a loading UI while downloading
//
// Attach this to an empty GameObject called "ModelLoader" in scene.
// Requires: GLTFUtility package installed (Siccity/GLTFUtility)
// ─────────────────────────────────────────────────────────────

public class ModelLoader : MonoBehaviour
{
    // ── REFERENCES ────────────────────────────────────────────
    [Header("Scene References")]
    public GameObject loadingPanel;      // UI panel shown while loading
    public TMPro.TextMeshProUGUI statusText; // shows "Downloading..." etc

    // The loaded building will be placed here
    private GameObject buildingRoot;

    // ── START ─────────────────────────────────────────────────
    void Start()
    {
        // Auto-load building when scene starts
        StartCoroutine(LoadBuilding());
    }

    // ─────────────────────────────────────────────────────────
    // MAIN LOAD FUNCTION
    // ─────────────────────────────────────────────────────────
    public IEnumerator LoadBuilding()
    {
        ShowStatus("Connecting to BIM Nexus...");

        // Step 1 — Download building bytes from Flask
        byte[] modelBytes = null;
        string errorMsg = null;

        yield return StartCoroutine(
            APIManager.Instance.GetBuildingModel(
                onSuccess: (bytes) => { modelBytes = bytes; },
                onError:   (err)   => { errorMsg = err; }
            )
        );

        // Step 2 — Check download succeeded
        if (errorMsg != null)
        {
            ShowStatus("Error: " + errorMsg + "\nMake sure Flask is running!");
            yield break;
        }

        ShowStatus("Building downloaded. Rendering...");

        // Step 3 — Save bytes to temp file
        // GLTFUtility needs a file path to load from
        string tempPath = Path.Combine(Application.temporaryCachePath, "building.glb");
        File.WriteAllBytes(tempPath, modelBytes);
        Debug.Log("Model saved to: " + tempPath);

        // Step 4 — Load GLB using GLTFUtility
        // GLTFUtility is an async loader — we use the callback version
        bool loadDone = false;
        GameObject loadedModel = null;

        Siccity.GLTFUtility.Importer.ImportGLBAsync(
            tempPath,
            new Siccity.GLTFUtility.ImportSettings(),
            (result, clips) =>
            {
                loadedModel = result;
                loadDone = true;
            }
        );

        // Wait for GLTFUtility to finish
        while (!loadDone) yield return null;

        if (loadedModel == null)
        {
            ShowStatus("Failed to parse building model.");
            yield break;
        }

        // Step 5 — Place and center building in scene
        buildingRoot = loadedModel;
        buildingRoot.name = "BIMBuilding";
        CenterBuilding(buildingRoot);

        // Step 6 — Hide loading UI
        HideStatus();

        Debug.Log("Building loaded successfully!");
    }

    // ─────────────────────────────────────────────────────────
    // CENTER BUILDING
    // Moves building so its base sits at y=0, centered at origin
    // ─────────────────────────────────────────────────────────
    void CenterBuilding(GameObject building)
    {
        // Calculate bounds of entire building
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        // Move so building sits on floor (y=0) and is centered
        Vector3 offset = new Vector3(
            -bounds.center.x,
            -bounds.min.y,
            -bounds.center.z
        );

        building.transform.position = offset;
        Debug.Log("Building centered. Size: " + bounds.size);
    }

    // ─────────────────────────────────────────────────────────
    // UI HELPERS
    // ─────────────────────────────────────────────────────────
    void ShowStatus(string message)
    {
        Debug.Log(message);
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (statusText != null) statusText.text = message;
    }

    void HideStatus()
    {
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }
}
