#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Menu: Tools > Creep > Animator Builder
/// Ek click mein: FBX ka Rig (Generic + Avatar), Loop Time, Animator Controller
/// (Speed/Attack parameters, Idle/Walk/Run/Attack states, saari transitions) bana ke creep pe laga deta hai.
/// Ye file 'Editor' naam ke folder mein honi chahiye.
/// </summary>
public class CreepAnimatorBuilder : EditorWindow
{
    GameObject fbx;          // Project wali FBX (animations wali)
    GameObject target;       // Scene wala creep
    AnimationClip idleClip, walkClip, runClip, attackClip;
    string controllerName = "CreepAnimator_Auto";
    float walkRunThreshold = 4f;
    Vector2 scroll;

    [MenuItem("Tools/Creep/Animator Builder")]
    static void Open() => GetWindow<CreepAnimatorBuilder>("Creep Animator");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox("1) Animations wali FBX daalo\n2) 'Auto Detect' dabao\n3) Clips check karo\n4) Scene ka creep daalo\n5) BUILD", MessageType.Info);

        EditorGUILayout.LabelField("1. FBX (Project window se)", EditorStyles.boldLabel);
        fbx = (GameObject)EditorGUILayout.ObjectField("Animation FBX", fbx, typeof(GameObject), false);

        GUI.enabled = fbx;
        if (GUILayout.Button("Rig Fix + Auto Detect Clips", GUILayout.Height(28))) FixRigAndDetect();
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Clips (check / badlo)", EditorStyles.boldLabel);
        idleClip = Clip("Idle", idleClip);
        walkClip = Clip("Walk", walkClip);
        runClip = Clip("Run (khali = Walk tez)", runClip);
        attackClip = Clip("Attack", attackClip);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Settings", EditorStyles.boldLabel);
        controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
        walkRunThreshold = EditorGUILayout.FloatField("Walk -> Run at Speed >", walkRunThreshold);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Scene ka Creep", EditorStyles.boldLabel);
        target = (GameObject)EditorGUILayout.ObjectField("Creep (Hierarchy)", target, typeof(GameObject), true);
        if (!target && Selection.activeGameObject && !EditorUtility.IsPersistent(Selection.activeGameObject))
            if (GUILayout.Button($"Use selected: {Selection.activeGameObject.name}")) target = Selection.activeGameObject;

