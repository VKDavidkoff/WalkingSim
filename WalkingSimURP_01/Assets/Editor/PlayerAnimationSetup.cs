using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Fills in the WalkingSim_Anim controller and connects it to the player:
//   1. Gives the controller a float parameter called "Speed" (renaming the "Blend"
//      one Unity auto-created, if that's what is there) and builds the Blend Tree.
//   2. Points the model's Animator at the controller, with root motion off,
//      and adds PlayerAnimationDriver, which feeds "Speed" at runtime.
public static class PlayerAnimationSetup
{
    const string ControllerPath = "Assets/WalkingSim_Anim.controller";
    const string ClipRoot = "Assets/Kevin Iglesias/Human Animations/Animations/Female/";
    public const string SpeedParameter = "Speed";

    // Blend points are each clip's own natural ground speed in m/s, measured from
    // Iglesias's root-motion ("[RM]") versions of the same clips. Matching them
    // keeps the feet from sliding. The Blend Tree's thresholds stay editable in the
    // Inspector afterwards if you want a different feel.
    static readonly (string path, float threshold)[] Motions =
    {
        ("Idles/HumanF@Idle01.fbx",                    0.00f),
        ("Movement/Walk/HumanF@Walk01_Forward.fbx",    1.94f),
        ("Movement/Run/HumanF@Run01_Forward.fbx",      3.87f),
        ("Movement/Sprint/HumanF@Sprint01_Forward.fbx", 5.81f),
    };

    [MenuItem("Tools/Treadmill/Set Up Player Animation")]
    public static void SetUpFromMenu()
    {
        AnimatorController controller = BuildBlendTree(out string problem);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Couldn't build the Blend Tree", problem, "OK");
            return;
        }

        string hookProblem = HookUpToScene(controller);
        EditorUtility.DisplayDialog("Player animation",
            "WalkingSim_Anim now has a float parameter 'Speed' and a Blend Tree: " +
            "Idle -> Walk -> Run -> Sprint.\n\n" +
            (hookProblem == null
                ? "The model's Animator is connected to it, and PlayerAnimationDriver was added to Player_Tread. Save the scene (Ctrl+S) and press Play."
                : "Not connected to the scene yet: " + hookProblem),
            "OK");
    }

    // Returns null (and explains why in 'problem') without touching the controller if anything is missing.
    public static AnimatorController BuildBlendTree(out string problem)
    {
        problem = null;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            problem = $"Couldn't find {ControllerPath}.";
            return null;
        }

        // Load every clip first so a missing file doesn't leave the controller half-built.
        var clips = new AnimationClip[Motions.Length];
        for (int i = 0; i < Motions.Length; i++)
        {
            clips[i] = LoadClip(ClipRoot + Motions[i].path);
            if (clips[i] == null)
            {
                problem = $"Couldn't find the animation clip at:\n{ClipRoot + Motions[i].path}";
                return null;
            }
        }

        // The float parameter. A fresh Blend Tree creates one called "Blend"; reuse it under the clearer name.
        AnimatorControllerParameter[] parameters = controller.parameters;
        if (!parameters.Any(p => p.name == SpeedParameter))
        {
            int blendIndex = Array.FindIndex(parameters, p => p.name == "Blend" && p.type == AnimatorControllerParameterType.Float);
            if (blendIndex >= 0)
            {
                parameters[blendIndex].name = SpeedParameter;
                controller.parameters = parameters;
            }
            else
            {
                controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);
            }
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        BlendTree tree = null;
        AnimatorState state = null;
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.motion is BlendTree existing)
            {
                tree = existing;
                state = child.state;
                break;
            }
        }
        if (tree == null)
        {
            state = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
        }

        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = SpeedParameter;
        tree.useAutomaticThresholds = false;
        for (int i = tree.children.Length - 1; i >= 0; i--) tree.RemoveChild(i);
        for (int i = 0; i < Motions.Length; i++) tree.AddChild(clips[i], Motions[i].threshold);

        if (stateMachine.defaultState == null) stateMachine.defaultState = state;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // Returns null on success, or a plain-English reason it couldn't connect things.
    public static string HookUpToScene(AnimatorController controller)
    {
        GameObject player = GameObject.Find("Player_Tread");
        if (player == null)
        {
            return "there's no 'Player_Tread' in the open scene. Run Tools > Treadmill > Add Treadmill Player To Open Scene first.";
        }

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return "there's no Animator under Player_Tread. Drag the female model (e.g. COMBINED_1) onto Player_Tread first.";
        }

        Undo.RecordObject(animator, "Assign WalkingSim_Anim");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // the CharacterController moves the player, not the animation
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorUtility.SetDirty(animator);

        PlayerAnimationDriver driver = player.GetComponent<PlayerAnimationDriver>();
        if (driver == null) driver = Undo.AddComponent<PlayerAnimationDriver>(player);
        Undo.RecordObject(driver, "Configure PlayerAnimationDriver");
        driver.animator = animator;
        driver.speedParameter = SpeedParameter;
        EditorUtility.SetDirty(driver);

        EditorSceneManager.MarkSceneDirty(player.scene);
        return null;
    }

    // Each Iglesias .fbx holds the real clip plus a "__preview__" copy; skip the preview.
    static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }
}
