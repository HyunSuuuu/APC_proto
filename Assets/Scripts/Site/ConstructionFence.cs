using TMPro;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 다음에 해금될 칸을 두르는 공사장 가설 울타리 (GDD 4.1, 9.3). 표지에 `잠금해제 : LV N`을 띄운다.
    /// 칸 하나 크기이고 칸 중심에 놓는다. SiteUnlocker가 만들고 지운다. 울타리는 칸 판정 밖이라 들어갈 수 없다.
    /// </summary>
    public sealed class ConstructionFence : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public void SetLevel(int level, bool showLabel)
        {
            if (label == null) return;
            label.gameObject.SetActive(showLabel);
            label.text = $"잠금해제 : LV {level}";
        }
    }
}
