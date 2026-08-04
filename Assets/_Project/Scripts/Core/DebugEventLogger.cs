using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// GameEvents 전량 콘솔 로그 (개발 빌드 한정 — D2 체크포인트 판정 근거: 공격 발동→전조→상쇄/오염,
    /// 페이즈 전이, 게임오버 3종, 재시작이 콘솔 로그만으로 판정 가능해야 한다).
    /// 연속 값(Sanity·걸쇠 진행률)은 버킷 단위로만 로그 — 스팸 방지. 릴리스 빌드에서는 빈 컴포넌트.
    /// </summary>
    public sealed class DebugEventLogger : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _sanityBucket = int.MinValue;  // 5% 단위
        private int _latchBucket = int.MinValue;   // 25% 단위

        private void OnEnable()
        {
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.AttackTelegraphStarted += OnTelegraphStarted;
            GameEvents.AttackResolved += OnAttackResolved;
            GameEvents.CornerStageChanged += OnCornerStageChanged;
            GameEvents.SanityChanged += OnSanityChanged;
            GameEvents.TalismanBurned += OnTalismanBurned;
            GameEvents.GameEventFired += OnGameEventFired;
            GameEvents.TrueSignalStarted += OnTrueSignalStarted;
            GameEvents.PlayerStateChanged += OnPlayerStateChanged;
            GameEvents.TVToggled += OnTVToggled;
            GameEvents.GameOver += OnGameOver;
            GameEvents.EndingStarted += OnEndingStarted;
            GameEvents.DoorLatchProgressChanged += OnDoorLatchProgressChanged;
            GameEvents.PrayerChannelChanged += OnPrayerChannelChanged;
            GameEvents.JarChannelChanged += OnJarChannelChanged;
            GameEvents.BlanketExitChanged += OnBlanketExitChanged;
            GameEvents.UrgeChanged += OnUrgeChanged;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.AttackTelegraphStarted -= OnTelegraphStarted;
            GameEvents.AttackResolved -= OnAttackResolved;
            GameEvents.CornerStageChanged -= OnCornerStageChanged;
            GameEvents.SanityChanged -= OnSanityChanged;
            GameEvents.TalismanBurned -= OnTalismanBurned;
            GameEvents.GameEventFired -= OnGameEventFired;
            GameEvents.TrueSignalStarted -= OnTrueSignalStarted;
            GameEvents.PlayerStateChanged -= OnPlayerStateChanged;
            GameEvents.TVToggled -= OnTVToggled;
            GameEvents.GameOver -= OnGameOver;
            GameEvents.EndingStarted -= OnEndingStarted;
            GameEvents.DoorLatchProgressChanged -= OnDoorLatchProgressChanged;
            GameEvents.PrayerChannelChanged -= OnPrayerChannelChanged;
            GameEvents.JarChannelChanged -= OnJarChannelChanged;
            GameEvents.BlanketExitChanged -= OnBlanketExitChanged;
            GameEvents.UrgeChanged -= OnUrgeChanged;
        }

        private static void OnPhaseChanged(PhaseId phase) => Debug.Log($"[EVT] PhaseChanged → {phase}");
        private static void OnTelegraphStarted(int corner, float duration)
            => Debug.Log($"[EVT] AttackTelegraphStarted corner={corner} dur={duration:F1}s");
        private static void OnAttackResolved(int corner, bool countered)
            => Debug.Log($"[EVT] AttackResolved corner={corner} {(countered ? "상쇄" : "오염")}");
        private static void OnCornerStageChanged(int corner, int stage)
            => Debug.Log($"[EVT] CornerStageChanged corner={corner} stage={stage}");
        private static void OnTalismanBurned() => Debug.Log("[EVT] TalismanBurned");
        private static void OnGameEventFired(EventDef def) => Debug.Log($"[EVT] GameEventFired id={def.Id} kind={def.Kind}");
        private static void OnTrueSignalStarted() => Debug.Log("[EVT] TrueSignalStarted");
        private static void OnPlayerStateChanged(PlayerState state) => Debug.Log($"[EVT] PlayerState → {state}");
        private static void OnTVToggled(bool isOn) => Debug.Log($"[EVT] TVToggled → {(isOn ? "ON" : "OFF")}");
        private static void OnGameOver(GameOverReason reason) => Debug.Log($"[EVT] GameOver reason={reason}");
        private static void OnEndingStarted(EndingKind kind) => Debug.Log($"[EVT] EndingStarted kind={kind}");
        private static void OnUrgeChanged(bool active) => Debug.Log($"[EVT] UrgeChanged → {(active ? "발생" : "해소")}");

        private void OnSanityChanged(float s01)
        {
            int bucket = Mathf.FloorToInt(s01 * 20f); // 5% 단위
            if (bucket == _sanityBucket) return;
            _sanityBucket = bucket;
            Debug.Log($"[EVT] SanityChanged ≈ {s01 * 100f:F0}%");
        }

        private void OnDoorLatchProgressChanged(float p01)
        {
            int bucket = Mathf.FloorToInt(p01 * 4f); // 25% 단위
            if (bucket == _latchBucket) return;
            _latchBucket = bucket;
            Debug.Log($"[EVT] DoorLatchProgress ≈ {p01 * 100f:F0}%");
        }

        private int _prayerBucket = int.MinValue; // 25% 단위 — 연속 값 스팸 방지 규약 동일
        private int _jarBucket = int.MinValue;
        private int _blanketBucket = int.MinValue;

        private void OnPrayerChannelChanged(float p01, int aimedCorner)
        {
            int bucket = Mathf.FloorToInt(p01 * 4f) * 10 + aimedCorner; // 진행 버킷+조준 변화 모두 감지
            if (bucket == _prayerBucket) return;
            _prayerBucket = bucket;
            Debug.Log($"[EVT] PrayerChannel ≈ {p01 * 100f:F0}% aim={aimedCorner}");
        }

        private void OnJarChannelChanged(float p01)
        {
            int bucket = Mathf.FloorToInt(p01 * 4f);
            if (bucket == _jarBucket) return;
            _jarBucket = bucket;
            Debug.Log($"[EVT] JarChannel ≈ {p01 * 100f:F0}%");
        }

        private void OnBlanketExitChanged(float p01)
        {
            int bucket = Mathf.FloorToInt(p01 * 4f);
            if (bucket == _blanketBucket) return;
            _blanketBucket = bucket;
            Debug.Log($"[EVT] BlanketExit ≈ {p01 * 100f:F0}%");
        }
#endif
    }
}
