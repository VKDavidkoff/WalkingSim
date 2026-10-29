using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click A/B comparison of player models. Swaps the visible character under
// Player_Tread while keeping everything that makes it work: the WalkingSim_Anim
// controller, root motion off, the PlayerAnimationDriver link, and the position
// offset that puts the feet on the ground.
//
// The point: Kevin Iglesias authored these animations on his own "Human Character
// Dummy". Seeing them on the dummy shows how they are *meant* to look, which
// separates "the animation setup is off" from "this model's proportions don't
// retarget well".
public static class PlayerModelSwitcher
{
    const string DummyFemalePrefab = "Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_F White.prefab";
    const string StylizedFemalePrefab = "Assets/STYLIZED FEMALE - SPORTY/Prefabs/Combined/COMBINED_1.prefab";
    const string ControllerPath = "Assets/WalkingSim_Anim.controller";

    // Used when there is no existing model to copy the offset from.
    static readonly Vector3 DefaultOffset = new Vector3(0f, -1.129f, 0f);

    [MenuItem("Tools/Treadmill/Player Model/Iglesias Dummy (female)")]
    static void UseDummy() => RunFromMenu(DummyFemalePrefab);

    [MenuItem("Tools/Treadmill/Player Model/Stylized Female (COMBINED_1)")]
    static void UseStylized() => RunFromMenu(StylizedFemalePrefab);

    static void RunFromMenu(string prefabPath)
    {
        string problem = SwitchTo(prefabPath, out _);
        if (problem != null) EditorUtility.DisplayDialog("Couldn't switch the model", problem, "OK");
    }

    // Returns null on success, or a plain-English reason it couldn't.
    public static string SwitchTo(string prefabPath, out GameObject newModel)
    {
        newModel = null;

        GameObject player = GameObject.Find("Player_Tread");
        if (player == null)
        {
            return "There's no 'Player_Tread' in the open scene. Run Tools > Treadmill > Add Treadmill Player To Open Scene first.";
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return $"Couldn't find the model at:\n{prefabPath}";
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            return $"Couldn't find {ControllerPath}. Run Tools > Treadmill > Set Up Player Animation first.";
        }

        // Only the visible character is replaced: direct children that hold an Animator.
        var oldModels = new List<Transform>();
        foreach (Transform child in player.transform)
        {
            if (child.GetComponentInChildren<Animator>(true) != null) oldModels.Add(child);
        }

        Vector3 offset = DefaultOffset;
        Quaternion rotation = Quaternion.identity;
        if (oldModels.Count > 0)
        {
            offset = oldModels[0].localPosition;
            rotation = oldModels[0].localRotation;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Object.DestroyImmediate(instance);
            return "That model has no Animator, so it can't play the animations.";
        }
        Undo.RegisterCreatedObjectUndo(instance, "Switch player model");

        instance.transform.localPosition = offset;
        instance.transform.localRotation = rotation;

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // the CharacterController moves the player, not the animation

        foreach (Transform old in oldModels) Undo.DestroyObjectImmediate(old.gameObject);

        PlayerAnimationDriver driver = player.GetComponent<PlayerAnimationDriver>();
        if (driver == null) driver = Undo.AddComponent<PlayerAnimationDriver>(player);
        Undo.RecordObject(driver, "Switch player model");
        driver.animator = animator;
        EditorUtility.SetDirty(driver);

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(player.scene);
        newModel = instance;
        return null;
    }
}
