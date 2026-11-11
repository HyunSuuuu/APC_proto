using UnityEngine;

namespace APC.UI
{
    /// <summary>
    /// HUD 초기화. 씬에는 HUD Canvas 아래 UI를 **모두 꺼진 채로** 저장하고(Build Site), 실행하면 이것이 차례대로 켠다.
    /// 켜지는 순간 각 UI의 Awake가 자기 초기 상태를 잡는다 (팝업은 body 숨김, 조이스틱은 바탕 숨김 등).
    /// 에디터에서 켜 둔 채 저장된 팝업 · 딤이 화면을 덮어 클릭을 가로채는 일을 막는다.
    /// GameManager(−1000) 다음, 다른 스크립트보다 먼저 돌아 팝업 Instance가 다른 스크립트의 Start 전에 등록된다.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class UiBootstrap : MonoBehaviour
    {
        void Awake() => Initialize();

        /// <summary>모든 자식 UI를 끈 뒤 순서대로 다시 켠다.</summary>
        void Initialize()
        {
            var root = transform;
            for (var i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(false);
            for (var i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(true);
        }
    }
}