        EditorGUILayout.Space();
        GUI.enabled = fbx && idleClip && walkClip && attackClip;
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("BUILD ANIMATOR", GUILayout.Height(40))) Build();
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    AnimationClip Clip(string label, AnimationClip c) =>
        (AnimationClip)EditorGUILayout.ObjectField(label, c, typeof(AnimationClip), false);

    // ================= RIG + DETECT =================
    void FixRigAndDetect()
    {
        string path = AssetDatabase.GetAssetPath(fbx);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (!importer) { EditorUtility.DisplayDialog("Creep", "Ye FBX/model file nahi hai", "OK"); return; }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }
        if (importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }
        if (changed) { importer.SaveAndReimport(); Debug.Log("[CreepBuilder] Rig -> Generic + Avatar"); }

        var clips = LoadClips(path);
        idleClip = Find(clips, "idle2", "idle1", "idle");
        walkClip = Find(clips, "walk1", "walk");
        runClip = Find(clips, "run", "crouch", "walk2", "slide");
        attackClip = Find(clips, "bite", "attack", "punch");

        Debug.Log($"[CreepBuilder] Clips mile: {clips.Count}\n" + string.Join("\n", clips.Select(c => "  - " + c.name)));
    }

    static List<AnimationClip> LoadClips(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();

    static AnimationClip Find(List<AnimationClip> clips, params string[] keys)
    {
        foreach (var k in keys)
        {
            var c = clips.FirstOrDefault(x => x.name.ToLower().Contains(k));
            if (c) return c;
        }
        return null;
    }

    // ================= LOOP TIME =================
    void SetLoops(string fbxPath, HashSet<string> loopNames, string noLoopName)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (!importer) return;

        var anims = importer.clipAnimations;
        if (anims == null || anims.Length == 0) anims = importer.defaultClipAnimations;

        bool changed = false;
        foreach (var a in anims)
        {
            bool wantLoop = loopNames.Contains(a.name);
            if (a.name == noLoopName) wantLoop = false;
            if (!loopNames.Contains(a.name) && a.name != noLoopName) continue;
            if (a.loopTime != wantLoop) { a.loopTime = wantLoop; changed = true; }
        }
        if (changed)
        {
            importer.clipAnimations = anims;
            importer.SaveAndReimport();
            Debug.Log("[CreepBuilder] Loop Time set: " + string.Join(", ", loopNames));
        }
    }

    // ================= BUILD =================
    void Build()
    {
        string fbxPath = AssetDatabase.GetAssetPath(fbx);

        // Clip naam yaad rakho (reimport ke baad dobara load karne ke liye)
        string idleN = idleClip.name, walkN = walkClip.name, runN = runClip ? runClip.name : null, atkN = attackClip.name;

        // 1) Loop Time (sirf agar clips isi FBX ke hain)
        if (AssetDatabase.GetAssetPath(idleClip) == fbxPath)
        {
            var loops = new HashSet<string> { idleN, walkN };
            if (runN != null) loops.Add(runN);
            SetLoops(fbxPath, loops, atkN);

            var clips = LoadClips(fbxPath);
            idleClip = clips.FirstOrDefault(c => c.name == idleN) ?? idleClip;
            walkClip = clips.FirstOrDefault(c => c.name == walkN) ?? walkClip;
            if (runN != null) runClip = clips.FirstOrDefault(c => c.name == runN) ?? runClip;
            attackClip = clips.FirstOrDefault(c => c.name == atkN) ?? attackClip;
        }

        // 2) Controller banao
        string dir = Path.GetDirectoryName(fbxPath).Replace("\\", "/");
        string outPath = $"{dir}/{controllerName}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outPath))
        {
            if (!EditorUtility.DisplayDialog("Creep", $"'{controllerName}' pehle se hai. Overwrite karun?", "Haan", "Nahi")) return;
            AssetDatabase.DeleteAsset(outPath);
        }

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(outPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        sm.entryPosition = new Vector3(0, 0);
        sm.anyStatePosition = new Vector3(0, -120);
        sm.exitPosition = new Vector3(900, 0);

        var idle = sm.AddState("Idle", new Vector3(250, 0));
        idle.motion = idleClip;
        var walk = sm.AddState("Walk", new Vector3(500, -100));
        walk.motion = walkClip;
        var run = sm.AddState("Run", new Vector3(500, 100));
        run.motion = runClip ? runClip : walkClip;
        if (!runClip) run.speed = 1.8f;   // run clip nahi -> walk tez chalao
        var attack = sm.AddState("Attack", new Vector3(250, -220));
        attack.motion = attackClip;
        sm.defaultState = idle;

        // 3) Transitions
        T(idle, walk, AnimatorConditionMode.Greater, 0.1f);
        T(walk, idle, AnimatorConditionMode.Less, 0.1f);
        T(walk, run, AnimatorConditionMode.Greater, walkRunThreshold);
        T(run, walk, AnimatorConditionMode.Less, walkRunThreshold);
        T(idle, run, AnimatorConditionMode.Greater, walkRunThreshold);
        T(run, idle, AnimatorConditionMode.Less, 0.1f);

        var toAtk = sm.AddAnyStateTransition(attack);
        toAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        toAtk.hasExitTime = false;
        toAtk.duration = 0.1f;
        toAtk.canTransitionToSelf = false;

        var back = attack.AddTransition(idle);
        back.hasExitTime = true;
        back.exitTime = 0.9f;
        back.duration = 0.15f;

        AssetDatabase.SaveAssets();
        Debug.Log($"[CreepBuilder] Controller bana: {outPath}");

        // 4) Creep pe lagao
        if (target) Apply(target, ctrl, fbxPath);

        EditorGUIUtility.PingObject(ctrl);
        EditorUtility.DisplayDialog("Creep", "Animator ban gaya!" + (target ? $"\n'{target.name}' pe laga diya." : "\nAb creep ke Animator mein laga do."), "OK");
    }

    static void T(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(mode, threshold, "Speed");
    }

    static void Apply(GameObject go, AnimatorController ctrl, string fbxPath)
    {
        Undo.RegisterFullObjectHierarchyUndo(go, "Creep Animator");

        // Extra animators hatao (root pe ek rahe)
        var anims = go.GetComponentsInChildren<Animator>(true);
        Animator main = go.GetComponent<Animator>();
        foreach (var a in anims)
        {
            if (a == main) continue;
            if (!main) { main = a; continue; }
            Debug.Log($"[CreepBuilder] Extra Animator hataya: {a.gameObject.name}");
            Undo.DestroyObjectImmediate(a);
        }
        if (!main) main = Undo.AddComponent<Animator>(go);

        main.runtimeAnimatorController = ctrl;
        main.applyRootMotion = false;
        main.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
        if (avatar) main.avatar = avatar;
        else Debug.LogWarning("[CreepBuilder] Avatar nahi mila - 'Rig Fix + Auto Detect' dabao");

        // CreepPatrolAI ko bhi bata do
        var ai = go.GetComponent<CreepPatrolAI>();
        if (ai)
        {
            Undo.RecordObject(ai, "Creep AI");
            ai.animator = main;
            ai.speedParam = "Speed";
            ai.attackTrigger = "Attack";
            EditorUtility.SetDirty(ai);
        }

        EditorUtility.SetDirty(main);
        Debug.Log($"[CreepBuilder] '{go.name}' pe laga: Controller {ctrl.name}, Avatar {(avatar ? avatar.name : "NONE")}");
    }
}
#endif
