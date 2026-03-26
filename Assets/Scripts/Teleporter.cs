using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────
// Teleporter.cs
// Lets user point controller at floor and press trigger to jump there.
// Uses Meta XR SDK input system — works reliably on Quest 2 and 3.
//
// HOW IT WORKS:
// Every frame → shoot ray from right controller
// If ray hits floor → show green circle marker
// When trigger pressed → move player to that position
//
// Attach to an empty GameObject called "Teleporter"
// ─────────────────────────────────────────────────────────────

public class Teleporter : MonoBehaviour
{
    // ── REFERENCES ────────────────────────────────────────────
    [Header("References")]
    public GameObject teleportMarker;     // green circle on floor
    public Transform cameraRig;           // the OVRCameraRig transform

    [Header("Settings")]
    public float maxDistance = 20f;       // max teleport distance
    public LayerMask floorLayer;          // which layers count as floor

    // ── PRIVATE ───────────────────────────────────────────────
    private Vector3 teleportTarget;
    private bool hasTarget = false;
    private OVRInput.Controller activeController = OVRInput.Controller.RTouch;

    void Start()
    {
        // Hide marker at start
        if (teleportMarker != null)
            teleportMarker.SetActive(false);

        // Auto-find camera rig if not assigned
        if (cameraRig == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null) cameraRig = rig.transform;
        }
    }

    void Update()
    {
        UpdateTeleportRay();
        CheckTeleportInput();
    }

    // ─────────────────────────────────────────────────────────
    // RAY FROM CONTROLLER
    // Shoots ray from right controller every frame
    // Shows green marker where it hits the floor
    // ─────────────────────────────────────────────────────────
    void UpdateTeleportRay()
    {
        // Get right controller position and rotation
        Vector3 controllerPos = OVRInput.GetLocalControllerPosition(activeController);
        Quaternion controllerRot = OVRInput.GetLocalControllerRotation(activeController);

        // Convert to world space using camera rig
        if (cameraRig != null)
        {
            controllerPos = cameraRig.TransformPoint(controllerPos);
            controllerRot = cameraRig.rotation * controllerRot;
        }

        // Direction controller is pointing
        Vector3 rayDirection = controllerRot * Vector3.forward;

        // Shoot ray
        Ray ray = new Ray(controllerPos, rayDirection);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, floorLayer))
        {
            // Hit the floor — show marker
            teleportTarget = hit.point;
            hasTarget = true;

            if (teleportMarker != null)
            {
                teleportMarker.SetActive(true);
                teleportMarker.transform.position = new Vector3(
                    hit.point.x,
                    hit.point.y + 0.02f,  // slightly above floor
                    hit.point.z
                );
            }
        }
        else
        {
            // No floor hit — hide marker
            hasTarget = false;
            if (teleportMarker != null)
                teleportMarker.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────
    // TRIGGER INPUT
    // When user releases trigger → teleport to target
    // Using button UP (release) feels more natural than press
    // ─────────────────────────────────────────────────────────
    void CheckTeleportInput()
    {
        // Right trigger released
        if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, activeController))
        {
            if (hasTarget)
            {
                DoTeleport();
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // DO TELEPORT
    // Moves the camera rig to the target position
    // ─────────────────────────────────────────────────────────
    void DoTeleport()
    {
        if (cameraRig == null) return;

        // Move rig to target — keep Y at 0 (floor level)
        cameraRig.position = new Vector3(
            teleportTarget.x,
            0f,
            teleportTarget.z
        );

        // Hide marker after teleport
        if (teleportMarker != null)
            teleportMarker.SetActive(false);

        hasTarget = false;

        Debug.Log("Teleported to: " + teleportTarget);
    }
}
