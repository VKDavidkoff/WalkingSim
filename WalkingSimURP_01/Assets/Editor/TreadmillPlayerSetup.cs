using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Rebuilds the treadmill player rig from the original TreadmillTestGame
// ForestLake_Env scene inside whichever scene is open: an empty "PlayerTreadmill"
// parent holding the capsule (CharacterController + PlayerMovement), the follow
// camera, and the TreadmillClient. Values below are copied from that scene,
// including the parent's 90-degree yaw and the speed multiplier tuned to 3.
public static class TreadmillPlayerSetup
{
    const string RigName = "PlayerTreadmill";

    // Same spawn point as the old project; the forest environment uses the same
    // world coordinates in both.
    static readonly Vector3 RigPosition = new Vector3(1400.63f, 70.03f, 1402.34f);
    static readonly Quaternion RigRotation = Quaternion.Euler(0f, 90f, 0f);

    [MenuItem("Tools/Treadmill/Add Treadmill Player To Open Scene")]
    public static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("No scene open", "Open the scene you want the player added to first.", "OK");
            return;
        }

        if (Object.FindObjectsOfType<TreadmillClient>(true).Length > 0)
        {
            EditorUtility.DisplayDialog("Already in this scene",
                "This scene already contains a TreadmillClient, so the treadmill player was not added again.", "OK");
            return;
        }

        GameObject player = AddTreadmillPlayer(out Camera newCamera);

        Camera[] others = Object.FindObjectsOfType<Camera>()
            .Where(c => c != newCamera && c.gameObject.activeInHierarchy && c.enabled)
            .ToArray();

        if (others.Length > 0)
        {
            string names = string.Join(", ", others.Select(c => $"'{c.name}'"));
            bool disable = EditorUtility.DisplayDialog("Other active camera found",
                $"This scene already has {others.Length} active camera(s): {names}.\n\n" +
                "Two active cameras (and two audio listeners) fight over the screen. " +
                "Disable them? They are only deactivated, not deleted.",
                "Disable them", "Leave them");

            if (disable)
            {
                foreach (Camera other in others)
                {
                    Undo.RecordObject(other.gameObject, "Disable other camera");
                    other.gameObject.SetActive(false);
                }
            }
        }

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[TreadmillPlayerSetup] Added PlayerTreadmill rig. Save the scene (Ctrl+S) to keep it.");
    }

    // Core of the work, kept free of dialogs so it can also be run unattended.
    public static GameObject AddTreadmillPlayer(out Camera camera)
    {
        GameObject rig = new GameObject(RigName);
        rig.transform.SetPositionAndRotation(RigPosition, RigRotation);
        Undo.RegisterCreatedObjectUndo(rig, "Add Treadmill Player");

        // Player capsule. CreatePrimitive in a URP project assigns the URP Lit material,
        // unlike the old project's Standard one. The default CapsuleCollider is removed so
        // the CharacterController is the only collider, as in the old scene.
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player_Tread";
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        player.transform.SetParent(rig.transform, false);
        player.transform.localPosition = new Vector3(0f, 1f, -32.2f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;
        controller.minMoveDistance = 0.001f;

        PlayerMovement movement = player.AddComponent<PlayerMovement>();
        movement.moveSpeed = 4f;
        movement.turnSpeedDegPerSec = 120f;
        movement.gravity = -9.81f;
        movement.useTreadmillInput = true;
        movement.treadmillSpeedMultiplier = 3f;

        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(rig.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 4f, -37.2f);
        camera = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();

        // Match the forest scene's own camera so the player view keeps the same look:
        // post-processing volumes on, SMAA anti-aliasing.
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        SimpleFollowCamera follow = camGO.AddComponent<SimpleFollowCamera>();
        follow.target = player.transform;
        follow.offset = new Vector3(0f, 3f, -5f);
        follow.smoothTime = 0.15f;

        GameObject clientGO = new GameObject("TreadmillClient");
        clientGO.transform.SetParent(rig.transform, false);
        clientGO.transform.localPosition = new Vector3(0f, 0f, -32.2f);
        clientGO.AddComponent<TreadmillClient>();

        return player;
    }
}
