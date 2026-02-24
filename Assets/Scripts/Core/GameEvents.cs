// ============================================================
// ThemeParkGame - Event System
// ゲーム全体のイベント管理（Observer Pattern）
// ============================================================

using System;
using System.Collections.Generic;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// 型安全なイベントバス。各システム間の疎結合な通信を実現する。
    /// </summary>
    public static class GameEvents
    {
        // ---- 来場者関連イベント ----
        public static event Action<int> OnVisitorEnterPark;
        public static event Action<int> OnVisitorLeavePark;
        public static event Action<int, EmotionBubbleType> OnVisitorEmotionChanged;
        public static event Action<int, float> OnVisitorHappinessChanged;
        public static event Action<int> OnVisitorVomited;
        public static event Action<int> OnVisitorHadAccident; // トイレ漏れ
        public static event Action<int, string> OnVisitorSaidSomething; // AI会話
        public static event Action<int, float, float> OnVisitorSatisfactionChanged; // visitorId, newScore, delta

        // ---- スタッフ関連イベント ----
        public static event Action<int, StaffType> OnStaffHired;
        public static event Action<int, StaffType> OnStaffFired;
        public static event Action<int> OnStaffWentOnStrike;
        public static event Action<int> OnStaffFinishedTask;

        // ---- アトラクション関連イベント ----
        public static event Action<int> OnAttractionBuilt;
        public static event Action<int> OnAttractionBrokenDown;
        public static event Action<int> OnAttractionRepaired;
        public static event Action<int> OnAttractionAccident;
        public static event Action<int> OnAttractionUpgraded;

        // ---- 経済関連イベント ----
        public static event Action<float> OnMoneyChanged;
        public static event Action<float> OnRevenueEarned;
        public static event Action<float> OnExpensePaid;
        public static event Action<int, float> OnPriceChanged; // facilityId, newPrice

        // ---- パーク関連イベント ----
        public static event Action<ThemeZone> OnThemeZoneUnlocked;
        public static event Action<int> OnGoldenTicketEarned;
        public static event Action<CertificateCategory> OnCertificateAwarded;
        public static event Action<Weather> OnWeatherChanged;
        public static event Action OnParkOpened;
        public static event Action OnParkClosed;
        public static event Action<int> OnParkYearPassed; // year number

        // ---- 研究開発関連イベント ----
        public static event Action<string> OnResearchStarted;
        public static event Action<string> OnResearchCompleted;

        // ---- VIP関連イベント ----
        public static event Action<int> OnVIPArrived;
        public static event Action<int, bool> OnVIPRequestCompleted; // vipId, success

        // ---- UI/視点関連イベント ----
        public static event Action<ViewMode> OnViewModeChanged;
        public static event Action<int> OnFacilitySelected;
        public static event Action<int> OnVisitorSelected;

        // ---- AI会話関連イベント ----
        public static event Action<int, string> OnNPCConversationStarted;
        public static event Action<int, string> OnNPCConversationEnded;
        public static event Action<string> OnQuestGenerated;
        public static event Action<int, string> OnSNSPostGenerated; // visitorId, content

        // ==== Fire メソッド群 ====

        // 来場者
        public static void FireVisitorEnterPark(int visitorId) => OnVisitorEnterPark?.Invoke(visitorId);
        public static void FireVisitorLeavePark(int visitorId) => OnVisitorLeavePark?.Invoke(visitorId);
        public static void FireVisitorEmotionChanged(int visitorId, EmotionBubbleType emotion) => OnVisitorEmotionChanged?.Invoke(visitorId, emotion);
        public static void FireVisitorHappinessChanged(int visitorId, float happiness) => OnVisitorHappinessChanged?.Invoke(visitorId, happiness);
        public static void FireVisitorVomited(int visitorId) => OnVisitorVomited?.Invoke(visitorId);
        public static void FireVisitorHadAccident(int visitorId) => OnVisitorHadAccident?.Invoke(visitorId);
        public static void FireVisitorSaidSomething(int visitorId, string message) => OnVisitorSaidSomething?.Invoke(visitorId, message);
        public static void FireVisitorSatisfactionChanged(int visitorId, float newScore, float delta) => OnVisitorSatisfactionChanged?.Invoke(visitorId, newScore, delta);

        // スタッフ
        public static void FireStaffHired(int staffId, StaffType type) => OnStaffHired?.Invoke(staffId, type);
        public static void FireStaffFired(int staffId, StaffType type) => OnStaffFired?.Invoke(staffId, type);
        public static void FireStaffWentOnStrike(int staffId) => OnStaffWentOnStrike?.Invoke(staffId);
        public static void FireStaffFinishedTask(int staffId) => OnStaffFinishedTask?.Invoke(staffId);

        // アトラクション
        public static void FireAttractionBuilt(int attractionId) => OnAttractionBuilt?.Invoke(attractionId);
        public static void FireAttractionBrokenDown(int attractionId) => OnAttractionBrokenDown?.Invoke(attractionId);
        public static void FireAttractionRepaired(int attractionId) => OnAttractionRepaired?.Invoke(attractionId);
        public static void FireAttractionAccident(int attractionId) => OnAttractionAccident?.Invoke(attractionId);
        public static void FireAttractionUpgraded(int attractionId) => OnAttractionUpgraded?.Invoke(attractionId);

        // 経済
        public static void FireMoneyChanged(float amount) => OnMoneyChanged?.Invoke(amount);
        public static void FireRevenueEarned(float amount) => OnRevenueEarned?.Invoke(amount);
        public static void FireExpensePaid(float amount) => OnExpensePaid?.Invoke(amount);
        public static void FirePriceChanged(int facilityId, float price) => OnPriceChanged?.Invoke(facilityId, price);

        // パーク
        public static void FireThemeZoneUnlocked(ThemeZone zone) => OnThemeZoneUnlocked?.Invoke(zone);
        public static void FireGoldenTicketEarned(int count) => OnGoldenTicketEarned?.Invoke(count);
        public static void FireCertificateAwarded(CertificateCategory category) => OnCertificateAwarded?.Invoke(category);
        public static void FireWeatherChanged(Weather weather) => OnWeatherChanged?.Invoke(weather);
        public static void FireParkOpened() => OnParkOpened?.Invoke();
        public static void FireParkClosed() => OnParkClosed?.Invoke();
        public static void FireParkYearPassed(int year) => OnParkYearPassed?.Invoke(year);

        // 研究
        public static void FireResearchStarted(string researchId) => OnResearchStarted?.Invoke(researchId);
        public static void FireResearchCompleted(string researchId) => OnResearchCompleted?.Invoke(researchId);

        // VIP
        public static void FireVIPArrived(int vipId) => OnVIPArrived?.Invoke(vipId);
        public static void FireVIPRequestCompleted(int vipId, bool success) => OnVIPRequestCompleted?.Invoke(vipId, success);

        // UI/視点
        public static void FireViewModeChanged(ViewMode mode) => OnViewModeChanged?.Invoke(mode);
        public static void FireFacilitySelected(int facilityId) => OnFacilitySelected?.Invoke(facilityId);
        public static void FireVisitorSelected(int visitorId) => OnVisitorSelected?.Invoke(visitorId);

        // AI
        public static void FireNPCConversationStarted(int npcId, string topic) => OnNPCConversationStarted?.Invoke(npcId, topic);
        public static void FireNPCConversationEnded(int npcId, string summary) => OnNPCConversationEnded?.Invoke(npcId, summary);
        public static void FireQuestGenerated(string questData) => OnQuestGenerated?.Invoke(questData);
        public static void FireSNSPostGenerated(int visitorId, string content) => OnSNSPostGenerated?.Invoke(visitorId, content);

        // ---- カメラ関連イベント ----
        public static event Action<int> OnCameraFollowRequested;
        public static void FireCameraFollowRequested(int targetId) => OnCameraFollowRequested?.Invoke(targetId);

        // ---- セーブ/ロード関連イベント ----
        public static event Action OnGameSaved;
        public static event Action OnGameLoaded;
        public static void FireGameSaved() => OnGameSaved?.Invoke();
        public static void FireGameLoaded() => OnGameLoaded?.Invoke();

        // ================================================================
        // イベントクリーンアップ
        // ================================================================

        /// <summary>
        /// 全イベントの購読を解除する。
        /// シーン遷移時やゲーム終了時に呼び出し、メモリリークを防止する。
        /// </summary>
        public static void ClearAll()
        {
            // 来場者
            OnVisitorEnterPark = null;
            OnVisitorLeavePark = null;
            OnVisitorEmotionChanged = null;
            OnVisitorHappinessChanged = null;
            OnVisitorVomited = null;
            OnVisitorHadAccident = null;
            OnVisitorSaidSomething = null;
            OnVisitorSatisfactionChanged = null;

            // スタッフ
            OnStaffHired = null;
            OnStaffFired = null;
            OnStaffWentOnStrike = null;
            OnStaffFinishedTask = null;

            // アトラクション
            OnAttractionBuilt = null;
            OnAttractionBrokenDown = null;
            OnAttractionRepaired = null;
            OnAttractionAccident = null;
            OnAttractionUpgraded = null;

            // 経済
            OnMoneyChanged = null;
            OnRevenueEarned = null;
            OnExpensePaid = null;
            OnPriceChanged = null;

            // パーク
            OnThemeZoneUnlocked = null;
            OnGoldenTicketEarned = null;
            OnCertificateAwarded = null;
            OnWeatherChanged = null;
            OnParkOpened = null;
            OnParkClosed = null;
            OnParkYearPassed = null;

            // 研究
            OnResearchStarted = null;
            OnResearchCompleted = null;

            // VIP
            OnVIPArrived = null;
            OnVIPRequestCompleted = null;

            // UI/視点
            OnViewModeChanged = null;
            OnFacilitySelected = null;
            OnVisitorSelected = null;

            // AI
            OnNPCConversationStarted = null;
            OnNPCConversationEnded = null;
            OnQuestGenerated = null;
            OnSNSPostGenerated = null;

            // カメラ
            OnCameraFollowRequested = null;

            // セーブ/ロード
            OnGameSaved = null;
            OnGameLoaded = null;
        }
    }
}
