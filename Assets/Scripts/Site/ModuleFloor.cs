using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 모듈 바닥 타일 데이터 (M14-2). 모듈을 tileSize(2m) 칸으로 나눠 칸마다 타일 모델(`Art/Model/Prop` fbx)과 회전(90° 단위)을 둔다.
    /// 칠하기 · 다시 깔기는 에디터 인스펙터(ModuleFloorEditor)가 하고, 깔린 타일은 자식 `FloorArt` 아래 저장된다 (실행 중엔 아무것도 안 함).
    /// 걷는 면 · 네비메시는 그대로 모듈 `Floor` 충돌체가 맡는다 (타일이 있으면 Floor 렌더러만 끈다).
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SiteModule))]
    public sealed class ModuleFloor : MonoBehaviour
    {
        public const string ArtName = "FloorArt";

        [SerializeField, Min(0.1f), Tooltip("타일 한 변 (m). 모듈 칸 10m = 5 × 5장")] float tileSize = 2f;
        [SerializeField, Tooltip("칸마다 타일 (x + z × 가로). 비면 그 칸은 안 깔림")] GameObject[] tiles = new GameObject[0];
        [SerializeField, Tooltip("칸마다 회전 (0~3 × 90°)")] int[] turns = new int[0];

        public float TileSize => tileSize;

        /// <summary>타일 칸 수 (x = 가로, y = 세로 = 로컬 z).</summary>
        public Vector2Int GridSize
        {
            get
            {
                var module = GetComponent<SiteModule>();
                return new Vector2Int(
                    Mathf.Max(1, Mathf.RoundToInt(module.Size.x * module.CellSize / tileSize)),
                    Mathf.Max(1, Mathf.RoundToInt(module.Size.y * module.CellSize / tileSize)));
            }
        }

        public GameObject GetTile(int x, int z)
        {
            var i = Index(x, z);
            return i < tiles.Length ? tiles[i] : null;
        }

        public int GetTurn(int x, int z)
        {
            var i = Index(x, z);
            return i < turns.Length ? turns[i] : 0;
        }

        public void Set(int x, int z, GameObject tile, int turn)
        {
            var count = GridSize.x * GridSize.y;
            if (tiles.Length != count) System.Array.Resize(ref tiles, count);
            if (turns.Length != count) System.Array.Resize(ref turns, count);
            var i = Index(x, z);
            tiles[i] = tile;
            turns[i] = ((turn % 4) + 4) % 4;
        }

        /// <summary>칸 가운데 (모듈 로컬, y = 0).</summary>
        public Vector3 CellCenter(int x, int z)
        {
            var grid = GridSize;
            return new Vector3((x - (grid.x - 1) * 0.5f) * tileSize, 0f, (z - (grid.y - 1) * 0.5f) * tileSize);
        }

        int Index(int x, int z) => x + z * GridSize.x;
    }
}
