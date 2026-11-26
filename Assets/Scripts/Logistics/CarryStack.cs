using System;
using System.Collections.Generic;
using APC.Core;
using APC.Data;
using DG.Tweening;
using R3;
using UnityEngine;

namespace APC.Logistics
{
    /// <summary>
    /// 머리 위 운반 스택 (GDD 5.3, 체크리스트 M2-2). 플레이어와 알바가 같이 쓴다.
    /// 품목이 섞여도 되고, 중간 상자가 빠지면 위 상자들이 내려앉는다.
    /// </summary>
    public sealed class CarryStack : MonoBehaviour
    {
        [SerializeField, Tooltip("스택 바닥 위치 (머리 위). 출렁 연출로 크기가 바뀌므로 다른 것을 자식으로 두지 않는다")]
        Transform anchor;

        readonly List<Cargo> items = new();
        readonly ReactiveProperty<int> count = new(0);
        readonly ReactiveProperty<int> capacity = new(1);

        CarryConfig config;
        Vector3 lastAnchorPosition;
        Vector3 lean;
        Vector3 leanVelocity;
        Tween bounce;

        public Transform Anchor => anchor;

        /// <summary>0번 = 맨 아래. 읽기만 한다 (넣고 빼기는 TryAdd · TakeTopMatching) — 구체 타입은 존 판단마다 foreach 박싱을 없애려고 (알바 100명 GC, 2026-11-25).</summary>
        public List<Cargo> Items => items;

        public ReadOnlyReactiveProperty<int> Count => count;
        public ReadOnlyReactiveProperty<int> Capacity => capacity;
        public bool IsFull => items.Count >= capacity.Value;
        public float BoxHeight => config != null ? config.BoxHeight : 0.5f;

        void Awake() => config = GameManager.Instance != null ? GameManager.Instance.Database.Carry : null;

        void Start()
        {
            config ??= GameManager.Instance.Database.Carry;
            lastAnchorPosition = anchor.position;
        }

        void OnDestroy()
        {
            count.Dispose();
            capacity.Dispose();
        }

        /// <summary>스택 바닥을 바꾼다 (맨손 ↔ 구루마 · 롤테이너, GDD 5.3). 든 상자는 새 바닥으로 옮겨 다시 쌓는다.</summary>
        public void UseAnchor(Transform newAnchor)
        {
            if (newAnchor == null || newAnchor == anchor) return;
            bounce?.Kill(true);
            anchor.localScale = Vector3.one;
            anchor = newAnchor;
            foreach (var cargo in items) cargo.transform.SetParent(anchor, true);
            lastAnchorPosition = anchor.position;
            if (config != null) Reflow(0);
        }

        /// <summary>최대 적재 수 (플레이어 = PlayerConfig, 알바 = 힘 스탯).</summary>
        /// <summary>어느 스택이든 화물이 들어오거나 나가면 +1 (`CarriedIndex` 다시 세기, 2026-11-26).</summary>
        public static int Version { get; private set; }

        void OnEnable() => Version++;
        void OnDisable() => Version++;

        public void SetCapacity(int value) => capacity.Value = Mathf.Max(1, value);

        /// <summary>맨 위에 올린다. 화물은 지금 위치에서 포물선을 그리며 날아온다.</summary>
        public bool TryAdd(Cargo cargo, bool ignoreCapacity = false)
        {
            if (cargo == null || (IsFull && !ignoreCapacity)) return false; // 쓰레기는 적재 수와 상관없이 존이 상한(5)을 정한다

            items.Add(cargo);
            Version++;
            count.Value = items.Count;

            var t = cargo.transform;
            t.DOKill();
            t.SetParent(anchor, true);
            var visual = cargo.Visual;
            t.DOLocalJump(SlotPosition(items.Count - 1), config.JumpPower, 1, config.FlyDuration)
                .OnComplete(() => visual.DOPunchScale(Vector3.one * config.LandPunch, config.LandPunchDuration, 6, 0.5f));
            t.DOLocalRotateQuaternion(Quaternion.identity, config.FlyDuration);
            Bounce();
            return true;
        }

        /// <summary>조건에 맞는 화물을 위에서부터 찾아 빼낸다. 없으면 null. 빼낸 화물의 이동 연출은 받는 쪽이 한다.</summary>
        public Cargo TakeTopMatching(Func<Cargo, bool> predicate)
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var cargo = items[i];
                if (!predicate(cargo)) continue;

                items.RemoveAt(i);
                Version++;
                count.Value = items.Count;
                cargo.transform.DOKill();
                cargo.Visual.localPosition = Vector3.zero;
                cargo.Visual.localRotation = Quaternion.identity; // 휘어짐 기울기 되돌림
                Reflow(i);
                Bounce();
                return cargo;
            }
            return null;
        }

        void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            if (config == null || deltaTime <= 0f) return;

            // 관성 기울기: 이동 반대쪽으로 기울고 스프링으로 돌아온다. 위층일수록 많이 기운다.
            var position = anchor.position;
            var velocity = (position - lastAnchorPosition) / deltaTime;
            lastAnchorPosition = position;
            velocity.y = 0f;

            var target = Vector3.ClampMagnitude(-velocity * config.LeanPerSpeed, config.MaxLeanPerLevel);
            leanVelocity += (target - lean) * (config.SpringStiffness * deltaTime);
            leanVelocity *= Mathf.Exp(-config.SpringDamping * deltaTime);
            lean += leanVelocity * deltaTime;

            // 휘어짐: 층마다 기울기 합 = lean × LeanFactor(i) (곡선 — 위로 갈수록 더 크게), 상자는 아래 상자에서 이어진 방향으로 기운다
            var previous = 0f;
            for (var i = 0; i < items.Count; i++)
            {
                var cargo = items[i];
                var factor = config.LeanFactor(i);
                cargo.Visual.position = cargo.transform.position + lean * factor;
                if (config.TiltAlongCurve)
                {
                    var height = Mathf.Max(0.05f, cargo.HeightScale * config.BoxHeight);
                    var up = anchor.up * height + lean * (factor - previous);
                    cargo.Visual.rotation = Quaternion.FromToRotation(anchor.up, up) * cargo.transform.rotation;
                }
                previous = factor;
            }
        }

        /// <summary>아래 화물들의 높이 합 (납작한 쓰레기는 촘촘히 쌓인다).</summary>
        Vector3 SlotPosition(int index)
        {
            var y = 0f;
            for (var i = 0; i < index && i < items.Count; i++) y += items[i].HeightScale * config.BoxHeight;
            return new Vector3(0f, y, 0f);
        }

        void Reflow(int fromIndex)
        {
            for (var i = fromIndex; i < items.Count; i++)
            {
                var t = items[i].transform;
                t.DOKill();
                t.localRotation = Quaternion.identity;
                t.DOLocalMove(SlotPosition(i), config.ReflowDuration).SetEase(Ease.OutQuad);
            }
        }

        void Bounce()
        {
            bounce?.Kill(true);
            anchor.localScale = Vector3.one;
            bounce = anchor.DOPunchScale(config.StackBounce, config.StackBounceDuration, 6, 0.5f).SetLink(gameObject);
        }
    }
}
