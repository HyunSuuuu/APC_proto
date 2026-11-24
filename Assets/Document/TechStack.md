# 앙팡물류센터 — 기술 스택

> 프로토타입 구현 기준. 게임 규칙은 [GDD.md](GDD.md) 참고.

## 1. 환경

| 항목 | 내용 |
|---|---|
| 엔진 | Unity **6000.3.26f1** |
| 렌더 파이프라인 | URP 17.3.0 — 모바일은 `Assets/Settings/Mobile_RPAsset` |
| 타깃 | Android / iOS, **가로 고정** (현재 Player 설정은 자동 회전 → 변경 필요) |
| 테스트 빌드 | **Windows x64** (Mono), 버전 `0.1.0`, 창 모드 1280 × 720 · 크기 조절 · 백그라운드 실행, 빌드 씬 = `Game.unity`만 (2026-11-17). `DebugMenu`는 Development Build에서만 보인다 |
| 입력 처리 | Input System 전용 (`Active Input Handling = Input System Package`) |
| IDE | VS Code (`com.unity.ide.visualstudio`로 csproj 생성) |

## 2. 패키지

| 패키지 | 버전 | 설치 방식 | 용도 |
|---|---|---|---|
| DOTween **Pro** (+ DemiLib) | - | `Assets/Plugins/Demigiant` (에셋 스토어) | 오브젝트·UI 연출, 인스펙터 연출(`DOTweenAnimation`), 경로 편집(`DOTweenPath`) |
| R3 | 1.3.1 | 코어: NuGetForUnity (`Assets/packages.config`) / Unity 확장: Git UPM | 상태 바인딩, 게임 이벤트 |
| NuGetForUnity | 4.5.0 | Git UPM | R3 코어 DLL 설치·복원 |
| MCP for Unity (UnityMCP) | 10.3.0 | Git UPM | AI(Claude Code)의 에디터 조작 — **개발 전용** |
| Universal RP | 17.3.0 | Unity 레지스트리 | 렌더링 |
| Input System | 1.20.0 | Unity 레지스트리 | 조이스틱, 드래그, 핀치 |
| uGUI (TextMeshPro 포함) | 2.0.0 | Unity 레지스트리 | HUD, 팝업 |
| AI Navigation | 2.0.14 | Unity 레지스트리 | `NavMeshSurface` 런타임 재굽기, 알바 경로 탐색 |
| Visual Studio Editor | 2.0.26 | Unity 레지스트리 | VS Code 연동 |
| 2D Sprite | 1.0.0 | Unity 레지스트리 | Sprite Editor — `Art/Temp` UI 그림 자르기(Multiple, 2026-11-18). 에디터 전용 |

Git UPM 패키지는 모두 **태그로 버전을 고정**했다 (`Packages/manifest.json`). 업데이트할 때는 태그를 바꾼다.

### 2.1 함께 넣은 설정

