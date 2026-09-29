using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerVisualAuthoring
{
    private const string Folder = "Assets/source/Player";
    private const string ClipFolder = Folder + "/Animations";
    private const string BodyPath = Folder + "/player_sp.aseprite";
    private const string HandPath = Folder + "/playerhand_sp.aseprite";
    private const string ControllerPath = ClipFolder + "/PlayerCharacter.controller";
    private const string HandObjectName = "PlayerHand";

    [MenuItem("Tools/Player/Build Player Visuals")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!AssetDatabase.IsValidFolder(ClipFolder))
            AssetDatabase.CreateFolder(Folder, "Animations");

        var bodyAssets = AssetDatabase.LoadAllAssetsAtPath(BodyPath);
        var handAssets = AssetDatabase.LoadAllAssetsAtPath(HandPath);
        var idleSprite = bodyAssets.OfType<Sprite>().Single(x => x.name == "Frame_0");
        var idleHand = handAssets.OfType<Sprite>().Single(x => x.name == "Frame_0");

        var idle = SaveClip("Idle", Merge(bodyAssets, handAssets, "idle", true));
        var walk = SaveClip("Walk", Merge(bodyAssets, handAssets, "walk", true));
        var run = SaveClip("Run", Merge(bodyAssets, handAssets, "run", true));
        var attack = SaveClip("Attack", Merge(bodyAssets, handAssets, "attack", false));
        var jump = SaveClip("Jump", Static(idleSprite, idleHand, 0.12f, true));
        var fall = SaveClip("Fall", Static(idleSprite, idleHand, 0.12f, true));
        var hurt = SaveClip("Hurt", Static(idleSprite, idleHand, 0.20f, false));
        var death = SaveClip("Death", Static(idleSprite, idleHand, 0.60f, false));

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var child in machine.states.ToArray())
            machine.RemoveState(child.state);
        var defaultState = machine.AddState("Idle");
        defaultState.motion = idle;
        machine.defaultState = defaultState;
        Add(machine, "Walk", walk);
        Add(machine, "Run", run);
        Add(machine, "jump", jump);
        Add(machine, "Fall", fall);
        Add(machine, "Dash NoDust", run);
        Add(machine, "Attack", attack);
        Add(machine, "Hurt NoEffect", hurt);
        Add(machine, "Death NoEffect", death);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        foreach (var scenePath in new[]
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/JinHo/Gathering/Scenes/FieldMapStructureTest.unity",
            "Assets/Campaign/Scenes/NotionCampaign.unity"
        })
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var player = scene.GetRootGameObjects().Single(x => x.name == "player");
            var bodyRenderer = player.GetComponent<SpriteRenderer>();
            var animator = player.GetComponent<Animator>();
            var movement = player.GetComponent<PlayerMovement>();
            var animation = player.GetComponent<PlayerAnimationController>();
            if (!bodyRenderer || !animator || !movement || !animation)
                throw new InvalidOperationException("Missing player visual components in " + scenePath);

            animator.runtimeAnimatorController = controller;
            var serializedBody = new SerializedObject(bodyRenderer);
            serializedBody.FindProperty("m_Sprite").objectReferenceValue = idleSprite;
            serializedBody.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bodyRenderer);

            var hand = player.transform.Find(HandObjectName);
            if (!hand)
            {
                var handObject = new GameObject(HandObjectName);
                hand = handObject.transform;
                hand.SetParent(player.transform, false);
            }
            hand.localPosition = Vector3.zero;
            hand.localRotation = Quaternion.identity;
            hand.localScale = Vector3.one;
            var handRenderer = hand.GetComponent<SpriteRenderer>();
            if (!handRenderer) handRenderer = hand.gameObject.AddComponent<SpriteRenderer>();
            handRenderer.sprite = idleHand;
            handRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            handRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
            handRenderer.flipX = bodyRenderer.flipX;

            var serializedMovement = new SerializedObject(movement);
            serializedMovement.FindProperty("warriorAnimatorController").objectReferenceValue = controller;
            serializedMovement.ApplyModifiedPropertiesWithoutUndo();
            var serializedAnimation = new SerializedObject(animation);
            serializedAnimation.FindProperty("handRenderer").objectReferenceValue = handRenderer;
            serializedAnimation.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("C029/build-result.txt", "Player visuals authored in 3 scenes; controller=" + ControllerPath);
    }

    private static void Add(AnimatorStateMachine machine, string name, Motion motion)
    {
        var state = machine.AddState(name);
        state.motion = motion;
    }

    private static AnimationClip SaveClip(string name, AnimationClip clip)
    {
        var path = ClipFolder + "/" + name + ".anim";
        clip.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        EditorUtility.CopySerialized(clip, existing);
        UnityEngine.Object.DestroyImmediate(clip);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static AnimationClip Merge(UnityEngine.Object[] bodyAssets, UnityEngine.Object[] handAssets, string tag, bool loop)
    {
        var body = bodyAssets.OfType<AnimationClip>().Single(x => x.name == tag);
        var hand = handAssets.OfType<AnimationClip>().Single(x => x.name == tag);
        var clip = new AnimationClip { frameRate = body.frameRate };
        CopySpriteCurve(body, clip, "");
        CopySpriteCurve(hand, clip, HandObjectName);
        var settings = AnimationUtility.GetAnimationClipSettings(body);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void CopySpriteCurve(AnimationClip source, AnimationClip target, string path)
    {
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(source)
            .Single(x => x.propertyName == "m_Sprite" && x.type == typeof(SpriteRenderer));
        var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
        AnimationUtility.SetObjectReferenceCurve(target,
            EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"), keys);
    }

    private static AnimationClip Static(Sprite body, Sprite hand, float duration, bool loop)
    {
        var clip = new AnimationClip { frameRate = 100f };
        SetStaticCurve(clip, "", body, duration);
        SetStaticCurve(clip, HandObjectName, hand, duration);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SetStaticCurve(AnimationClip clip, string path, Sprite sprite, float duration)
    {
        var binding = EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = sprite },
            new ObjectReferenceKeyframe { time = duration, value = sprite }
        });
    }
}
