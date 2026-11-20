using System;
using System.Collections.Generic;
using APC.Data;
using APC.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace APC.BrokeCash
{
    /// <summary>
    /// 정산 저금통 깨기 연출 진행 (공용, R&amp;D). 몸체(PiggyBody — DOTween · 스켈레톤 중 하나)에 내려옴 → 쩌적 → 깨짐을 시키고,
    /// 깨지면 구멍에서 돈 조각을 DOJump로 흘려 바닥 더미에 쌓은 뒤 CashFlyEffect로 좌측 하단 "정산할 돈"에 날려 숫자를 올린다.
    /// 0원이면 먼지 · 나방 · 단추 개그. 모든 트윈은 SetUpdate(true)(게임이 멈춘 정산 중에도 돌도록) · SetId(this)(건너뛰기 때 한 번에 정리).
    /// </summary>
    public sealed class BrokeCashDirector : MonoBehaviour
    {
        [SerializeField] PiggyBody body;
        [SerializeField] BrokeCashHud hud;
        [SerializeField, Tooltip("HUD의 CashFlyEffect (상차 현금 줍기와 같은 비행)")] CashFlyEffect cashFly;
        [SerializeField] Camera stageCamera;
        [SerializeField, Tooltip("연출 값 (Assets/Data/BrokeCashConfig)")] BrokeCashConfig config;
        [SerializeField, Tooltip("바닥 높이 (월드 y)")] float groundY = -2.7f;
        [SerializeField, Tooltip("구멍 → 더미 시작까지 가로 간격")] float pileGap = 0.52f;
        [SerializeField, Tooltip("더미 앞뒤 깊이를 화면 아래로 얼마나 내릴지")] float depthToScreen = 0.45f;
        [SerializeField, Tooltip("\"쩌적!\" · \"와장창!\" · 개그 글자 (월드 TextMeshPro)")] TMP_Text crackText;
        [SerializeField] TMP_Text breakText;
        [SerializeField] TMP_Text gagText;
        [SerializeField] Color floorColor = new(0.89f, 0.9f, 0.92f);
        [SerializeField] Color wallColor = new(0.97f, 0.97f, 0.98f);

        readonly List<SpriteRenderer> piecePool = new();
        readonly List<SpriteRenderer> shardPool = new();
        readonly List<SpriteRenderer> dustPool = new();
        Transform moth;
        SpriteRenderer button;
        BrokeCashPlan plan;
        Vector3 cameraHome;
        int playId;
        int arrived;
        bool done = true;

        public BrokeCashPlan Plan => plan;
        public bool IsPlaying => !done;
        public event Action Finished;

        BrokeCashConfig Config => BrokeCashConfig.OrDefault(config);

        void Awake()
        {
            DOTween.SetTweensCapacity(1250, 312); // 조각 120개 × (점프 · 회전 · 착지 · 대기) + CashFlyEffect
            cameraHome = stageCamera.transform.position;
            BuildBackdrop();
            foreach (var t in new[] { crackText, breakText, gagText }) t.gameObject.SetActive(false);
        }

        void BuildBackdrop()
        {
            var size = stageCamera.orthographicSize;
            var width = size * 2f * 3f;
            Rect("Wall", new Vector3(cameraHome.x, cameraHome.y, 5f), new Vector2(width, size * 2f + 2f), wallColor, -100);
            var floorTop = groundY + 0.9f;
            var floorBottom = cameraHome.y - size - 1f;
            Rect("Floor", new Vector3(cameraHome.x, (floorTop + floorBottom) * 0.5f, 4f), new Vector2(width, floorTop - floorBottom), floorColor, -90);
        }

        void Rect(string name, Vector3 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<SpriteRenderer>();
            r.sprite = BrokeCashArt.Square;
            r.color = color;
            r.sortingOrder = order;
        }

        // ── 재생 ────────────────────────────────────────────────────────

        public void Play(int income, int reference, BrokeCashFlyMode flyMode, int seed)
        {
            playId++;
            var id = playId;
            DOTween.Kill(this);
            HideAll();
            stageCamera.transform.position = cameraHome;
            done = false;
            arrived = 0;

            plan = BrokeCashPlan.Create(income, reference, config, flyMode, seed);
            body.Prepare(plan, config);
            hud.ResetHud();

            var c = Config;
            var tCrack = c.LowerSeconds + c.CrackDelay;
            var tBreak = tCrack + c.BreakDelay;
            body.Lower(plan);
            DOTween.Sequence().SetUpdate(true).SetId(this).SetLink(gameObject)
                .InsertCallback(tCrack, () =>
                {
                    body.Crack(plan);
                    Pop(crackText, "쩌적!", body.HolePosition + new Vector3(0.9f, 1.3f), 0.55f, -7f);
                    Shake(0.08f, 0.2f);
                })
                .InsertCallback(tBreak, () => Break(id));
        }

        void Break(int id)
        {
            body.Break(plan);
            var hole = body.HolePosition;
            Pop(breakText, plan.Empty ? "퐁…" : "와장창!", hole + new Vector3(1.6f, 0.8f), 0.8f, 6f);
            Shake(plan.CameraShake, 0.45f);
            SpawnShards(hole);
            if (plan.Empty)
            {
                PlayGag(id, hole);
                return;
            }

            var holeFraction = body.HoleFraction;
            DOVirtual.Float(0f, plan.Pour + 0.1f, plan.Pour + 0.1f, t => body.SetFill(plan.FillAt(t, holeFraction)))
                .SetEase(Ease.Linear).SetUpdate(true).SetId(this).SetLink(gameObject);

            var pileCenter = new Vector3(hole.x + pileGap + plan.PileRadius * 0.55f, groundY - depthToScreen * pileGap, 0f);
            var seq = DOTween.Sequence().SetUpdate(true).SetId(this).SetLink(gameObject);
            for (var i = 0; i < plan.Pieces.Length; i++)
            {
                var index = i;
                seq.InsertCallback(plan.Pieces[i].Spawn, () => Launch(id, index, pileCenter));
            }
        }

        void Launch(int id, int index, Vector3 pileCenter)
        {
            var p = plan.Pieces[index];
            var piece = Get(piecePool, "Cash");
            piece.sprite = BrokeCashArt.Piece(p.Kind);
            piece.sortingOrder = 400 + index;
            var t = piece.transform;
            t.position = body.HolePosition + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.1f);
            t.localScale = Vector3.one;
            t.localEulerAngles = Vector3.zero;
            var target = pileCenter + new Vector3(p.Offset.x, -depthToScreen * p.Offset.y + p.Height, 0f);

            t.DOJump(target, p.Power, 1, p.Duration).SetEase(Ease.Linear).SetUpdate(true).SetId(this).SetLink(piece.gameObject);
            t.DORotate(new Vector3(0f, 0f, p.Spin * p.Duration), p.Duration, RotateMode.FastBeyond360).SetUpdate(true).SetId(this).SetLink(piece.gameObject)
                .OnComplete(() =>
                {
                    // 바닥에 납작하게 + 착지 튐. 아래(앞)에 있을수록 위에 그림
                    t.localEulerAngles = new Vector3(0f, 0f, p.EndAngle);
                    var flat = new Vector3(1f, p.Kind == CashPieceKind.Coin ? 0.45f : 0.6f, 1f);
                    t.localScale = flat;
                    piece.sortingOrder = 200 + Mathf.RoundToInt(-target.y * 40f) + index % 7;
                    t.DOPunchScale(flat * 0.25f, 0.18f, 4, 0.5f).SetUpdate(true).SetId(this).SetLink(piece.gameObject);
                });
            DOVirtual.DelayedCall(p.Fly - p.Spawn, () => FlyToHud(id, index, piece), true).SetId(this).SetLink(piece.gameObject);
        }

        void FlyToHud(int id, int index, SpriteRenderer piece)
        {
            piece.gameObject.SetActive(false);
            var value = plan.Pieces[index].Value;
            void Arrive()
            {
                if (id != playId) return; // 건너뛰기 · 다시 재생 뒤에 늦게 도착한 것
                hud.Add(value);
                arrived++;
                if (arrived == plan.Pieces.Length) Finish();
            }
            if (cashFly != null) cashFly.Fly(piece.transform.position, Arrive);
            else Arrive();
        }

        void Finish()
        {
            done = true;
            hud.Finish();
            DOVirtual.DelayedCall(0.6f, () => { hud.ShowReceipt(); Finished?.Invoke(); }, true).SetId(this).SetLink(gameObject);
        }

        /// <summary>끝까지 건너뛰기: 깨진 채 멈추고 금액을 바로 채운다.</summary>
        public void Skip()
        {
            if (plan == null || done) return;
            playId++;
            DOTween.Kill(this);
            HideAll();
            stageCamera.transform.position = cameraHome;
            body.SnapBroken();
            body.SetFill(plan.FillAt(999f, body.HoleFraction));
            if (plan.Empty) hud.ShowEmpty();
            else hud.SetValue(plan.Income);
            done = true;
            hud.ShowReceipt();
            Finished?.Invoke();
        }

        // ── 효과 ────────────────────────────────────────────────────────

        void SpawnShards(Vector3 hole)
        {
            var rng = new System.Random(plan.Seed * 7 + 3);
            float R() => (float)rng.NextDouble();
            for (var i = 0; i < 12; i++)
            {
                var shard = Get(shardPool, "Shard");
                shard.sprite = BrokeCashArt.Shard;
                shard.sortingOrder = 450;
                shard.color = Color.white;
                var t = shard.transform;
                t.position = hole;
                t.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.2f, R());
                var a = Mathf.Lerp(-70f, 25f, R()) * Mathf.Deg2Rad;
                var d = Mathf.Lerp(0.8f, 2.4f, R());
                var target = new Vector3(hole.x + Mathf.Cos(a) * d + 0.35f, groundY - depthToScreen * Mathf.Lerp(-0.3f, 0.8f, R()), 0f);
                var dur = Mathf.Lerp(0.45f, 0.7f, R());
                t.DOJump(target, Mathf.Lerp(0.25f, 1f, R()), 1, dur).SetEase(Ease.Linear).SetUpdate(true).SetId(this).SetLink(shard.gameObject);
                t.DORotate(new Vector3(0f, 0f, Mathf.Lerp(-800f, 800f, R()) * dur), dur, RotateMode.FastBeyond360).SetUpdate(true).SetId(this).SetLink(shard.gameObject);
                shard.DOFade(0f, 0.4f).SetDelay(dur + 0.8f).SetUpdate(true).SetId(this).SetLink(shard.gameObject)
                    .OnComplete(() => shard.gameObject.SetActive(false));
            }
        }

        void PlayGag(int id, Vector3 hole)
        {
            // 먼지
            var rng = new System.Random(plan.Seed);
            float R() => (float)rng.NextDouble();
            for (var i = 0; i < 10; i++)
            {
                var dust = Get(dustPool, "Dust");
                dust.sprite = BrokeCashArt.SoftCircle;
                dust.sortingOrder = 460;
                dust.color = new Color(0.6f, 0.6f, 0.64f, 0.6f);
                var a = Mathf.Lerp(-80f, 40f, R()) * Mathf.Deg2Rad;
                var d = Mathf.Lerp(0.7f, 1.6f, R());
                dust.transform.position = hole;
                dust.transform.localScale = Vector3.one * 0.3f;
                dust.transform.DOMove(hole + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * d, 1f).SetEase(Ease.OutQuad).SetUpdate(true).SetId(this).SetLink(dust.gameObject);
                dust.transform.DOScale(Mathf.Lerp(0.8f, 1.1f, R()), 1f).SetEase(Ease.OutQuad).SetUpdate(true).SetId(this).SetLink(dust.gameObject);
                dust.DOFade(0f, 1f).SetUpdate(true).SetId(this).SetLink(dust.gameObject);
            }

            // 나방: 구멍에서 지그재그로 날아올라 사라짐
            if (moth == null) moth = BuildMoth();
            moth.position = hole;
            moth.gameObject.SetActive(true);
            var wings = moth.GetComponentsInChildren<SpriteRenderer>();
            foreach (var w in wings) w.color = new Color(w.color.r, w.color.g, w.color.b, 1f);
            DOVirtual.Float(0f, 1f, 2.4f, u =>
                {
                    moth.position = hole + new Vector3(0.5f * Mathf.Sin(u * Mathf.PI * 4f) + u * 1.4f, u * 5.2f, 0f);
                    var flap = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 32f));
                    var alpha = 1f - Mathf.Clamp01((u - 0.8f) / 0.2f);
                    foreach (var w in wings) w.color = new Color(w.color.r, w.color.g, w.color.b, alpha);
                    wings[0].transform.localScale = new Vector3(0.19f, 0.14f * flap + 0.02f, 1f);
                    wings[1].transform.localScale = new Vector3(0.19f, 0.14f * flap + 0.02f, 1f);
                })
                .SetDelay(0.15f).SetEase(Ease.Linear).SetUpdate(true).SetId(this).SetLink(moth.gameObject)
                .OnComplete(() => moth.gameObject.SetActive(false));

            // 단추 하나: 툭 → 또르르 → 비틀비틀 쓰러짐
            if (button == null) button = Get(new List<SpriteRenderer>(), "Button");
            button.gameObject.SetActive(true);
            button.sprite = BrokeCashArt.Button;
            button.sortingOrder = 470;
            var bt = button.transform;
            bt.position = hole;
            bt.localScale = Vector3.one;
            bt.localEulerAngles = Vector3.zero;
            var land = new Vector3(hole.x + 0.6f, groundY - 0.15f + 0.17f, 0f);
            var end = land + Vector3.right * 2.4f;
            DOTween.Sequence().SetUpdate(true).SetId(this).SetLink(button.gameObject)
                .AppendInterval(0.25f)
                .Append(bt.DOJump(land, 0.35f, 1, 0.42f).SetEase(Ease.Linear))
                .Join(bt.DOScaleX(0.3f, 0.42f))
                .AppendCallback(() => Pop(gagText, "또르르…", land + Vector3.up * 0.8f, 1.4f, 0f, follow: bt))
                .Append(bt.DOMoveX(end.x, 1.2f).SetEase(Ease.OutQuad))
                .Join(bt.DORotate(new Vector3(0f, 0f, -1440f), 1.2f, RotateMode.FastBeyond360).SetEase(Ease.OutQuad))
                .Append(bt.DOScale(new Vector3(1f, 0.45f, 1f), 0.8f).SetEase(Ease.OutBounce))
                .Join(bt.DOMoveY(end.y - 0.12f, 0.8f).SetEase(Ease.OutBounce))
                .AppendCallback(() => Pop(gagText, "딸깍", end + Vector3.up * 0.7f, 0.9f, 0f))
                .AppendCallback(() => { if (id == playId) hud.ShowEmpty(); })
                .AppendInterval(0.9f)
                .AppendCallback(() => { if (id != playId) return; done = true; hud.ShowReceipt(); Finished?.Invoke(); });
        }

        Transform BuildMoth()
        {
            var root = new GameObject("Moth").transform;
            root.SetParent(transform, false);
            foreach (var side in new[] { -1f, 1f })
            {
                var wing = new GameObject("Wing", typeof(SpriteRenderer));
                wing.transform.SetParent(root, false);
                wing.transform.localPosition = new Vector3(side * 0.17f, 0f, 0f);
                wing.transform.localEulerAngles = new Vector3(0f, 0f, side * 23f);
                wing.transform.localScale = new Vector3(0.19f, 0.14f, 1f);
                var r = wing.GetComponent<SpriteRenderer>();
                r.sprite = BrokeCashArt.Circle; r.color = new Color(0.6f, 0.55f, 0.45f); r.sortingOrder = 480;
            }
            var bodyGo = new GameObject("MothBody", typeof(SpriteRenderer));
            bodyGo.transform.SetParent(root, false);
            bodyGo.transform.localScale = new Vector3(0.05f, 0.15f, 1f);
            var br = bodyGo.GetComponent<SpriteRenderer>();
            br.sprite = BrokeCashArt.Circle; br.color = new Color(0.37f, 0.32f, 0.27f); br.sortingOrder = 481;
            return root;
        }

        /// <summary>효과음 글자: 0 → 1 OutBack 팝, 70% 지나면 사라짐.</summary>
        void Pop(TMP_Text text, string value, Vector3 position, float duration, float angle, Transform follow = null)
        {
            text.DOKill();
            text.gameObject.SetActive(true);
            text.text = value;
            text.alpha = 1f;
            var t = text.transform;
            t.position = position;
            t.localEulerAngles = new Vector3(0f, 0f, angle);
            t.localScale = Vector3.zero;
            t.DOScale(1f, 0.14f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true).SetId(this).SetLink(text.gameObject);
            if (follow != null)
            {
                var offset = position - follow.position;
                DOVirtual.Float(0f, 1f, duration, _ => t.position = new Vector3(follow.position.x, position.y, 0f) + new Vector3(offset.x, 0f, 0f))
                    .SetUpdate(true).SetId(this).SetLink(text.gameObject);
            }
            DOVirtual.Float(1f, 0f, duration * 0.3f, a => text.alpha = a).SetDelay(duration * 0.7f).SetUpdate(true).SetId(this).SetLink(text.gameObject)
                .OnComplete(() => text.gameObject.SetActive(false));
        }

        void Shake(float strength, float duration)
        {
            if (strength <= 0f) return;
            var cam = stageCamera.transform;
            cam.DOKill(true);
            cam.position = cameraHome;
            cam.DOShakePosition(duration, new Vector3(strength, strength, 0f), 25, 90f, false, true).SetUpdate(true).SetId(this).SetLink(gameObject)
                .OnKill(() => cam.position = cameraHome);
        }

        SpriteRenderer Get(List<SpriteRenderer> pool, string name)
        {
            foreach (var r in pool)
                if (!r.gameObject.activeSelf) { r.gameObject.SetActive(true); r.color = Color.white; return r; }
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            pool.Add(renderer);
            return renderer;
        }

        void HideAll()
        {
            foreach (var pool in new[] { piecePool, shardPool, dustPool })
                foreach (var r in pool) r.gameObject.SetActive(false);
            if (moth != null) moth.gameObject.SetActive(false);
            if (button != null) button.gameObject.SetActive(false);
            foreach (var t in new[] { crackText, breakText, gagText }) t.gameObject.SetActive(false);
        }
    }
}
