using System.Collections.Generic;
using APC.Contracts;
using APC.Data;
using APC.Logistics;
using APC.Orders;
using APC.Site;
using APC.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static APC.EditorTools.BuildKit;

namespace APC.EditorTools
{
    /// <summary>
    /// SiteLayout(config)대로 부지를 씬에 굽는다: 시작 모듈은 프리팹 인스턴스로. 잠긴 칸은 비워 두고, 해금 · 울타리는 실행 중 SiteUnlocker가 한다.
    /// 해금은 실행 중 SiteGrid.Unlock이 모듈 프리팹을 붙인다. "Site" 오브젝트를 지우고 다시 만들므로 여러 번 실행해도 같다.
    /// 모듈 내용(존 · 시설)을 바꾸려면 씬이 아니라 모듈 프리팹(Assets/Prefabs/Modules)을 고친다.
    /// </summary>
    public static class SiteBuilder
    {
        [MenuItem("APC/Setup/Build Site")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildInternal();
        }

        /// <summary>대화상자 없이 실행 (MCP · 자동화용).</summary>
        public static void BuildInternal()
        {
            var database = Database;
            var layout = database.Site;
            var size = layout.CellSize;

            var shelfPrefab = StandardModules.ShelfPrefab();
            StandardModules.TruckPrefab();
            StandardModules.OutboundTruckPrefab();
            StandardModules.CashBundlePrefab();
            var fencePrefab = StandardModules.ConstructionFencePrefab();
            StandardModules.EnsureAll(overwrite: false);
            AssignDefaultModules(layout);

            DestroyIfExists(GameObject.Find("Site"));
            DestroyIfExists(GameObject.Find("Ground (임시)"));
            var site = new GameObject("Site").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(site, false);
            ground.transform.localPosition = new Vector3(2f * size, -0.05f, -1f * size);
            ground.transform.localScale = new Vector3(14f * size / 10f, 1f, 9f * size / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_Ground", new Color(0.36f, 0.42f, 0.36f));
            ground.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // 임시 바닥은 네비메시에서 뺀다

            // 네비메시: 열린 모듈 바닥(충돌체)에만 굽는다 (M5-5)
            var surface = site.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            site.gameObject.AddComponent<SiteNavMesh>();

            var services = new GameObject("Logistics").transform;
            services.SetParent(site, false);
            SetRef(services.gameObject.AddComponent<Warehouse>(), "shelfPrefab", shelfPrefab);
            services.gameObject.AddComponent<ContractService>();
            services.gameObject.AddComponent<OrderService>();
            services.gameObject.AddComponent<APC.Economy.PartService>(); // 파트 타이머 · 정산 (GDD 9.5)
            SetRef(services.gameObject.AddComponent<APC.Staff.StaffService>(), "workerPrefab", StandardModules.StaffWorkerPrefab()); // 알바 (M6)

            var grid = BakeGrid(site, layout);
            SetRef(grid.gameObject.AddComponent<SiteUnlocker>(), "fencePrefab", fencePrefab); // 레벨로 자동 해금 + 다음 구역 울타리
            BuildLevelHud();
            BuildCashHud();
            BuildLevelUpBanner();
            BuildOrderQueue();
            BuildGuideArrow();
            BuildContractPopup();
            BuildSettlementPopup();
            FixCameraButton();
            DisableHudForBootstrap();
            RemoveTestZones();
            SetupPlayer(layout.CellCenter(1, 1) + new Vector3(-1f, 0f, -1f)); // 7번 사무실

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SiteBuilder] 부지 생성 완료 — 사무실 계약 PC에서 회사 A를 계약해 보세요.");
        }

