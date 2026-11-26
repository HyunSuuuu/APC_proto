using APC.Characters;
using APC.Logistics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// 캐릭터 모델 · 애니 적용 (`Assets/Art/Model/Worker` · `Ani`).
    /// 재질(모델별 `*_P.psd`) · 애니 반복 · 공용 Animator Controller(idle / walk / carry / pull)를 준비하고,
    /// 씬의 플레이어에 모델을 붙인다. 알바는 `StandardModules.StaffWorkerPrefab`이 `Model`로 모델을 넣는다.
    /// </summary>
    public static class CharacterArt
    {
        const string ModelFolder = "Assets/Art/Model/Worker";
        const string AniFolder = "Assets/Art/Model/Ani";
        const string ControllerPath = AniFolder + "/Character.controller";
        const string MaterialFolder = "Assets/Art/Materials";

        public const string Player = "player_SBS"; // 2026-11-18 플레이어 아바타 교체 (옛 player.fbx)
        /// <summary>알바 모델. [0] = 물류 유니폼, 나머지 = 코스튬 (GDD 11.1, StaffWorker.models 순서).</summary>
        public static readonly string[] Workers = { "uniformT", "worker_macaron", "worker_rabbit" }; // SBS는 플레이어로 옮김 (2026-11-18)
        static readonly string[] States = { "idle", "walk", "carry_idle", "carry_move", "push_wagon_idle", "push_wagon", "push_rolltainer_idle", "push_rolltainer", "riding" };

        [MenuItem("APC/Art/Apply Character Models (플레이어 · 알바)")]
        static void Apply()
        {
            PrepareAssets();
            ApplyPlayer();
            StandardModules.StaffWorkerPrefab(overwrite: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterArt] 플레이어 모델 · 알바 프리팹 적용 완료");
        }

        /// <summary>애니 반복 · 아바타 · 컨트롤러 (여러 번 불러도 됨).</summary>
        public static void PrepareAssets()
        {
            foreach (var name in States) SetLoop(AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AniFolder}/{name}.anim"));
            foreach (var name in Workers) EnsureAvatar($"{ModelFolder}/{name}.fbx");
            EnsureAvatar($"{ModelFolder}/{Player}.fbx");
            Controller();
        }

        /// <summary>모델 인스턴스: fbx + 재질(psd) + Animator(공용 컨트롤러, 루트 모션 끔). parent 아래 원점.</summary>
        public static GameObject Model(string name, Transform parent)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{name}.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var material = Material(name, r.name);
                var shared = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = false; // 화면 밖이면 뼈 · 경계 계산 안 함 (알바 100명, 2026-11-26). 가장자리에서 팔이 잘려 보이면 메시 경계를 키운다
            }

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = Controller();
            animator.applyRootMotion = false;
            return go;
        }

        static void ApplyPlayer()
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[CharacterArt] 씬에 Player가 없습니다.");
                return;
            }

            foreach (var temp in new[] { "Body (임시)", "Facing (임시)" })
            {
                var t = player.transform.Find(temp);
                if (t != null && t.TryGetComponent<Renderer>(out var r)) r.enabled = false; // 오브젝트는 남긴다 (참조 대비)
            }

            foreach (var name in new[] { "player", Player }) // 옛 모델도 지운다
            {
                var old = player.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            Model(Player, player.transform);

            var anchor = player.transform.Find("StackAnchor");
            if (anchor != null) anchor.localPosition = new Vector3(0f, 0.55f, 0.45f); // 맨손 = 손 바로 위, 큰 머리를 피해 앞으로 (GDD 5.3)

            var animator = player.GetComponent<CharacterAnimator>();
            if (animator == null) animator = player.AddComponent<CharacterAnimator>();
            BuildKit.SetRef(animator, "stack", player.GetComponent<CarryStack>());

            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
        }

        static AnimatorController Controller()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var name in States) // 빠진 상태만 더한다 (애니가 늘어도 기존 컨트롤러 유지)
            {
                if (System.Array.Exists(machine.states, s => s.state.name == name)) continue;
                var state = machine.AddState(name);
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AniFolder}/{name}.anim");
            }
            if (machine.defaultState == null) machine.defaultState = machine.states[0].state; // idle
            return controller;
        }

        static void SetLoop(AnimationClip clip)
        {
            if (clip == null) return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime) return;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static void EnsureAvatar(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null || importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel) return;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 모델별 재질 `M_Char_이름` (URP Lit + `이름_P.psd`). `_P.psd`가 없는 모델은 부위별 텍스처를 메시 이름으로 고른다 → `M_Char_이름_부위`:
        /// uniformT = `_hair` · `_skin`(얼굴 · 팔) · `_cloth`(나머지), player_SBS = `_body`(top · bottom) · `_head`(나머지).
        /// </summary>
        static Material Material(string name, string meshName)
        {
            var texture = $"{ModelFolder}/{name}_P.psd";
            var suffix = "";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texture) == null)
            {
                var mesh = meshName.ToLowerInvariant();
                // shortcut: 모델별 부위 규칙을 코드에 둔다 — 모델이 더 늘면 표(에셋)로
                suffix = name.EndsWith("SBS")
                    ? (mesh.StartsWith("top") || mesh.StartsWith("bottom") ? "_body" : "_head")
                    : mesh.StartsWith("hair") ? "_hair" : mesh.StartsWith("face") || mesh.StartsWith("arm") ? "_skin" : "_cloth";
                texture = $"{ModelFolder}/{name}{suffix}.psd";
            }
            var path = $"{MaterialFolder}/M_Char_{name}{suffix}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
