Dust FX — 걷기 먼지 / 작업 구름 이펙트
=====================================

요구 사항
- Unity 6 이상, URP 프로젝트
  (Built-in / HDRP 에서는 머티리얼 셰이더를 해당 파이프라인의 파티클 셰이더로 바꿔주세요)

들어 있는 것
- Prefabs/VFX_WalkDust.prefab  걷기 먼지 (툰 스타일)
- Prefabs/VFX_WorkDust.prefab  작업 구름 (본체 구름 + Pops 작은 구름 + Debris 부스러기)
- Materials/, Textures/        프리팹이 쓰는 머티리얼과 텍스처
- 스크립트는 들어 있지 않습니다. 재생/정지는 쓰는 쪽 코드에서 ParticleSystem.Play()/Stop() 으로 합니다.

사용법
1) VFX_WalkDust
   - 캐릭터의 자식으로 넣고 발밑(바닥 높이)에 둡니다.
   - Emission > Rate over Distance 로 이동한 거리만큼만 나옵니다. 멈추면 자동으로 끊깁니다.
   - 양 조절: Rate over Distance (1m 이동당 개수).
     이동 속도가 6m/s 라면 1.2 정도가 걷는 느낌, 5 이상은 뛰는 느낌입니다.

2) VFX_WorkDust
   - 작업대 윗면 중앙에 둡니다. 작업대 프리팹의 자식으로 넣으면 모든 작업대에 들어갑니다.
   - 루트에서 Play() / Stop() 하면 자식(Pops, Debris)도 같이 재생/정지됩니다.
   - 처음부터 꺼두려면 루트와 자식의 Play On Awake 를 끄세요.
   - 범위 조절: 루트 Shape > Scale (기본 0.9 x 0.3 x 0.45)
   - 농도 조절: 루트 Emission > Rate over Time (기본 22)