        /// <summary>config에 모듈 참조가 비어 있는 배치만 기본 모듈로 채운다 (디자이너가 바꾼 값은 유지).</summary>
        static void AssignDefaultModules(SiteLayout layout)
        {
            var so = new SerializedObject(layout);
            var list = so.FindProperty("modules");
            for (var i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var module = element.FindPropertyRelative("module");
                if (module.objectReferenceValue != null) continue;

                var id = element.FindPropertyRelative("id").stringValue;
                if (!StandardModules.DefaultAssignments.TryGetValue(id, out var assignment)) continue;
                module.objectReferenceValue = ModuleFactory.Load(assignment.asset);
                element.FindPropertyRelative("rotation").intValue = assignment.rotation;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }

        static SiteGrid BakeGrid(Transform site, SiteLayout layout)
        {
            var gridGo = new GameObject("SiteGrid");
            gridGo.transform.SetParent(site, false);
            var grid = gridGo.AddComponent<SiteGrid>();
            var entries = new List<SiteGrid.Entry>();

            foreach (var placement in layout.Modules)
            {
                var entry = new SiteGrid.Entry { id = placement.id };
                var moduleSize = placement.module != null ? placement.module.Size : Vector2Int.one;
                var center = layout.GetModuleCenter(placement, moduleSize);

                var startOpen = layout.IsStartOpen(placement); // 묶인 배치는 대표를 따른다
                if (startOpen && placement.module != null)
                {
                    var instance = (SiteModule)PrefabUtility.InstantiatePrefab(placement.module, gridGo.transform);
                    instance.transform.SetPositionAndRotation(center, SiteLayout.GetRotation(placement));
                    instance.name = $"Module_{placement.id}";
                    entry.instance = instance;
                }
                else if (startOpen)
                {
                    Debug.LogWarning($"[SiteBuilder] 시작 칸 '{placement.id}'에 모듈이 없습니다.");
                }
                // 잠긴 칸은 아무것도 두지 않는다 (보이지 않음 · 들어갈 수 없음). 해금하면 SiteGrid가 모듈을 붙인다.
                entries.Add(entry);
            }

            grid.SetEntries(entries);
            EditorUtility.SetDirty(grid);
            return grid;
        }

        /// <summary>
        /// HUD Canvas 아래 UI를 모두 꺼진 채로 저장하고, 실행하면 UiBootstrap이 차례대로 켠다
        /// (켜 둔 채 저장된 팝업 · 딤이 클릭을 가로채지 않게).
        /// </summary>
        static void DisableHudForBootstrap()
        {
            var canvas = GameObject.Find("HUD Canvas");
            if (canvas == null) return;
            if (canvas.GetComponent<APC.UI.UiBootstrap>() == null) canvas.AddComponent<APC.UI.UiBootstrap>();
            foreach (Transform child in canvas.transform) child.gameObject.SetActive(false);
        }

        /// <summary>M2 손맛 테스트 존 정리 (부지 밖이라 이제 들어갈 수 없다).</summary>
        static void RemoveTestZones()
        {
            foreach (var name in new[] { "Test_Source", "Test_SinkAll", "Test_SinkRed", "Test_GaugePad" })
                DestroyIfExists(GameObject.Find(name));
        }

        /// <summary>플레이어 위치 + 현금 흡수 대상 등록.</summary>
        static void SetupPlayer(Vector3 position)
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            player.transform.position = position;
            if (player.GetComponent<CashCollector>() == null) player.AddComponent<CashCollector>();
        }

        // ── HUD: 레벨 · 경험치 (좌상단) ──────────────────────────────────

        static void BuildLevelHud()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("LevelHud");
            if (old != null) DestroyIfExists(old.gameObject);

            var sprite = BuiltinSprite("UI/Skin/UISprite.psd");
            var panel = UIImage("LevelHud", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(330f, 84f),
                new Color(0f, 0f, 0f, 0.45f), sprite);
            panel.type = Image.Type.Sliced;
            var rect = panel.rectTransform;
            rect.pivot = new Vector2(0f, 1f);

            var level = UIText("Level", rect, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(110f, 84f), "Lv.1", 40f, TitleFont, Color.white);
            var barBack = UIImage("XpBack", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(222f, 8f), new Vector2(190f, 22f),
                new Color(1f, 1f, 1f, 0.2f), sprite);
            barBack.type = Image.Type.Sliced;
            var fill = UIImage("XpFill", barBack.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.82f, 0.3f), sprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            var xp = UIText("Xp", rect, new Vector2(0f, 0.5f), new Vector2(222f, -22f), new Vector2(190f, 26f), "0/30", 22f, BodyFont, new Color(1f, 1f, 1f, 0.85f));

            var view = panel.gameObject.AddComponent<LevelView>();
            SetRef(view, "levelLabel", level);
            SetRef(view, "xpFill", fill);
            SetRef(view, "xpLabel", xp);
            SetRef(view, "punchTarget", rect);
        }

        // ── 가이드 화살표 (들고 있는 화물의 목적지) ───────────────────────

        static void BuildGuideArrow()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("GuideArrow");
            if (old != null) DestroyIfExists(old.gameObject);

