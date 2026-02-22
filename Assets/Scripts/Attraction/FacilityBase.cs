// ============================================================
// ThemeParkGame - FacilityBase
// 全設置型施設の抽象基底クラス
// アトラクション・ショップ・トイレ等すべてが継承する
// ============================================================

using System;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// パーク内に設置可能な全施設の抽象基底クラス。
    /// グリッドベースの配置システム、アクティブ状態管理、
    /// 来場者とのインタラクションインターフェースを提供する。
    /// </summary>
    public abstract class FacilityBase : MonoBehaviour
    {
        // ---- 識別・基本情報 ----

        /// <summary>施設の一意なランタイムID（配置時に採番）</summary>
        [Header("Facility Identity")]
        [SerializeField] private int facilityId;
        public int FacilityId
        {
            get => facilityId;
            set => facilityId = value;
        }

        /// <summary>施設タイプ</summary>
        public abstract FacilityType FacilityType { get; }

        /// <summary>施設の表示名</summary>
        public abstract string DisplayName { get; }

        // ---- 配置情報 ----

        /// <summary>グリッド上の配置座標（左下基準）</summary>
        [Header("Placement")]
        [SerializeField] private Vector2Int gridPosition;
        public Vector2Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }

        /// <summary>グリッド上のサイズ（タイル数）</summary>
        [SerializeField] private Vector2Int gridSize = new Vector2Int(1, 1);
        public Vector2Int GridSize
        {
            get => gridSize;
            set => gridSize = value;
        }

        /// <summary>施設の向き（0, 90, 180, 270度）</summary>
        [SerializeField] private int rotationAngle;
        public int RotationAngle
        {
            get => rotationAngle;
            set => rotationAngle = value % 360;
        }

        // ---- 費用 ----

        /// <summary>建設費用</summary>
        [Header("Cost")]
        [SerializeField] private int buildCost;
        public int BuildCost
        {
            get => buildCost;
            set => buildCost = value;
        }

        // ---- 稼働状態 ----

        /// <summary>施設がアクティブ（稼働可能）かどうか</summary>
        [Header("State")]
        [SerializeField] private bool isActive = true;
        public bool IsActive
        {
            get => isActive;
            private set
            {
                if (isActive != value)
                {
                    isActive = value;
                    OnActiveStateChanged(value);
                }
            }
        }

        /// <summary>建設が完了しているか</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>所属するテーマゾーン</summary>
        [SerializeField] private ThemeZone themeZone;
        public ThemeZone ThemeZone
        {
            get => themeZone;
            set => themeZone = value;
        }

        // ---- ライフサイクル ----

        protected virtual void Awake()
        {
            IsBuilt = false;
        }

        protected virtual void Start() { }

        protected virtual void Update() { }

        // ---- 建設・配置 ----

        /// <summary>
        /// 施設をグリッド上に配置する。
        /// 配置バリデーションを通過した場合のみ成功する。
        /// </summary>
        /// <param name="position">グリッド座標</param>
        /// <param name="zone">配置先テーマゾーン</param>
        /// <returns>配置成功ならtrue</returns>
        public bool Place(Vector2Int position, ThemeZone zone)
        {
            if (!ValidatePlacement(position, zone))
            {
                Debug.LogWarning($"[FacilityBase] 配置バリデーション失敗: {DisplayName} at {position}");
                return false;
            }

            GridPosition = position;
            ThemeZone = zone;
            transform.position = GridToWorldPosition(position);
            IsBuilt = true;
            IsActive = true;

            OnPlaced();
            return true;
        }

        /// <summary>
        /// 配置可能かどうかを検証する。
        /// サブクラスで追加条件を実装可能。
        /// </summary>
        /// <param name="position">配置候補のグリッド座標</param>
        /// <param name="zone">配置先テーマゾーン</param>
        /// <returns>配置可能ならtrue</returns>
        public virtual bool ValidatePlacement(Vector2Int position, ThemeZone zone)
        {
            // 基本チェック: グリッド座標が有効範囲内か
            if (position.x < 0 || position.y < 0)
                return false;

            // サイズ分の全タイルが空いているかのチェックは
            // ParkManagerのグリッドシステムに委譲する
            return true;
        }

        /// <summary>施設を撤去する</summary>
        public virtual void Demolish()
        {
            IsActive = false;
            IsBuilt = false;
            OnDemolished();
            Debug.Log($"[FacilityBase] 施設撤去: {DisplayName} (ID: {FacilityId})");
        }

        /// <summary>施設のアクティブ状態を切り替える</summary>
        public void SetActive(bool active)
        {
            if (!IsBuilt) return;
            IsActive = active;
        }

        // ---- 来場者インタラクション ----

        /// <summary>
        /// 来場者がこの施設を利用可能かどうかを返す。
        /// 稼働状態・定員・その他の条件を考慮する。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        /// <returns>利用可能ならtrue</returns>
        public abstract bool CanAcceptVisitor(int visitorId);

        /// <summary>
        /// 来場者が施設に到着し、利用を開始する。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        /// <returns>利用開始に成功したらtrue</returns>
        public abstract bool OnVisitorArrive(int visitorId);

        /// <summary>
        /// 来場者が施設の利用を終えて退出する。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        public abstract void OnVisitorLeave(int visitorId);

        /// <summary>
        /// 来場者に対するこの施設の魅力度を計算する。
        /// 来場者AIの行動選択で使用される。
        /// </summary>
        /// <param name="visitorId">来場者ID</param>
        /// <returns>0.0～1.0の魅力度スコア</returns>
        public virtual float CalculateAppeal(int visitorId)
        {
            if (!IsActive) return 0f;
            return 0.5f; // デフォルトの魅力度
        }

        // ---- ユーティリティ ----

        /// <summary>グリッド座標をワールド座標に変換する</summary>
        protected Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            // 1グリッド = 1ユニットとして変換（実装はParkManagerのグリッド設定に依存）
            float tileSize = 1f;
            return new Vector3(
                gridPos.x * tileSize + (gridSize.x * tileSize * 0.5f),
                0f,
                gridPos.y * tileSize + (gridSize.y * tileSize * 0.5f)
            );
        }

        /// <summary>この施設が占有するグリッドタイルの一覧を返す</summary>
        public Vector2Int[] GetOccupiedTiles()
        {
            var tiles = new Vector2Int[gridSize.x * gridSize.y];
            int index = 0;
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    tiles[index++] = new Vector2Int(gridPosition.x + x, gridPosition.y + y);
                }
            }
            return tiles;
        }

        // ---- 拡張ポイント ----

        /// <summary>配置完了時のコールバック</summary>
        protected virtual void OnPlaced() { }

        /// <summary>撤去時のコールバック</summary>
        protected virtual void OnDemolished() { }

        /// <summary>アクティブ状態変更時のコールバック</summary>
        protected virtual void OnActiveStateChanged(bool active) { }
    }
}
