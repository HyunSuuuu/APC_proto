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

            var grid = BakeGrid(site, layout);
            SetRef(grid.gameObject.AddComponent<SiteUnlocker>(), "fencePrefab", fencePrefab); // 레벨로 자동 해금 + 다음 구역 울타리
            BuildLevelHud();
            BuildCashHud();
            BuildLevelUpBanner();
            BuildOrderQueue();
            BuildGuideArrow();
            BuildContractPopup();
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

            // 레벨 패널(330) 오른쪽 — GDD 12.1 [Lv.3 ■■■□ 42/80] 💰 1,250
            var panel = UIImage("CashHud", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(370f, -20f), new Vector2(300f, 84f),
                new Color(0f, 0f, 0f, 0.45f), BuiltinSprite("UI/Skin/UISprite.psd"));
            panel.type = Image.Type.Sliced;
            var rect = panel.rectTransform;
            rect.pivot = new Vector2(0f, 1f);

            UIImage("Icon", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(52f, 34f),
                new Color(0.35f, 0.75f, 0.4f), BuiltinSprite("UI/Skin/UISprite.psd")).type = Image.Type.Sliced;
            var label = UIText("Amount", rect, new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(200f, 84f), "0", 52f, TitleFont, Color.white);
            label.alignment = TMPro.TextAlignmentOptions.Left;

            var view = panel.gameObject.AddComponent<CashView>();
            SetRef(view, "label", label);
            SetRef(view, "punchTarget", rect);
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

            var panel = UIImage("Panel", body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 640f),
                new Color(0.97f, 0.96f, 0.93f), BuiltinSprite("UI/Skin/UISprite.psd"));
            panel.type = Image.Type.Sliced;
            var panelRect = panel.rectTransform;
            UIText("Title", panelRect, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 90f), "계약", 60f, TitleFont, new Color(0.25f, 0.22f, 0.2f));

            var close = UIImage("Close", panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(90f, 90f),
                new Color(0.85f, 0.4f, 0.35f), BuiltinSprite("UI/Skin/UISprite.psd"));
            close.type = Image.Type.Sliced;
            close.raycastTarget = true; // 버튼은 클릭을 받아야 한다
            var closeButton = close.gameObject.AddComponent<Button>();
            UIText("X", close.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f), "X", 48f, TitleFont, Color.white);

            var cards = UIRect("Cards", panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(960f, 440f));
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
            var infoLabel = UIText("Info", card, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(380f, 60f), "입고 트럭 8개", 34f, BodyFont, new Color(0.35f, 0.33f, 0.3f));
            var buttonImage = UIImage("Button", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(300f, 96f),
                new Color(0.3f, 0.75f, 0.4f), BuiltinSprite("UI/Skin/UISprite.psd"));
            buttonImage.type = Image.Type.Sliced;
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<Button>();
            var buttonLabel = UIText("Label", buttonImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 96f), "계약", 40f, TitleFont, Color.white);

            var contractCard = card.gameObject.AddComponent<ContractCard>();
            SetRef(contractCard, "nameLabel", nameLabel);
            SetRef(contractCard, "infoLabel", infoLabel);
            SetRef(contractCard, "iconRow", iconRow);
            SetRef(contractCard, "iconTemplate", icon);
            SetRef(contractCard, "button", button);
            SetRef(contractCard, "buttonImage", buttonImage);
            SetRef(contractCard, "buttonLabel", buttonLabel);

            var popup = root.gameObject.AddComponent<ContractPopup>();
            SetRef(popup, "body", body.gameObject);
            SetRef(popup, "group", group);
            SetRef(popup, "panel", panelRect);
            SetRef(popup, "closeButton", closeButton);
            SetRef(popup, "cardContainer", cards);
            SetRef(popup, "cardTemplate", contractCard);
            root.SetAsLastSibling();
        }
    }
}
