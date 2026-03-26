using System.Collections;
using UnityEngine;
using TMPro;

// ─────────────────────────────────────────────────────────────
// ElementInspector.cs
// When user points controller at a wall and pulls trigger,
// this script shoots a ray, gets the element's Express ID,
// calls Flask /element-info endpoint, and shows a floating
// info panel in VR with the IFC properties.
//
// Attach to an empty GameObject called "ElementInspector"
// ─────────────────────────────────────────────────────────────

public class ElementInspector : MonoBehaviour
{
    // ── REFERENCES ────────────────────────────────────────────
    [Header("Info Panel")]
    public GameObject infoPanel;           // floating panel prefab
    public TextMeshProUGUI titleText;      // element type e.g. "Wall"
    public TextMeshProUGUI detailText;     // properties list
    public TextMeshProUGUI roomText;       // which room it belongs to

    [Header("Highlight Material")]
    public Material highlightMaterial;     // green highlight material
    public Material defaultMaterial;       // original material to restore

    // ── PRIVATE ───────────────────────────────────────────────
    private GameObject lastHighlighted;
    private Material lastOriginalMaterial;
    private bool isPanelVisible = false;

    void Start()
    {
        // Hide panel at start
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    // CALLED BY CONTROLLER
    // Call this when user pulls trigger
    // Pass in the ray origin and direction from the controller
    // ─────────────────────────────────────────────────────────
    public void InspectAtRay(Ray controllerRay)
    {
        RaycastHit hit;

        if (Physics.Raycast(controllerRay, out hit, 100f))
        {
            GameObject hitObject = hit.collider.gameObject;

            // Highlight the hit object
            HighlightObject(hitObject);

            // Get Express ID from object name
            // GLTFUtility names meshes by their IFC Express ID
            string objectName = hitObject.name;
            Debug.Log("Hit object: " + objectName);

            // Try to parse Express ID from name
            // IFC elements are named like "Wall_123456" or just "123456"
            int expressId = ParseExpressId(objectName);

            if (expressId > 0)
            {
                // Show loading state
                ShowPanel("Loading...", "Fetching data from BIM Nexus...", "");

                // Fetch from Flask
                StartCoroutine(
                    APIManager.Instance.GetElementInfo(
                        expressId,
                        onSuccess: (json) => { DisplayElementInfo(json); },
                        onError:   (err)  => { ShowPanel("Error", err, ""); }
                    )
                );
            }
            else
            {
                // No Express ID — show basic info
                ShowPanel(objectName, "No IFC data available", "");
            }

            // Position panel near hit point
            if (infoPanel != null)
            {
                infoPanel.transform.position = hit.point + hit.normal * 0.5f;
                infoPanel.transform.LookAt(Camera.main.transform);
                infoPanel.transform.Rotate(0, 180, 0);
            }
        }
        else
        {
            // Nothing hit — hide panel
            HidePanel();
            RemoveHighlight();
        }
    }

    // ─────────────────────────────────────────────────────────
    // DISPLAY ELEMENT INFO
    // Parses the JSON from Flask and shows it in the panel
    // ─────────────────────────────────────────────────────────
    void DisplayElementInfo(string json)
    {
        // Parse JSON manually (no external library needed)
        // Flask returns: {"status":"success","element_type":"Wall","data":{...}}
        try
        {
            ElementInfoResponse response = JsonUtility.FromJson<ElementInfoResponse>(json);

            if (response.status == "success")
            {
                string title = response.element_type;
                string details = "";
                string room = "";

                // Build details string from data
                if (response.element_type == "Wall")
                {
                    WallData data = JsonUtility.FromJson<WallData>(
                        ExtractDataJson(json)
                    );
                    details = "Name: " + data.name +
                              "\nMaterial: " + data.material +
                              "\nExternal: " + (data.is_external ? "Yes" : "No");

                    if (data.rooms != null && data.rooms.Length > 0)
                        room = "Room: " + data.rooms[0].room;
                }
                else if (response.element_type == "Door")
                {
                    DoorData data = JsonUtility.FromJson<DoorData>(
                        ExtractDataJson(json)
                    );
                    details = "Name: " + data.name +
                              "\nWidth: " + data.width + "m" +
                              "\nHeight: " + data.height + "m";
                }
                else
                {
                    details = "Type: " + response.element_type;
                }

                ShowPanel(title, details, room);
            }
            else
            {
                ShowPanel("Not found", "Element not in knowledge graph", "");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("JSON parse error: " + e.Message);
            ShowPanel("Error", "Could not parse response", "");
        }
    }

    // ─────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────
    int ParseExpressId(string name)
    {
        // Try last part after underscore: "Wall_123456" → 123456
        string[] parts = name.Split('_');
        foreach (string part in parts)
        {
            int id;
            if (int.TryParse(part, out id) && id > 0)
                return id;
        }
        // Try whole name as number
        int directId;
        if (int.TryParse(name, out directId))
            return directId;
        return -1;
    }

    string ExtractDataJson(string fullJson)
    {
        int dataStart = fullJson.IndexOf("\"data\":");
        if (dataStart < 0) return "{}";
        dataStart += 7;
        return fullJson.Substring(dataStart, fullJson.Length - dataStart - 1);
    }

    void ShowPanel(string title, string details, string room)
    {
        if (infoPanel != null) infoPanel.SetActive(true);
        if (titleText != null) titleText.text = title;
        if (detailText != null) detailText.text = details;
        if (roomText != null) roomText.text = room;
        isPanelVisible = true;
    }

    void HidePanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        isPanelVisible = false;
    }

    void HighlightObject(GameObject obj)
    {
        RemoveHighlight();
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null && highlightMaterial != null)
        {
            lastHighlighted = obj;
            lastOriginalMaterial = rend.material;
            rend.material = highlightMaterial;
        }
    }

    void RemoveHighlight()
    {
        if (lastHighlighted != null)
        {
            Renderer rend = lastHighlighted.GetComponent<Renderer>();
            if (rend != null && lastOriginalMaterial != null)
                rend.material = lastOriginalMaterial;
            lastHighlighted = null;
        }
    }

    // ─────────────────────────────────────────────────────────
    // JSON DATA CLASSES
    // ─────────────────────────────────────────────────────────
    [System.Serializable]
    public class ElementInfoResponse
    {
        public string status;
        public string element_type;
    }

    [System.Serializable]
    public class WallData
    {
        public string name;
        public string material;
        public bool is_external;
        public RoomData[] rooms;
    }

    [System.Serializable]
    public class RoomData
    {
        public string room;
        public float area;
    }

    [System.Serializable]
    public class DoorData
    {
        public string name;
        public float width;
        public float height;
        public float area;
    }
}
