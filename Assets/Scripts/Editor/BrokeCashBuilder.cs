using APC.BrokeCash;
using APC.Data;
using APC.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace APC.EditorTools
{
    /// <summary>
    /// 저금통 깨기 R&amp;D 씬을 코드로 만든다. 메뉴 `APC/Broke Cash/Build DOTween Scene` → `Assets/Scenes/BrokeCashScene.unity`를 비우고 다시 채움.
    /// 저금통 이미지(`Art/Temp/BrokeCash/Piggy_Glass.png`, 1024 × 572 px, 100 px = 1 유닛)를 곱하기 재질로 올리고, 이미지 픽셀 좌표로 몸통 · 구멍 · 사슬 고정점을 잡는다.
    /// 설정 에셋 `Data/BrokeCashConfig.asset` · 재질 `Art/Materials/M_SpriteMultiply.mat`은 없을 때만 만든다.
    /// </summary>
    internal static class BrokeCashBuilder
    {
        const string ScenePath = "Assets/Scenes/BrokeCashScene.unity";
        const string PiggyPath = "Assets/Art/Temp/BrokeCash/Piggy_Glass.png";
        const string MaterialPath = "Assets/Art/Materials/M_SpriteMultiply.mat";
        const string ConfigPath = "Assets/Data/BrokeCashConfig.asset";

        // 이미지 픽셀 좌표 (왼쪽 위 0,0)
        static readonly Vector2 ImageSize = new(1024f, 572f);
        static readonly Vector2 BodyCenterPx = new(520f, 305f);
        static readonly Vector2 HolePx = new(727f, 430f);
        static readonly Vector2 PivotPx = new(545f, -300f);   // 사슬 고정점 (화면 위 밖)
        const float GroundPx = 640f;

        const float CameraSize = 3.575f;                       // 이미지 높이 = 화면의 80%
        static readonly Vector2 ImageCenter = new(-1.61f, 0.835f); // 이미지 위 끝이 화면 위보다 살짝 위

        static Vector3 Px(Vector2 px) => new((px.x - ImageSize.x * 0.5f) / 100f, (ImageSize.y * 0.5f - px.y) / 100f, 0f); // 이미지 중심 기준 로컬

        [MenuItem("APC/Broke Cash/Build DOTween Scene")]
        static void BuildTweenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var sprite = PreparePiggySprite();
            var material = EnsureMaterial();
            var config = EnsureConfig();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

            // 카메라
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.97f, 0.97f, 0.98f);

            // 저금통: 사슬 고정점(PiggyBodyTween) → 이미지(Body) → 안의 돈 · 금
            var imageCenterWorld = (Vector3)ImageCenter;
            var rig = new GameObject("Piggy (DOTween)");
            rig.transform.position = imageCenterWorld + Px(PivotPx);
            var bodyGo = new GameObject("Body", typeof(SpriteRenderer));
            bodyGo.transform.SetParent(rig.transform, false);
            bodyGo.transform.position = imageCenterWorld;
            var glass = bodyGo.GetComponent<SpriteRenderer>();
            glass.sprite = sprite;
            glass.sharedMaterial = material;
            glass.sortingOrder = 100;
            var inner = new GameObject("Inner").transform;
            inner.SetParent(bodyGo.transform, false);
            inner.localPosition = Px(BodyCenterPx);
            var cracksGo = new GameObject("Cracks", typeof(PiggyCracks));
            cracksGo.transform.SetParent(bodyGo.transform, false);
            cracksGo.transform.localPosition = Px(HolePx);
            var body = rig.AddComponent<PiggyBodyTween>();
            BuildKit.SetRef(body, "body", bodyGo.transform);
            BuildKit.SetRef(body, "innerRoot", inner);
            BuildKit.SetRef(body, "cracks", cracksGo.GetComponent<PiggyCracks>());

            // HUD
            var hud = BuildHud(config, out var cashFly);

            // 진행 · 시험 버튼
            var directorGo = new GameObject("BrokeCash Director");
            var director = directorGo.AddComponent<BrokeCashDirector>();
            BuildKit.SetRef(director, "body", body);
            BuildKit.SetRef(director, "hud", hud);
            BuildKit.SetRef(director, "cashFly", cashFly);
            BuildKit.SetRef(director, "stageCamera", cam);
            BuildKit.SetRef(director, "config", config);
            BuildKit.SetFloat(director, "groundY", imageCenterWorld.y + (ImageSize.y * 0.5f - GroundPx) / 100f);
            BuildKit.SetRef(director, "crackText", WorldText("CrackText", directorGo.transform, 7f));
            BuildKit.SetRef(director, "breakText", WorldText("BreakText", directorGo.transform, 9f));
            BuildKit.SetRef(director, "gagText", WorldText("GagText", directorGo.transform, 4.5f));
            var tester = directorGo.AddComponent<BrokeCashTester>();
            BuildKit.SetRef(tester, "director", director);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BrokeCashBuilder] {ScenePath} 생성 완료");
        }

        static Sprite PreparePiggySprite()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(PiggyPath);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 100f || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(PiggyPath);
        }

        static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("APC/SpriteMultiply"));
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static BrokeCashConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BrokeCashConfig>(ConfigPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<BrokeCashConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static TMP_Text WorldText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = BuildKit.TitleFont; // 밝은 배경이라 외곽선 재질 없이 진한 글자
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.16f, 0.18f, 0.22f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 2f);
            tmp.sortingOrder = 500;
            return tmp;
        }

        static BrokeCashHud BuildHud(BrokeCashConfig config, out CashFlyEffect cashFly)
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;
            var dollar = LoadFirstSprite("Assets/Art/Temp/Dollor.png");

            // 좌측 하단 "정산할 돈"
            var pill = Ui("SettleMoney", canvas, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(400f, 120f));
            var bg = pill.gameObject.AddComponent<Image>();
            bg.sprite = LoadFirstSprite("Assets/Art/Temp/ui_rounded_square.png");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.09f, 0.13f, 0.85f);
            var icon = Ui("Icon", pill, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(66f, 0f), new Vector2(84f, 84f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = dollar;
            iconImage.preserveAspect = true;
            var caption = Label("Caption", pill, "정산할 돈", 26f, BuildKit.BodyFont, new Color(0.86f, 0.89f, 0.92f));
            Place(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(124f, 26f), new Vector2(260f, 36f));
            var value = Label("Value", pill, "0", 52f, BuildKit.TitleFont, new Color(1f, 0.83f, 0.3f));
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(124f, -20f), new Vector2(260f, 60f));
            var empty = Label("Empty", canvas, "…텅", 40f, BuildKit.TitleFont, new Color(0.2f, 0.22f, 0.26f));
            Place(empty.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(448f, 70f), new Vector2(160f, 60f));

            // 아래 가운데 "영수증 출력"
            var receipt = Ui("Receipt", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(380f, 100f));
            var receiptBg = receipt.gameObject.AddComponent<Image>();
            receiptBg.sprite = bg.sprite;
            receiptBg.type = Image.Type.Sliced;
            receiptBg.color = new Color(1f, 0.99f, 0.96f);
            var receiptText = Label("Text", receipt, "영수증 출력 ▶\n<size=60%>다음: SettlementPopup 출력기</size>", 34f, BuildKit.TitleFont, new Color(0.17f, 0.15f, 0.13f));
            Place(receiptText.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);

            var hud = canvasGo.AddComponent<BrokeCashHud>();
            BuildKit.SetRef(hud, "valueLabel", value);
            BuildKit.SetRef(hud, "icon", icon);
            BuildKit.SetRef(hud, "emptyLabel", empty);
            BuildKit.SetRef(hud, "receipt", receipt);

            // 상차 현금 줍기와 같은 비행 (맨 위에 그림)
            var flyRoot = Ui("CashFly", canvas, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
            var template = Ui("Icon", flyRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            var templateImage = template.gameObject.AddComponent<Image>();
            templateImage.sprite = dollar;
            templateImage.preserveAspect = true;
            templateImage.raycastTarget = false;
            cashFly = flyRoot.gameObject.AddComponent<CashFlyEffect>();
            BuildKit.SetRef(cashFly, "iconTemplate", templateImage);
            BuildKit.SetRef(cashFly, "target", icon);
            BuildKit.SetRef(cashFly, "config", AssetDatabase.LoadAssetAtPath<CashFlyConfig>("Assets/Data/CashFlyConfig.asset"));
            return hud;
        }

        static RectTransform Ui(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, anchor, pivot, position, size, stretch);
            return rect;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, bool stretch = false)
        {
            if (stretch) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; return; }
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string text, float size, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (name == "Text") tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        static Sprite LoadFirstSprite(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
            return null;
        }
    }
}