            var root = UIRect("GuideArrow", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var arrow = UIImage("Arrow", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 64f), new Color(1f, 0.8f, 0.15f), null);
            var outline = arrow.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.12f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            var guide = root.gameObject.AddComponent<GuideArrow>();
            SetRef(guide, "arrow", arrow.rectTransform);
            SetRef(guide, "image", arrow);
            var player = GameObject.Find("Player");
            if (player != null) SetRef(guide, "stack", player.GetComponent<CarryStack>());
        }

        // ── 레벨업 배너 (중앙) + 플레이어 위 파티클 ───────────────────────

        static void BuildLevelUpBanner()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("LevelUpBanner");
            if (old != null) DestroyIfExists(old.gameObject);

            var root = UIRect("LevelUpBanner", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var band = UIImage("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1000f, 190f),
                new Color(0.1f, 0.08f, 0.05f, 0.7f), BuiltinSprite("UI/Skin/UISprite.psd"));
            band.type = Image.Type.Sliced;
            var title = UIText("Title", band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(980f, 110f), "LEVEL UP!  Lv.2", 84f, TitleFont, new Color(1f, 0.85f, 0.3f));
            var note = UIText("Note", band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(960f, 50f), "해금 내용", 32f, BodyFont, Color.white);

            var banner = root.gameObject.AddComponent<LevelUpBanner>();
            SetRef(banner, "group", group);
            SetRef(banner, "panel", band.rectTransform);
            SetRef(banner, "titleLabel", title);
            SetRef(banner, "noteLabel", note);
            SetRef(banner, "burst", BuildConfetti());
        }

        /// <summary>플레이어 머리 위 축하 파티클 (색종이 조각이 위로 터졌다가 떨어짐).</summary>
        static ParticleSystem BuildConfetti()
        {
            var player = GameObject.Find("Player");
            if (player == null) return null;
            var old = player.transform.Find("LevelUpConfetti");
            if (old != null) DestroyIfExists(old.gameObject);

            var go = new GameObject("LevelUpConfetti");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var colors = new Gradient();
            colors.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                        new GradientColorKey(new Color(0.3f, 0.8f, 1f), 0.66f), new GradientColorKey(new Color(0.4f, 0.9f, 0.4f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMat("M_Confetti");
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        // ── HUD: 현금 (좌상단) ───────────────────────────────────────────

        static void BuildCashHud()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("CashHud");
            if (old != null) DestroyIfExists(old.gameObject);

            // 레벨 패널(330) 오른쪽, 한 패널에 위아래 두 줄 — GDD 12.1: 위 보유 재화(크게) / 아래 저금통 `+N 대기`(작게)
            var sprite = BuiltinSprite("UI/Skin/UISprite.psd");
            var panel = UIImage("CashHud", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(370f, -20f), new Vector2(300f, 112f),
                new Color(0f, 0f, 0f, 0.45f), sprite);
            panel.type = Image.Type.Sliced;
            var rect = panel.rectTransform;
            rect.pivot = new Vector2(0f, 1f);

            // 위 줄: 보유 재화
            UIImage("Icon", rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -38f), new Vector2(52f, 34f),
                new Color(0.35f, 0.75f, 0.4f), sprite).type = Image.Type.Sliced;
            var label = UIText("Amount", rect, new Vector2(0f, 1f), new Vector2(190f, -38f), new Vector2(200f, 64f), "0", 50f, TitleFont, Color.white);
            label.alignment = TMPro.TextAlignmentOptions.Left;

            // 아래 줄: 저금통 (분홍 동그라미 = 돼지 저금통 자리)
            UIImage("PiggyIcon", rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(46f, 26f), new Vector2(26f, 26f),
                new Color(1f, 0.6f, 0.7f), BuiltinSprite("UI/Skin/Knob.psd"));
            var piggy = UIText("Piggy", rect, new Vector2(0f, 0f), new Vector2(170f, 26f), new Vector2(200f, 36f), "+0 대기", 28f, BodyFont, new Color(1f, 0.82f, 0.86f));
            piggy.alignment = TMPro.TextAlignmentOptions.Left;

            var view = panel.gameObject.AddComponent<CashView>();
            SetRef(view, "label", label);
            SetRef(view, "punchTarget", rect);
            SetRef(view, "piggyLabel", piggy);

            BuildPartClock(canvas);
        }

        /// <summary>파트 시계 (레벨 패널 아래): `3파트 · 01:42`.</summary>
        static void BuildPartClock(Transform canvas)
        {
            var old = canvas.Find("PartClock");
            if (old != null) DestroyIfExists(old.gameObject);

            var panel = UIImage("PartClock", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -112f), new Vector2(330f, 50f),
                new Color(0f, 0f, 0f, 0.45f), BuiltinSprite("UI/Skin/UISprite.psd"));
            panel.type = Image.Type.Sliced;
            panel.rectTransform.pivot = new Vector2(0f, 1f);
            var label = UIText("Label", panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(310f, 50f), "1파트 · 01:30", 32f, TitleFont, Color.white);

            // 계약 전(파트 0)에는 스스로 숨는다. 구독은 컴포넌트 수명이라 숨어 있어도 다시 켜진다.
            var clock = panel.gameObject.AddComponent<PartClockView>();
            SetRef(clock, "root", panel.gameObject);
            SetRef(clock, "label", label);
        }

        // ── 정산 팝업 (파트 종료) ─────────────────────────────────────────

        static void BuildSettlementPopup()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("SettlementPopup");
            if (old != null) DestroyIfExists(old.gameObject);

            var dark = new Color(0.25f, 0.22f, 0.2f);
            var sprite = BuiltinSprite("UI/Skin/UISprite.psd");
            var root = UIRect("SettlementPopup", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var body = UIRect("Body", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            UIImage("Dim", body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f), null).raycastTarget = true;

            var panel = UIImage("Panel", body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 760f), new Color(0.97f, 0.96f, 0.93f), sprite);
            panel.type = Image.Type.Sliced;
            var p = panel.rectTransform;
            var title = UIText("Title", p, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(700f, 90f), "1파트 정산", 56f, TitleFont, dark);

            string[] names = { "출고 수입", "반품 재출고 수입", "할당량 보너스", "알바 시급" };
            var values = new TMPro.TMP_Text[5];
            for (var i = 0; i < 5; i++)
            {
                var y = -150f - i * 72f - (i == 4 ? 24f : 0f);
                var isTotal = i == 4;
                var name = UIText($"Row{i}Name", p, new Vector2(0f, 1f), new Vector2(230f, y), new Vector2(380f, 60f), isTotal ? "합계" : names[i], isTotal ? 40f : 34f, isTotal ? TitleFont : BodyFont, dark);
                name.alignment = TMPro.TextAlignmentOptions.Left;
                var value = UIText($"Row{i}Value", p, new Vector2(1f, 1f), new Vector2(-190f, y), new Vector2(300f, 60f), "0", isTotal ? 46f : 36f, TitleFont, isTotal ? new Color(0.2f, 0.55f, 0.3f) : dark);
                value.alignment = TMPro.TextAlignmentOptions.Right;
                values[i] = value;
            }
            UIImage("Line", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(620f, 4f), new Color(0.25f, 0.22f, 0.2f, 0.3f), null);
            // 미지급 · 퇴사 알바 명단 + 경고 (GDD 11.7)
            var staff = UIText("Staff", p, new Vector2(0.5f, 1f), new Vector2(0f, -545f), new Vector2(680f, 100f), "", 26f, BodyFont, new Color(0.8f, 0.2f, 0.15f));
            staff.enableAutoSizing = true;
            staff.fontSizeMin = 18f;
            staff.fontSizeMax = 26f;

            var buttonImage = UIImage("Confirm", p, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(300f, 96f), new Color(0.3f, 0.75f, 0.4f), sprite);
            buttonImage.type = Image.Type.Sliced;
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<Button>();
            UIText("Label", buttonImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 96f), "확인", 42f, TitleFont, Color.white);

            var popup = root.gameObject.AddComponent<SettlementPopup>();
            SetRef(popup, "body", body.gameObject);
            SetRef(popup, "group", group);
            SetRef(popup, "panel", p);
            SetRef(popup, "titleLabel", title);
            SetRef(popup, "outboundValue", values[0]);
            SetRef(popup, "returnValue", values[1]);
            SetRef(popup, "bonusValue", values[2]);
            SetRef(popup, "wageValue", values[3]);
            SetRef(popup, "totalValue", values[4]);
            SetRef(popup, "staffLabel", staff);
            SetRef(popup, "confirmButton", button);
            root.SetAsLastSibling();
        }

        /// <summary>카메라 버튼 글자를 한글이 나오는 TMP로 바꾼다 (M1에서 만든 버튼은 기본 Text).</summary>
        static void FixCameraButton()
        {
            var button = Object.FindFirstObjectByType<CameraModeButton>(FindObjectsInactive.Include); // HUD는 꺼진 채 저장됨
            if (button == null) return;
            var labelTransform = button.transform.Find("Label");
            if (labelTransform == null && button.transform.childCount > 0) labelTransform = button.transform.GetChild(0);
            if (labelTransform == null) return;

            var legacy = labelTransform.GetComponent<Text>();
            if (legacy != null) Object.DestroyImmediate(legacy);
            var tmp = labelTransform.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp == null) tmp = labelTransform.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.font = TitleFont;
            tmp.fontSize = 36f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 20f;
            tmp.fontSizeMax = 40f;
            tmp.margin = new Vector4(10f, 0f, 10f, 0f);
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.2f, 0.2f, 0.2f);
            tmp.text = "카메라";
            tmp.raycastTarget = false;
            SetRef(button, "label", tmp);
            var so = new SerializedObject(button);
            so.FindProperty("followLabel").stringValue = "카메라";
            so.FindProperty("freeLabel").stringValue = "내 캐릭터로";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── HUD: 주문서 큐 (상단 오른쪽) ─────────────────────────────────

        static void BuildOrderQueue()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("OrderQueue");
            if (old != null) DestroyIfExists(old.gameObject);

            var dark = new Color(0.25f, 0.22f, 0.2f);
            var sprite = BuiltinSprite("UI/Skin/UISprite.psd");

            var root = UIRect("OrderQueue", canvas, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), Vector2.zero);
            root.pivot = new Vector2(1f, 1f);

            // 카드 템플릿 (오른쪽 위 기준)
            var cardImage = UIImage("CardTemplate", root, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(170f, 150f),
                new Color(0.97f, 0.96f, 0.93f), sprite);
            cardImage.type = Image.Type.Sliced;
            var card = cardImage.rectTransform;
            card.pivot = new Vector2(1f, 1f);
            var group = card.gameObject.AddComponent<CanvasGroup>();

            var number = UIText("Number", card, new Vector2(0f, 1f), new Vector2(42f, -24f), new Vector2(64f, 36f), "#1", 26f, TitleFont, dark);
            number.alignment = TMPro.TextAlignmentOptions.Left;

            var badge = UIImage("Badge", card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -24f), new Vector2(84f, 34f),
                new Color(0.55f, 0.55f, 0.58f), sprite);
            badge.type = Image.Type.Sliced;
            var badgeLabel = UIText("Label", badge.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 34f), "대기", 24f, TitleFont, Color.white);

            var lines = UIRect("Lines", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(150f, 100f));
            lines.pivot = new Vector2(0.5f, 1f);
            var layout = lines.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.padding = new RectOffset(16, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var lineIcon = UIImage("LineTemplate", lines, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f), Color.white, sprite);
            lineIcon.type = Image.Type.Sliced;
            var count = UIText("Count", lineIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(52f, 0f), new Vector2(90f, 32f), "0/3", 28f, TitleFont, dark);
            count.alignment = TMPro.TextAlignmentOptions.Left;

            var cardView = card.gameObject.AddComponent<OrderCardView>();
            SetRef(cardView, "group", group);
            SetRef(cardView, "numberLabel", number);
            SetRef(cardView, "badge", badge);
            SetRef(cardView, "badgeLabel", badgeLabel);
            SetRef(cardView, "lineRoot", lines);
            SetRef(cardView, "lineTemplate", lineIcon);

            var overflow = UIText("Overflow", root, new Vector2(1f, 1f), Vector2.zero, new Vector2(80f, 56f), "+1", 44f, TitleFont, dark);
            overflow.rectTransform.pivot = new Vector2(1f, 1f);

            var view = root.gameObject.AddComponent<OrderQueueView>();
            SetRef(view, "container", root);
            SetRef(view, "cardTemplate", cardView);
            SetRef(view, "overflowLabel", overflow);
        }

        // ── 계약 팝업 UI ────────────────────────────────────────────────

        static void BuildContractPopup()
        {
            var canvas = GameObject.Find("HUD Canvas").transform;
            var old = canvas.Find("ContractPopup");
            if (old != null) DestroyIfExists(old.gameObject);

            var root = UIRect("ContractPopup", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var body = UIRect("Body", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            var dim = UIImage("Dim", body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f), null);
            dim.raycastTarget = true;

            // 계약 PC 팝업: 계약 | 고용 탭 (GDD 11.1)
            var panel = UIImage("Panel", body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 900f),
                new Color(0.97f, 0.96f, 0.93f), BuiltinSprite("UI/Skin/UISprite.psd"));
            panel.type = Image.Type.Sliced;
            var panelRect = panel.rectTransform;
            var contractTab = TabButton("ContractTab", panelRect, new Vector2(150f, -60f), "계약");
            var hireTab = TabButton("HireTab", panelRect, new Vector2(390f, -60f), "고용");
            var rosterTab = TabButton("RosterTab", panelRect, new Vector2(630f, -60f), "배치");

            var close = UIImage("Close", panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(90f, 90f),
                new Color(0.85f, 0.4f, 0.35f), BuiltinSprite("UI/Skin/UISprite.psd"));
            close.type = Image.Type.Sliced;
            close.raycastTarget = true; // 버튼은 클릭을 받아야 한다
            var closeButton = close.gameObject.AddComponent<Button>();
            UIText("X", close.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f), "X", 48f, TitleFont, Color.white);

            var contractPage = UIRect("ContractPage", panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cards = UIRect("Cards", contractPage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(960f, 440f));
            var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cardImage = UIImage("CardTemplate", cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f),
                Color.white, BuiltinSprite("UI/Skin/UISprite.psd"));
            cardImage.type = Image.Type.Sliced;
            var card = cardImage.rectTransform;
            var nameLabel = UIText("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(380f, 70f), "회사", 46f, TitleFont, new Color(0.25f, 0.22f, 0.2f));
            var iconRow = UIRect("Icons", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(380f, 70f));
            var iconLayout = iconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            iconLayout.spacing = 14f;
            iconLayout.childAlignment = TextAnchor.MiddleCenter;
            iconLayout.childControlWidth = false;
            iconLayout.childControlHeight = false;
            iconLayout.childForceExpandWidth = false;
            iconLayout.childForceExpandHeight = false;
            var icon = UIImage("IconTemplate", iconRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f),
                Color.white, BuiltinSprite("UI/Skin/UISprite.psd"));
            var infoLabel = UIText("Info", card, new Vector2(0.5f, 1f), new Vector2(0f, -255f), new Vector2(380f, 100f), "입고 트럭 : 8\n단가 : 10", 34f, BodyFont, new Color(0.35f, 0.33f, 0.3f));
            var blockLabel = UIText("Block", card, new Vector2(0.5f, 1f), new Vector2(0f, -335f), new Vector2(380f, 50f), "Lv.7 해금", 34f, BodyFont, new Color(0.75f, 0.25f, 0.2f));
            cardImage.raycastTarget = true; // 도장을 놓을 곳 (계약은 버튼 대신 도장, GDD 6.3)

            var contractCard = card.gameObject.AddComponent<ContractCard>();
            SetRef(contractCard, "nameLabel", nameLabel);
            SetRef(contractCard, "infoLabel", infoLabel);
            SetRef(contractCard, "blockLabel", blockLabel);
            SetRef(contractCard, "iconRow", iconRow);
            SetRef(contractCard, "iconTemplate", icon);
            StampOverlay(contractCard, card, "[ 계약 ]", new Vector2(0f, 35f));

            var popup = root.gameObject.AddComponent<ContractPopup>();
            SetRef(popup, "body", body.gameObject);
            SetRef(popup, "group", group);
            SetRef(popup, "panel", panelRect);
            SetRef(popup, "closeButton", closeButton);
            SetRef(popup, "cardContainer", cards);
            SetRef(popup, "cardTemplate", contractCard);
            SetRef(popup, "contractTab", contractTab);
            SetRef(popup, "hireTab", hireTab);
            SetRef(popup, "contractPage", contractPage.gameObject);
            SetRef(popup, "hirePage", BuildHirePage(panelRect).gameObject);
            SetRef(popup, "rosterTab", rosterTab);
            SetRef(popup, "rosterPage", BuildRosterPage(panelRect).gameObject);
            SetRef(popup, "stampArea", BuildStamp(panelRect)); // 계약 · 고용 탭이 같이 쓰는 도장 (카드보다 위에 그려지게 마지막에)
            root.SetAsLastSibling();
        }

        /// <summary>도장 카드(StampCard) 공통: 회색 필터 + 붉은 도장 자국(25° 기울임) + 사유 글자. 모두 꺼진 채로 만든다.</summary>
        static void StampOverlay(StampCard target, RectTransform card, string markText, Vector2 reasonPosition)
        {
            var filter = UIImage("StampFilter", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.35f, 0.35f, 0.35f, 0.65f), BuiltinSprite("UI/Skin/UISprite.psd"));
            filter.type = Image.Type.Sliced;
            filter.gameObject.SetActive(false);
            var mark = UIText("StampMark", card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 160f), markText, 96f, TitleFont, new Color(0.85f, 0.15f, 0.15f));
            mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            mark.gameObject.SetActive(false);
            var reason = UIText("Reason", card, new Vector2(0.5f, 0f), reasonPosition, new Vector2(360f, 44f), "", 30f, TitleFont, new Color(0.85f, 0.15f, 0.15f));
            reason.gameObject.SetActive(false);
            SetRef(target, "stampFilter", filter);
            SetRef(target, "stampMark", mark.rectTransform);
            SetRef(target, "reasonLabel", reason);
        }

        /// <summary>
        /// 계약 PC 팝업 오른쪽 아래 도장: 평소 앞모습(Art/Temp/Stamp_Front), 들면 윗모습(Stamp_Top).
        /// 끌어서 계약 · 후보 카드에 놓으면 찍는다.
        /// </summary>
        static GameObject BuildStamp(RectTransform panelRect)
        {
            var front = LoadSprite("Assets/Art/Temp/Stamp_Front.png");
            var top = LoadSprite("Assets/Art/Temp/Stamp_Top.png");
            var area = UIRect("StampArea", panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); // 배치 탭에서는 통째로 숨김
            var image = UIImage("Stamp", area, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 160f), new Vector2(230f, 230f), Color.white, front);
            image.preserveAspect = true;
            image.raycastTarget = true;
            image.gameObject.AddComponent<CanvasGroup>();
            UIText("Hint", area, new Vector2(1f, 0f), new Vector2(-150f, 30f), new Vector2(320f, 44f), "끌어서 카드에 찍기", 28f, BodyFont, new Color(0.35f, 0.33f, 0.3f));
            var stamp = image.gameObject.AddComponent<Stamp>();
            SetRef(stamp, "image", image);
            SetRef(stamp, "frontSprite", front);
            SetRef(stamp, "topSprite", top);
            return area.gameObject;
        }

        /// <summary>
        /// 배치 탭 (GDD 11.3): 위 왼쪽 알바 수, 하차 · 분류 · 피킹 3열(제목에 인원 수) + 오른쪽 해고 칸.
        /// 알바 카드를 끌어 다른 열에 놓으면 역할 변경, 해고 칸에 놓으면 확인 창.
        /// </summary>
        static RosterPanel BuildRosterPage(RectTransform panelRect)
        {
            var dark = new Color(0.25f, 0.22f, 0.2f);
            var soft = new Color(0.35f, 0.33f, 0.3f);
            var red = new Color(0.8f, 0.25f, 0.2f);
            var sprite = BuiltinSprite("UI/Skin/UISprite.psd");
            var rules = Database.Staff;
            var center = new Vector2(0.5f, 0.5f);

            var page = UIRect("RosterPage", panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var header = UIText("Header", page, new Vector2(0f, 1f), new Vector2(380f, -150f), new Vector2(700f, 60f), "알바 0/1", 36f, BodyFont, dark);
            header.alignment = TMPro.TextAlignmentOptions.Left;

            var roles = new[] { StaffRole.Unload, StaffRole.Sort, StaffRole.Pick };
            var names = new[] { "하차", "분류", "피킹" };
            var columns = new List<Object>();
            for (var i = 0; i < roles.Length; i++)
            {
                var bg = UIImage($"Column_{roles[i]}", page, center, center, new Vector2(-560f + 380f * i, -60f), new Vector2(360f, 640f), new Color(0.9f, 0.88f, 0.85f), sprite);
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = true; // 카드를 놓을 곳
                var title = UIText("Title", bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(340f, 60f), $"{names[i]} (0)", 38f, TitleFont, dark);
                UIImage("Bar", bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(320f, 8f), rules.GetRoleColor(roles[i]), sprite);
                var content = UIRect("Content", bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -365f), new Vector2(340f, 540f));
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                columns.Add(Column(bg.gameObject, false, roles[i], title, names[i], content));
            }

            var fireImage = UIImage("Column_Fire", page, center, center, new Vector2(590f, -60f), new Vector2(300f, 640f), new Color(0.95f, 0.86f, 0.83f), sprite);
            fireImage.type = Image.Type.Sliced;
            fireImage.raycastTarget = true;
            UIText("Label", fireImage.rectTransform, center, Vector2.zero, new Vector2(280f, 200f), "해고\n<size=60%>여기에 놓기</size>", 52f, TitleFont, red);
            Column(fireImage.gameObject, true, StaffRole.Unload, null, "해고", null);

            // 끄는 카드를 올려 둘 층 (레이아웃 밖) + 카드 템플릿
            var dragLayer = UIRect("DragLayer", page, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cardImage = UIImage("CardTemplate", dragLayer, center, center, Vector2.zero, new Vector2(340f, 100f), Color.white, sprite);
            cardImage.type = Image.Type.Sliced;
            cardImage.raycastTarget = true; // 끌 수 있게
            cardImage.gameObject.AddComponent<CanvasGroup>();
            var bar = UIImage("Color", cardImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(16f, 76f), Color.white, sprite);
            var nameLabel = UIText("Name", cardImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(20f, -30f), new Vector2(280f, 44f), "알바 1", 34f, TitleFont, dark);
            nameLabel.alignment = TMPro.TextAlignmentOptions.Left;
            var statsLabel = UIText("Stats", cardImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(20f, -72f), new Vector2(280f, 36f), "이동 1 · 적재 1 · 시급 8", 26f, BodyFont, soft);
            statsLabel.alignment = TMPro.TextAlignmentOptions.Left;
            var card = cardImage.gameObject.AddComponent<RosterCard>();
            SetRef(card, "colorBar", bar);
            SetRef(card, "nameLabel", nameLabel);
            SetRef(card, "statsLabel", statsLabel);

            // 해고 확인 창 (맨 위, 꺼진 채)
            var confirm = UIImage("Confirm", page, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.45f), null);
            confirm.raycastTarget = true;
            var box = UIImage("Box", confirm.rectTransform, center, center, Vector2.zero, new Vector2(700f, 340f), Color.white, sprite);
            box.type = Image.Type.Sliced;
            var confirmLabel = UIText("Label", box.rectTransform, center, new Vector2(0f, 60f), new Vector2(640f, 150f), "알바 1을(를) 해고할까요?", 38f, TitleFont, dark);
            var yes = DialogButton("Fire", box.rectTransform, new Vector2(-150f, -95f), "해고", red);
            var no = DialogButton("Cancel", box.rectTransform, new Vector2(150f, -95f), "취소", new Color(0.6f, 0.58f, 0.55f));
            confirm.gameObject.SetActive(false);

            var panel = page.gameObject.AddComponent<RosterPanel>();
            SetList(panel, "columns", columns);
            SetRef(panel, "cardTemplate", card);
            SetRef(panel, "dragLayer", dragLayer);
            SetRef(panel, "headerLabel", header);
            SetRef(panel, "confirm", confirm.gameObject);
            SetRef(panel, "confirmLabel", confirmLabel);
            SetRef(panel, "confirmButton", yes);
            SetRef(panel, "cancelButton", no);

            page.gameObject.SetActive(false);
            return panel;
        }

        static RosterColumn Column(GameObject go, bool fire, StaffRole role, TMPro.TMP_Text title, string displayName, RectTransform content)
        {
            var column = go.AddComponent<RosterColumn>();
            var so = new SerializedObject(column);
            so.FindProperty("fire").boolValue = fire;
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("content").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
            return column;
        }

        static Button DialogButton(string name, RectTransform parent, Vector2 position, string text, Color color)
        {
            var image = UIImage(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(240f, 80f), color, BuiltinSprite("UI/Skin/UISprite.psd"));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            UIText("Label", image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 80f), text, 38f, TitleFont, Color.white);
            return button;
        }

        /// <summary>스프라이트 모드가 Single이든 Multiple이든 그 텍스처의 첫 스프라이트.</summary>
        static Sprite LoadSprite(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite) return sprite;
            }
            Debug.LogWarning($"[SiteBuilder] 스프라이트가 없습니다: {path}");
            return null;
        }

        static Button TabButton(string name, RectTransform parent, Vector2 position, string text)
        {
            var image = UIImage(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(220f, 84f),
                new Color(0.75f, 0.72f, 0.68f), BuiltinSprite("UI/Skin/UISprite.psd"));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            UIText("Label", image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 84f), text, 42f, TitleFont, Color.white);
            return button;
        }

        /// <summary>
        /// 고용 탭 (GDD 11.1): 위 왼쪽 알바 수 · 보유 재화, 위 오른쪽 다음 교체 시간 + [새로고침], 후보 카드 3장,
        /// 도장은 팝업에 하나 (BuildStamp, 계약 탭과 같이 씀).
        /// </summary>
        static HirePanel BuildHirePage(RectTransform panelRect)
        {
            var dark = new Color(0.25f, 0.22f, 0.2f);
            var soft = new Color(0.35f, 0.33f, 0.3f);
            var sprite = BuiltinSprite("UI/Skin/UISprite.psd");

            var page = UIRect("HirePage", panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var header = UIText("Header", page, new Vector2(0f, 1f), new Vector2(380f, -150f), new Vector2(700f, 60f), "알바 0/1", 36f, BodyFont, dark);
            header.alignment = TMPro.TextAlignmentOptions.Left;
            var timer = UIText("Timer", page, new Vector2(1f, 1f), new Vector2(-290f, -60f), new Vector2(320f, 60f), "다음 교체 00:00", 34f, BodyFont, dark);
            var refreshImage = UIImage("Refresh", page, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-290f, -140f), new Vector2(260f, 74f),
                new Color(0.35f, 0.6f, 0.85f), sprite);
            refreshImage.type = Image.Type.Sliced;
            refreshImage.raycastTarget = true;
            var refresh = refreshImage.gameObject.AddComponent<Button>();
            UIText("Label", refreshImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 74f), "새로고침", 36f, TitleFont, Color.white);

            var cardRow = UIRect("Candidates", page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -80f), new Vector2(1240f, 520f));
            var layout = cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cards = new List<Object>();
            for (var i = 0; i < 3; i++)
            {
                var cardImage = UIImage($"Candidate{i + 1}", cardRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 500f), Color.white, sprite);
                cardImage.type = Image.Type.Sliced;
                cardImage.raycastTarget = true; // 도장을 놓을 곳
                var card = cardImage.rectTransform;
                var title = UIText("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(340f, 64f), $"후보 {i + 1}", 44f, TitleFont, dark);
                var speed = UIText("Speed", card, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(340f, 56f), "이동 속도", 34f, BodyFont, soft);
                var carry = UIText("Carry", card, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(340f, 56f), "적재량", 34f, BodyFont, soft);
                var cost = UIText("Cost", card, new Vector2(0.5f, 1f), new Vector2(0f, -305f), new Vector2(340f, 64f), "고용금", 42f, TitleFont, dark);
                var wage = UIText("Wage", card, new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(340f, 56f), "시급", 32f, BodyFont, soft);
                var hireCard = card.gameObject.AddComponent<HireCard>();
                SetRef(hireCard, "titleLabel", title);
                SetRef(hireCard, "speedLabel", speed);
                SetRef(hireCard, "carryLabel", carry);
                SetRef(hireCard, "costLabel", cost);
                SetRef(hireCard, "wageLabel", wage);
                StampOverlay(hireCard, card, "[ 고용 ]", new Vector2(0f, 55f));
                cards.Add(hireCard);
            }

            var hire = page.gameObject.AddComponent<HirePanel>();
            SetList(hire, "cards", cards);
            SetRef(hire, "headerLabel", header);
            SetRef(hire, "timerLabel", timer);
            SetRef(hire, "refreshButton", refresh);

            page.gameObject.SetActive(false);
            return hire;
        }
    }
}
