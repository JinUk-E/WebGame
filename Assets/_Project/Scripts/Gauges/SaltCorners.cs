using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 소금 4귀퉁이 0~2단계 (백/회/흑). 인덱스 규약: 0=좌상 1=우상 2=좌하 3=우하 (CornerIndex).
    /// 전이: 공격 판정 미대응 +1(AttackScheduler) / 소금 뿌리기 완료 −1(SaltInteractable).
    ///
    /// <para>
    /// <b>v0.7에서 무엇이 빠졌나.</b>
    /// ① <b>흑화 심화</b>(_deepened/IsSaturated) — 유일한 게임플레이 효과가 "기도 채널 ×1.5"였고 기도가 사라졌다.
    ///    아무것도 하지 않는 플래그와, 그 플래그를 위해 존재하던 공격 재타겟 로직 45줄이 함께 사라진다.
    /// ② <b>봉인 붕괴</b>(AllBlack/HandleCollapse/IsCollapsed) — "네 귀퉁이 전부 흑 = 즉사"는 부적 타이머와
    ///    <b>중복이 아니라 상충</b>이었다. 4곳 동시 오염 상황에서 붕괴가 판정 프레임에 즉사시키면
    ///    부적이 1초도 못 돌고, 부적 메커니즘이 그것을 위해 설계된 바로 그 상황에서 발동하지 못한다.
    /// ③ <b>RestoreAll</b> — 유일한 호출자가 부적의 1회 방어였다.
    /// </para>
    ///
    /// <para>
    /// <b>정화 진행도를 여기가 소유하는 이유.</b> 진행도를 PlayerInteraction이 들면 손을 떼는 순간 사라진다.
    /// 그런데 부적 예산이 빠듯해서(최악 이동 3.04초 + 홀드 1.5초) <b>오조작 1회 = 확정 사망</b>이 되면
    /// 어려운 게 아니라 부당한 게임이 된다. 진행도가 귀퉁이에 남아 있어야 실수를 만회할 수 있다.
    /// 대신 무기한 저축은 "조금씩 발라두고 도망"을 최적해로 만들므로 감쇠를 둔다.
    /// 이 때문에 이 클래스는 v0.6까지의 순수 이산 클래스에서 시간 축이 있는 클래스가 됐다 — 의도된 변경이다.
    /// </para>
    /// </summary>
    public sealed class SaltCorners : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private Transform[] cornerTransforms = new Transform[CornerIndex.Count]; // FarthestFromPlayer 해석용

        private readonly int[] _stages = new int[CornerIndex.Count];
        // 귀퉁이별 정화 진행도 0~1 (1 = 이번 프레임에 한 단계 정화). 손을 떼도 남되 감쇠한다.
        private readonly float[] _purify01 = new float[CornerIndex.Count];
        // 이번 프레임에 실제로 뿌리는 중인 귀퉁이 — 감쇠에서 제외한다. 매 프레임 SaltInteractable이 갱신.
        private int _activeCorner = CornerIndex.None;

        /// <summary>
        /// 흑(2단계) 귀퉁이 수 0~4 — 감광 스케일 n. 같은 계층이 직접 읽는다 (표현 계층은 CornerStageChanged로 셀 것, §1.2).
        /// </summary>
        public int BlackCornerCount { get; private set; }

        /// <summary>
        /// 더러운(1단계 이상) 귀퉁이 수 0~4. <b>부적이 타는 조건</b>이라 Talisman이 매 프레임 폴링한다.
        /// BlackCornerCount와 다르다 — 회(1)만 돼도 부적은 탄다.
        /// </summary>
        public int ContaminatedCornerCount { get; private set; }

        public int GetStage(int corner) => _stages[corner];
        public bool IsContaminated(int corner) => _stages[corner] > 0;
        public float GetPurifyProgress01(int corner) => _purify01[corner];

        public Vector2 GetCornerPosition(int corner)
        {
            Transform t = cornerTransforms != null && corner < cornerTransforms.Length ? cornerTransforms[corner] : null;
            return t != null ? (Vector2)t.position : Vector2.zero;
        }

        /// <summary>
        /// 기준 위치에서 가장 먼 귀퉁이 (AttackTargetRule.FarthestFromPlayer 해석).
        /// v0.7: 공격이 항상 한 방향이라 다중 선택판(buffer/count)이 필요 없어졌다.
        /// 확인하러 갈 수 없는 곳이 가장 위협적이라는 규칙은 그대로다 — 한 곳씩만 올 때 오히려 더 중요해진다
        /// (매번 발밑이면 공짜, 매번 반대편이면 방을 가로지르는 압박이 산다).
        /// </summary>
        public int SelectFarthestCorner(Vector2 from)
        {
            int best = CornerIndex.None;
            float bestSqr = -1f;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                float sqr = (GetCornerPosition(i) - from).sqrMagnitude;
                if (sqr <= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        /// <summary>공격 판정 미대응 오염 +1 (백→회→흑). 흑에서 더 맞으면 변화 없음.</summary>
        public void Contaminate(int corner)
        {
            if (_stages[corner] >= (int)CornerStage.Black)
            {
                Debug.Log($"[SALT] 귀퉁이 {corner} 이미 흑 — 변화 없음");
                return;
            }
            _stages[corner]++;
            Recount(); // 이벤트보다 먼저 — 구독자가 콜백 안에서 카운트를 읽어도 일관되게
            GameEvents.RaiseCornerStageChanged(corner, _stages[corner]);
            Debug.Log($"[SALT] 귀퉁이 {corner} 오염 → {(CornerStage)_stages[corner]}");
        }

        /// <summary>소금 뿌리기 완료 −1 (SaltInteractable이 호출). 진행도도 함께 리셋.</summary>
        public void Purify(int corner)
        {
            _purify01[corner] = 0f;
            if (_stages[corner] <= 0)
            {
                GameEvents.RaiseSaltPurifyNoop(corner); // 속마음 "여긴 이미 깨끗해" (RecoveryHintDirector)
                Debug.Log($"[SALT] 귀퉁이 {corner} 이미 백 — 정화 효과 없음");
                return;
            }
            _stages[corner]--;
            Recount();
            GameEvents.RaiseCornerStageChanged(corner, _stages[corner]);
            Debug.Log($"[SALT] 귀퉁이 {corner} 정화 → {(CornerStage)_stages[corner]}");
        }

        /// <summary>
        /// 뿌리는 중 진행도 갱신 (SaltInteractable이 매 틱 호출). 1에 도달하면 호출자가 Purify를 부른다.
        /// 이 프레임에 갱신된 귀퉁이는 감쇠 대상에서 빠진다.
        /// </summary>
        public void SetPurifyProgress(int corner, float progress01)
        {
            _purify01[corner] = Mathf.Clamp01(progress01);
            _activeCorner = corner;
        }

        private void Update()
        {
            if (config == null) return;
            float decay = config.SaltProgressDecayPerSec * Time.deltaTime;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (i == _activeCorner || _purify01[i] <= 0f) continue;
                _purify01[i] = Mathf.Max(0f, _purify01[i] - decay);
            }
            _activeCorner = CornerIndex.None; // 다음 프레임에 다시 갱신되지 않으면 감쇠 대상으로 돌아간다
        }

        private void Recount()
        {
            int black = 0;
            int dirty = 0;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (_stages[i] >= (int)CornerStage.Black) black++;
                if (_stages[i] > 0) dirty++;
            }
            BlackCornerCount = black;
            ContaminatedCornerCount = dirty;
        }
    }
}
