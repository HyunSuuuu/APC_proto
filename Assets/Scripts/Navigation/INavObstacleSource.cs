using System.Collections.Generic;
using UnityEngine;

namespace APC.Navigation
{
    /// <summary>
    /// 장애물을 길찾기 코어에 넘기는 쪽 (게임 어댑터 · 자기 검사). 코어는 장애물이 충돌체인지 레일 데이터인지 모른다.
    /// </summary>
    public interface INavObstacleSource
    {
        /// <summary>월드 XZ 영역(x = X, y = Z)과 겹치는 장애물을 output에 더한다 (지우지 않음).</summary>
        void Collect(Rect worldXZ, List<NavBox> output);
    }
}