| 설정 | 위치 | 이유 |
|---|---|---|
| `NuGet.config`, `packages.config` | `Assets/` | R3 코어와 의존 DLL 6개를 `Assets/Packages/`에 복원 |
| `.mcp.json` | 프로젝트 루트 | Claude Code에 UnityMCP 서버 등록 (`http://localhost:8080/mcp`). 연결 방법은 [4.4](#44-unitymcp--개발-보조) |

## 3. 처음 열 때 확인할 것

1. 프로젝트를 열면 Package Manager가 Git 패키지를 받는다 (**Git for Windows가 PATH에 있어야 한다**). 없으면 R3.Unity가 빠져 `.AddTo(this)`에서 CS1620이 대량으로 난다 (2026-11-11 실제 발생, Git 설치로 해결).
2. NuGetForUnity가 `packages.config`를 읽어 R3 DLL을 자동 복원한다. 복원 전에는 R3 관련 컴파일 에러가 잠깐 뜰 수 있다 → 복원 후 재컴파일되면 사라짐. 복원이 안 되면 `NuGet > Restore Packages`를 실행한다.
   - **안전 모드(Safe Mode) 주의:** 첫 실행 때 이 에러 때문에 안전 모드를 띄우라는 창이 나오면 **Ignore**를 고른다. 안전 모드에서는 NuGetForUnity가 실행되지 않아 복원도 되지 않는다. 그러면 에러가 풀리지 않아 안전 모드를 계속 빠져나오지 못한다.
   - 이미 안전 모드에 들어갔다면, 같은 버전의 `Assets/Packages/`를 다른 프로젝트(예: BPCP)에서 `.meta` 파일까지 통째로 복사한다. 그 뒤 `Ctrl+R`로 다시 불러오고 안전 모드를 종료한다.
3. `Tools > Demigiant > DOTween Utility Panel`에서 아래를 1회 실행한다.
   - **Setup DOTween**: 모듈 활성화. **TextMeshPro** 모듈을 반드시 체크한다 (Pro의 `DOTweenTextMeshPro` — HUD 숫자·MAX 텍스트 연출).
   - **Create ASMDEF** (선택): 우리 코드를 asmdef로 나눠 DOTween 모듈·Pro 스크립트를 참조해야 할 때만 실행한다. 기본 `Assembly-CSharp`에서 쓰면 필요 없다. 만들 때는 수동으로 만들지 말고 이 버튼을 쓴다 (Pro용 asmdef가 빠지지 않게).
4. UnityMCP: `Window > MCP for Unity`에서 서버를 시작한다 (HTTP `localhost:8080/mcp`). Claude Code의 `UnityMCP` 서버가 여기에 연결된다.

## 4. 도구별 사용처

### 4.1 DOTween — 연출

| 사용처 | GDD | 연출 |
|---|---|---|
| 자동 집기 / 내려놓기 | 5.3 | `DOJump`로 상자가 포물선을 그리며 스택에 안착, 스택 흔들림 |
| 시설 팝 · 부지 확장 | 9.2, 9.3 | `DOPunchScale`, 바닥·벽 모듈이 순서대로 이어 붙는 `Sequence` |
| 현금 흡수 | 7.6 | 현금 → 플레이어로 이동하며 축소 |
| 트럭 진입 · 출발 | 7.1, 8.3 | 주차장 모듈 안의 나타나는 위치 → 주차 위치로 `DOMove` 후진, 나갈 때 반대로 + 크기 연출. 도로 경로 없음 (모듈 독립성) |
| UI | 12 | 주문 카드 슬라이드·재정렬, 게이지 링, 재화 카운트업, 팝업 열기/닫기, MAX 바운스 |
| 텍스트 | 12 | **`DOTweenTextMeshPro`(Pro)** — 재화·XP 숫자 카운트업, 레벨업·MAX 텍스트 펀치 |

**Pro 기능 사용 기준**

| 기능 | 쓰는 곳 | 쓰지 않는 곳 |
|---|---|---|
| `DOTweenAnimation` (인스펙터 연출) | 고정된 UI 연출 — 팝업 열기/닫기, 버튼 반응, 배지 깜빡임. 기획·아트가 코드 수정 없이 조정 | 풀링 오브젝트(상자·현금), 대상·값이 런타임에 정해지는 연출 → 코드 트윈 |
| `DOTweenPath` | 지금은 쓰지 않음 (트럭 도로 경로를 없앰). 씬에 고정된 곡선 경로가 다시 필요할 때 | 알바 이동 (네비메시), 모듈 안 이동 (직선 `DOMove`로 충분) |
| 에디터 미리보기 (`DOTweenPreviewManager`) | 플레이 모드 없이 연출 확인 | - |

- `DOTweenPro Examples` 폴더는 예제 씬이다. 빌드에 들어가지 않지만 필요 없으면 삭제해도 된다.

### 4.2 R3 — 상태 바인딩 · 이벤트

| 사용처 | GDD |
|---|---|
| 재화 · XP · 레벨 · 하루 시계 `ReactiveProperty` → HUD | 12.1 |
| 선반 재고 → 외형 3단계 + 수량 표시 | 7.3 |
| 알바 스탯 → 파생값(적재, 속도, 작업 배율), 미지급 상태 → 빨간 ₩ 아이콘 | 11.2, 11.7 |
| 게임 이벤트: 주문 완료 → XP·현금, 레벨업 → 해금, 구매 → 저장 | 8.5, 10.2 |
| 튜토리얼 단계 완료 조건 | 13 |
| 자동 저장 묶기 (`Debounce`) | 14.1 |

### 4.3 순서형 흐름 — 코루틴 / Awaitable (별도 패키지 없음)

| 사용처 | GDD | 방식 |
|---|---|---|
| 트럭: 진입 → 정차 → 하차 완료 대기 → 출발 | 7.1, 7.6 | 상태 머신 (알바 AI와 같은 방식) |
| 파트 마감: 시급 정산 → 정산 팝업 [확인] → 다음 파트 · 고용 후보 교체 | 11.7 | 코루틴 (`WaitUntil(팝업 닫힘)`) |
| 레벨업: 축하 연출 → 팝업 → 확장 칸 해금 · 업그레이드 패드 등장 | 10.2 | DOTween `Sequence` + `OnComplete` |
| 튜토리얼 단계 진행 | 13 | 단계 매니저 + R3 조건 감지 |
| 시설 생성 → 네비메시 재굽기 | 11.5 | `NavMeshSurface.UpdateNavMesh` + 코루틴 또는 `Awaitable.FromAsyncOperation` |
| 저장 / 불러오기 파일 입출력 | 14.1 | 동기 처리로 시작 (데이터가 작음), 끊김이 보이면 `Awaitable.BackgroundThreadAsync` |

### 4.4 UnityMCP — 개발 보조

- GDD의 수치표(레벨, 비용, 회사·품목, 확장 칸)를 ScriptableObject 데이터로 생성·입력
- 임시 프리미티브 프리팹 생성, 부지 그리드(GDD 4.2) 배치
- 콘솔 에러 확인, 플레이 모드 실행 확인
- 빌드에는 영향 없음 (에디터 전용 패키지)
- **Claude Code 연결**: 프로젝트 루트 `.mcp.json`에 서버 `UnityMCP`(`http://localhost:8080/mcp`)가 등록돼 있다. Unity에서 `Window > MCP for Unity` 서버를 먼저 켠 뒤 Claude Code를 (재)시작하고, 프로젝트 MCP 서버 사용을 승인한다. 포트가 바뀌면 `.mcp.json`의 url을 고친다.

## 5. 사용 규칙

### 5.1 역할 분담

| 상황 | 사용 |
|---|---|
| 값이 바뀌면 다른 곳이 반응해야 함 (HUD, 외형, 저장) | **R3** |
| 순서가 있고 끝이 있는 흐름 (연출 → 대기 → 다음 단계) | **코루틴 / Awaitable** (또는 DOTween `Sequence` 콜백) |
| 매 프레임 판단하는 로직 (알바 상태 머신, 플레이어 이동, 존 감지 타이머) | **일반 `Update` + 상태 머신** — Rx/async로 짜지 않는다 |
| 눈에 보이는 움직임 | **DOTween** |

### 5.2 수명 관리

- R3 구독은 `.AddTo(this)`로 오브젝트 수명에 묶는다.
- 코루틴은 실행한 오브젝트가 꺼지면 함께 멈춘다. `Awaitable`을 쓸 때는 `destroyCancellationToken`을 넘겨서 오브젝트가 사라지면 취소되게 한다.
- 풀링하는 오브젝트(상자, 현금)의 트윈은 반환할 때 `Kill()`, 일반 오브젝트는 `SetLink(gameObject)`.

### 5.3 일시정지

GDD상 팝업이 열리면 게임이 멈추고 하루 타이머도 멈춘다.

| 대상 | 처리 |
|---|---|
| 게임 일시정지 | `Time.timeScale = 0` (게임 매니저 상태와 함께 변경) |
| UI 트윈 | `SetUpdate(true)` — 일시정지 중에도 재생 |
| UI 쪽 코루틴 대기 | `WaitForSecondsRealtime` — 일시정지 중에도 진행 |
| 하루 타이머 (10분) | `Time.deltaTime` 누적 — 일시정지·앱 종료 시 자동으로 멈춤. `WaitForSecondsRealtime(600)` 같은 실시간 대기는 쓰지 않는다 |

### 5.4 데이터

- 밸런스 수치는 모두 ScriptableObject로 뺀다 (GDD `임시` 값 = 조정 대상).
- 테이블: 회사·품목, 레벨·해금, 비용, 주문 생성 규칙, 확장 칸, 알바 스탯 생성 규칙, 플레이어, 조작·카메라.
- 진입점은 `Assets/Data/GameDatabase.asset` 하나. 새 테이블은 여기에 연결한다. (예외: 측정용 `NavGridConfig` · `NavBenchmarkConfig`는 메뉴가 에셋 경로로 읽음, 2026-11-24)
- **어셈블리 정의는 `APC.Navigation` 하나** (`Assets/Scripts/Navigation/`, 2026-11-24): 길찾기 코어를 게임 코드와 떼어 알파로 옮기기 위함. 이 폴더 코드는 `APC.*` 게임 코드를 참조하지 않는다 (UnityEngine만). 나머지는 그대로 Assembly-CSharp. 테스트 프레임워크 대신 `NavSelfTest.Run()` 자기 검사.
- 성능 측정은 **Deep Profile을 끄고** 한다 (켜면 C# 코드만 약 100배 느려짐). 메뉴 `APC > Test > Nav Benchmark`는 알아서 끄고 되돌린다.

### 5.5 입력

- 터치와 마우스는 `PointerInput`으로만 읽는다. 지금 닿은 손가락이 있으면 터치, 없으면 마우스 왼쪽 버튼을 손가락 1개로 본다. 터치스크린 장치 유무로 고르지 않는다 (PC에 터치 · 펜 장치가 잡혀 있으면 마우스가 무시되는 버그가 났었다).
- Input System의 `TouchSimulation`은 쓰지 않는다. 에디터 개인 설정(Input Debugger)에 따라 플레이 시작 시 강제로 꺼지기 때문이다.
- UI 위 터치 판정은 `UiHitTest` (uGUI 레이캐스트 + 디버그 메뉴 영역).

### 5.6 폰트 (TextMeshPro)

| 용도 | 폰트 에셋 |
|---|---|
| 기본 (본문 — 이력서, 정산 내역, 설명) — **TMP 기본 폰트** | `Fonts/SUITE-Medium SDF` |
| 숫자 · 제목 (HUD 현금·레벨, MAX, 팝업 제목) | `Fonts/TmoneyRoundWindExtraBold SDF` |
| 보조 | `Fonts/TmoneyRoundWindRegular SDF` |

- 세 에셋 모두 정적 아틀라스(4096², 한글 11,000자 이상)라 하나에 약 37MB → **Git LFS로 관리**한다 (`.gitattributes`).
- 정적 아틀라스에 없는 기호(`·`, `→` 등)는 TMP 전역 대체 폰트 `LiberationSans SDF - Fallback`(동적)에서 가져온다 (`TMP Settings`).
- 월드 글자에 외곽선이 필요하면 `tmp.outlineWidth`를 코드로 바꾸지 말고 외곽선 재질 에셋(`Fonts/... Outline.mat`)을 `fontSharedMaterial`로 쓴다. 코드로 바꾸면 재질 복사본이 생겨 프리팹에 저장되지 않는다.
- 처음 받을 때 `git lfs install`이 필요하다.

### 5.7 부지 모듈

부지는 **n × m 칸 모듈 프리팹을 config대로 붙여** 만든다 (GDD 4.2).

| 구성 | 역할 |
|---|---|
| `SiteModule` (모듈 프리팹 루트) | 크기(칸), 칸 경계 기즈모. 안에 바닥 · 존 · 시설을 담는다. 루트 = 차지하는 칸들의 중심 |
| `SiteLayout` (config, `Assets/Data`) | 배치 목록: id, 모듈 프리팹, 행 · 열(왼쪽 위 칸), 회전(0/90/180/270), 시작 여부, **해금 레벨 · 비용**, **같이 해금(`unlockWith`)** |
| `SiteGrid` (씬) | 붙은 모듈 목록 + 열린 칸 집합. `Unlock(id)` = 모듈을 팝 연출로 붙이고 칸을 엶, `OnUnlocked` 이벤트, `IsAreaWalkable` = 열린 칸 안인지 |
| `SiteUnlocker` (SiteGrid 오브젝트) | 다음 해금 1곳에 공사장 울타리(`ConstructionFence`, `잠금해제 : LV N`). 레벨이 되면 울타리 옆에 **해금 발판**(`ExpansionPad`, `SiteLayout.cost`)이 나오고 돈을 다 채우면 해금 (레벨 자동 해금은 2026-11-14 삭제) |
| 하차 · 상차장 모듈 (`Module_Lot*`) | 가장자리용. 주차장은 칸 **바깥 −x**, 회전으로 방향을 정한다 (0 = 서, 90 = 북, 180 = 동, 270 = 남). `_M`은 주차 칸을 반대로 비킨 판 |
| `SiteNavMesh` (Site 루트, `NavMeshSurface`) | 열린 모듈 바닥 충돌체에만 네비메시를 굽고, 해금마다 다시 굽는다 (임시 Ground는 `NavMeshModifier`로 제외) |
| `Build Site` (메뉴 `APC > Setup`) | config대로 시작 모듈을 **프리팹 인스턴스로** 씬에 굽는다. 잠긴 칸에는 아무것도 두지 않는다. HUD 자식 UI는 꺼진 채 저장하고 `UiBootstrap`이 켠다 |
| `New Module` (메뉴 `APC > Module`) | 빈 n × m 모듈 틀(바닥 + `Content`)을 `Assets/Prefabs/Modules`에 만든다 |
| `Rebuild Standard Modules` | 기본 모듈 프리팹을 코드 기준으로 다시 만든다 (덮어쓰기 — 프리팹에서 고친 내용은 사라짐) |
| `Apply Layout 6x4` (메뉴 `APC > Setup`, M12-1) | SiteLayout · 회사 선반 구역을 6 × 4 기본값으로(GDD 4.2), 보관 · 반품 · 쓰레기장 · 빈 공간 모듈만 다시 만들고 Build Site. 사무실 · 하차 · 상차 프리팹 · 레벨 표는 그대로 (옛 `Apply Layout v0.3` 대체) |
| `Apply M6 Staff` (메뉴 `APC > Setup`) | 기본 모듈 · 선반 · 알바 프리팹(`StaffWorker.prefab`)을 덮어쓰고 Build Site. config는 건드리지 않는다 |
| `Apply Character Models` (메뉴 `APC > Art`) | 플레이어 · 알바 모델(`Art/Model/Worker`, Generic 리그 `Bip001`) + 공용 Animator Controller `Ani/Character.controller`(idle · walk · carry · pull). 재질은 모델별 `M_Char_*`(URP Lit + `*_P.psd`, fbx 기본 재질은 텍스처 없음). 알바 프리팹(`Apply M6 Staff` 포함)은 모델이 자동으로 들어간다 |
| `Build Test Module` · `Log Prop Sizes` (메뉴 `APC > Art`) | Prop fbx 모델로 모듈 겉모습을 만드는 시험 도구. 바닥 타일 1장 = 1m, 모듈 1칸 = 10m (타일 10 × 10). fbx마다 import 스케일이 달라 **메시 실제 크기를 재서** 맞춘다. 공용 재질 `Assets/Art/Materials/M_Prop.mat`(`Color_Pallet.png`). fbx 기본 재질은 3ds Max 셰이더 그래프(`PhysicalMaterial3DsMax`)라 텍스처가 없다 → `M_Prop`으로 바꿔 씌운다. import `globalScale`은 모두 1 (`floor1_basic`만 10이던 것을 1로 고침, 2026-11-14) — 바닥 타일 1장 = 1 × 0.3 × 1m. **바뀜** (2026-11-16, M14-2): 바닥 · 도로 · 횡단보도 타일(`floor*` · `*road*` · `*crosswalk*`)은 Scale Factor **2**(2m, 모듈 1칸 = 5 × 5장) — `ModuleFloorEditor`가 팔레트를 읽을 때 맞춘다 |
| `Group Module Children` (메뉴 `APC > Art`, M14-5) | 하차 · 상차 · 반품 모듈 `Content` 자식을 기능 묶음 빈 부모(`TruckDock` · `UnloadSpace` · `ReturnShelf` · `RepackOutputArea`)로 모은다. 규칙은 `ModuleGrouping.Rules`. 이미 묶였으면 그대로 |
| `Background Layout` (메뉴 `APC > Art`, 2026-11-19) | 배경 모듈(`SiteLayout.background`) 배치 창. 팔레트 = `Prefabs/Modules/Background`의 프리팹(상자 안 세로 목록 · 스크롤, [팔레트 높이]로 조절 — 많아져도 창이 안 커짐), 칸 격자 왼클릭 = 놓기(같은 프리팹이면 90° 돌리기) · 오른클릭 = 지우기, 회색 = 부지 6 × 4(못 놓음), [부지 바깥 칸 수] 1~10. [씬 미리보기]를 켜면 편집 중 씬에 배경이 깔린다(저장 안 됨, Play · 창 닫기 때 지움 — 실제 배경은 실행 때 `SiteBackground`). 바뀐 값은 SiteLayout 에셋에 저장(Undo 됨) |
| `Add Module Floor` (메뉴 `APC > Art`, M14-2) | 모든 모듈 프리팹 루트에 `ModuleFloor`를 붙인다. 바닥은 **모듈 프리팹을 열고 루트를 골라 인스펙터에서 칠한다**: 팔레트(바닥 타일 fbx) 고르고 칸 격자 왼쪽 클릭 · 끌기 = 칠하기, 오른쪽 클릭 = 90° 회전, [붓으로 전체 채우기] · [같은 종류 무작위 섞기](이름 끝 숫자만 다른 타일) · [다시 깔기]. 타일은 `FloorArt` 아래 `M_Prop` 재질로 깔리고 바닥면 y = −0.6(보통 타일 윗면 = 0, 연석 타일은 0.12 솟음, 2026-11-17), 회색 `Floor` 렌더러는 꺼진다(충돌체 그대로) |

**새 모듈 추가 절차**
1. `APC > Module > New Module...` → 이름 · 크기 입력 → 생성
2. 프리팹을 열어 `Content` 아래에 존 · 시설을 넣는다 (좌표 = 모듈 중심 기준, 1칸 = −5 ~ +5)
3. `Assets/Data/SiteLayout`의 배치 목록에 추가: 모듈 · 행 · 열 · 회전 · 해금 레벨 · 비용
4. `APC > Setup > Build Site`

**ㄱ자처럼 직사각형이 아닌 구역 만들기 (같이 해금)**
1. 대표 모듈(예: E2 선반 구역 B)은 평소처럼 배치한다 — 이 배치의 레벨 · 비용 · 패드가 묶음 전체에 쓰인다.
2. 나머지 칸에 빈 타일 `Module_Empty`(1칸, 바닥만)나 다른 모듈을 배치하고 `unlockWith`에 대표 id(`E2`)를 적는다. 묶인 칸에 또 묶어도 된다(사슬).
3. Build Site. 해금하면(`SiteGrid.Unlock` — 묶음 안 어느 id를 넘겨도 같음) 묶음 전체가 한꺼번에 팝 연출로 열린다.
- 더 큰 빈 타일이 필요하면 `New Module`로 n × m 빈 모듈을 만들면 된다 (만들자마자 바닥만 있는 상태).

**규칙**
- 모듈은 **혼자 동작**해야 한다. 씬 오브젝트를 직접 참조하지 않는다 → 씬의 서비스는 `Instance`로 찾고(예: `ContractPopup.Instance`), 모듈 안의 것은 스스로 등록한다(예: `ShelfSlot.All`).
- 모듈 내용은 씬이 아니라 **프리팹**에서 고친다. 씬의 모듈은 프리팹 인스턴스라 자동으로 반영된다.
- 같은 모듈을 여러 칸에 재사용한다 (예: 하차 주차장 = R1C0 · E1, 상차 주차장 = 하차 주차장 모양을 180° 돌린 것).
- 회전해서 붙는 모듈의 바닥 글자는 `FlatLabelAligner`가 카메라 쪽으로 돌려 놓는다.
- 이동 제한은 물리 벽이 아니라 **칸 판정**(`SiteGrid.IsAreaWalkable`)으로 한다. 플레이어는 `PlayerMover`가 이동 전에 확인하고, 알바(M6)는 열린 모듈 바닥에만 네비메시를 구워 같은 효과를 낸다 (`SiteNavMesh`).
- 모듈 바닥(`Floor`)만 충돌체를 가진다. 네비메시가 바닥 충돌체로 구워지므로, 걸을 수 있는 면을 새로 만들면 충돌체를 남기고(`BuildKit.Cube(..., keepCollider: true)`), 장식에는 충돌체를 두지 않는다.
- 선반 등 시설은 `BoxCollider` + `NavMeshObstacle`(carve)로 알바 경로에서 피한다. 바닥에 구워지지 않게 `NavMeshModifier`(ignoreFromBuild)를 붙인다. 플레이어도 선반 충돌체에 막힌다. 트럭은 장애물로 두지 않는다 (주차 칸이 부지 밖).
- 알바 프리팹(`StaffWorker`)은 충돌체가 없고 NavMeshAgent 회피를 끈다 (플레이어 · 알바끼리 겹쳐 지나감).

### 5.7b 셰이더 · 렌더링

- 추가 렌더 패스는 렌더러 에셋(Render Objects 피처) 대신 코드로 넣는다 — `Visuals/OutlinePasses`처럼 `beginCameraRendering`에서 `EnqueuePass`. 같은 LightMode를 피처로도 넣으면 두 번 그려진다.
- 캐릭터 · 구조물 셰이더 교체는 Play 중 재질 사본으로 한다 (`CharacterToon` · `WallCutout`). 실행 코드에서 `Shader.Find`를 쓰지 않고 재질 참조로 셰이더를 빌드에 넣는다.
- 외곽선 스텐실 그룹: 일반 1~126(16의 배수 피함), 관찰 255. 새 외곽선 재질(`APC/OutlineStencil` · `APC/Toon`)을 만들면 `APC/Art/Validate Outline Materials`를 실행한다 (관찰 그룹만 `OutlineOuter` 켬).
- 캐릭터 툰은 손으로 쓴 HLSL `APC/Toon`(2026-11-24, Shader Graph에서 옮김 — 변형 17,492 → 9). 외곽선 3패스가 같은 셰이더에 있어 외곽선 슬롯을 따로 붙이지 않는다. 툰 그래프를 다시 만들지 않는다.
- 렌더러: Mobile = Forward, PC = Forward+ (Deferred 아님).

### 5.8 문서

- 스크립트를 추가 · 삭제하거나 역할이 바뀌면 **같은 커밋에서** [Wiki.md](Wiki.md)의 해당 항목(역할 · 동작 · 연결 · 주의)을 고친다.

## 6. 검토 중

| 항목 | 메모 |
|---|---|
| Cinemachine | 쿼터뷰 추적 + 부지 경계 제한(`CinemachineConfiner`)을 직접 구현하지 않아도 됨. 자유 이동 모드와 카메라 전환 구조 필요 |
| UniTask | 프로토타입에서 제외. 본 개발에서 비동기 흐름(서버 통신, 에셋 로딩, `WhenAll`)이 많아지면 재검토 |
| ObservableCollections.R3 | 주문 큐처럼 목록 변화를 UI에 바인딩할 때 |
| `Assets/Packages/` 커밋 여부 | NuGet 복원 결과물. 오프라인·CI 안정성을 위해 커밋 권장 |
