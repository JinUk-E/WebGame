using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 어둠 속 실루엣 (명세 v0.5 §2 — 표현 계층, 구독만 §1.2).
    ///
    /// **분위기 전용이다. 피해도 상호작용도 없다.** 그래서 공격 전조와 시각 문법을 엄격히 분리한다:
    ///   전조 = 붉은 점멸 + 방향음 / 실루엣 = 색 없는 명도 차 + 무음.
    ///   이 규칙을 깨는 순간 플레이어는 "대응해야 하나?"로 오인하고, 그 오인은 곧 죽음이다.
    /// 출현 빈도·동시 수는 흑화 개수에 비례하고 n=0이면 아예 나오지 않는다 — 실루엣 자체가
    /// "내가 얼마나 무너졌나"를 알려주는 다이어제틱 게이지가 된다.
    ///
    /// 가독성 보호: 플레이어·불상·전조 중인 귀퉁이 근처에는 스폰하지 않는다 (SilhouetteSpawnModel).
    /// 렌더: 조명을 받지 않는 머티리얼(Sprites-Default)로 그린다 — 감광이 심할수록 실루엣만 남기 위해서다.
    /// </summary>
    public sealed class SilhouetteDirector : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private Sprite silhouetteSprite;
        // 조명을 받지 않는 머티리얼(Sprites-Default). 실루엣이 2D 라이트를 받으면 감광이 심할수록 같이 사라져
        // "어두운 구역에 나타난다"는 규칙 자체가 성립하지 않는다.
        [SerializeField] private Material unlitMaterial;
        [SerializeField] private Transform player;
        [SerializeField] private Transform altar;                                  // 불상 — 등대 위를 가리지 않는다
        [SerializeField] private Transform[] cornerTransforms = new Transform[CornerIndex.Count];

        [SerializeField] private Color tint = new Color(0.30f, 0.30f, 0.34f, 1f);  // 색 없는 명도 차 (붉은 계열 금지)
        [SerializeField] private float maxAlpha = 0.28f;
        [SerializeField] private float crossSpeed = 2.2f;        // 유닛/s — 걷는 속도보다 조금 느리게 "스쳐 지나감"
        [SerializeField] private float lifetimeSec = 3.2f;
        [SerializeField] private float fadePortion = 0.35f;
        [SerializeField] private Vector2 scaleRange = new Vector2(1.0f, 1.35f);    // 원본 0.64×1.32u 기준 배율
        [SerializeField] private float travelYRange = 3.0f;
        [SerializeField] private float spawnMarginX = 7.4f;      // 방 밖(±6.6 벽)에서 들어와 반대편으로 빠진다
        [SerializeField] private int poolSize = 4;

        private struct Ghost
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public bool Active;
        }

        private Ghost[] _pool;
        private readonly int[] _stages = new int[CornerIndex.Count];
        // corner당 개수로 센다 — 같은 귀퉁이에 전조가 겹칠 수 있어 단일 시각으로는 조기에 회피가 풀린다
        private readonly int[] _telegraphCount = new int[CornerIndex.Count];
        private readonly Vector2[] _telegraphPositions = new Vector2[CornerIndex.Count];
        private int _blackCount;
        private float _spawnTimer;
        private System.Random _rng;
        private bool _running;

        private void Awake()
        {
            SessionContext.EnsureInitialized(); // Awake 순서에 상관없이 시드가 잡혀 있게 (GameFlow.Start보다 먼저 돌 수 있다)
            _rng = new System.Random(SessionContext.Seed ^ 0x5115);
            BuildPool();
        }

        private void OnEnable()
        {
            GameEvents.CornerStageChanged += HandleCornerStage;
            GameEvents.AttackTelegraphStarted += HandleTelegraphStarted;
            GameEvents.AttackResolved += HandleAttackResolved;
            GameEvents.PhaseChanged += HandlePhaseChanged;
            GameEvents.GameOver += HandleStop;
            GameEvents.EndingStarted += HandleEndingStop;
        }

        private void OnDisable()
        {
            GameEvents.CornerStageChanged -= HandleCornerStage;
            GameEvents.AttackTelegraphStarted -= HandleTelegraphStarted;
            GameEvents.AttackResolved -= HandleAttackResolved;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            GameEvents.GameOver -= HandleStop;
            GameEvents.EndingStarted -= HandleEndingStop;
        }

        private void BuildPool()
        {
            int size = Mathf.Max(1, poolSize);
            _pool = new Ghost[size];
            for (int i = 0; i < size; i++)
            {
                var go = new GameObject($"Silhouette_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = silhouetteSprite;
                if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
                else if (i == 0) Debug.LogError("[SILHOUETTE] unlitMaterial 미배선 — 실루엣이 2D 라이트를 받아 감광과 함께 사라진다", this);
                // 소팅 1 = 실내 소품 층. 플레이어(2)와 같은 값을 쓰면 같은 order 안의 순서가 보장되지 않아
                // 실루엣이 프레임마다 플레이어 앞뒤로 튄다 — 항상 뒤로 지나가게 한 단계 낮춘다.
                sr.sortingOrder = 1;
                sr.color = new Color(tint.r, tint.g, tint.b, 0f);
                go.SetActive(false);
                _pool[i] = new Ghost { Renderer = sr };
            }
        }

        private void HandlePhaseChanged(PhaseId phase)
        {
            _running = true; // 본편 시작 — 프롤로그·타이틀에서는 나오지 않는다
        }

        private void HandleStop(GameOverReason reason) => StopAll();
        private void HandleEndingStop(EndingKind kind) => StopAll();

        private void StopAll()
        {
            _running = false;
            for (int i = 0; i < _pool.Length; i++) Despawn(i);
        }

        private void HandleCornerStage(int corner, int stage)
        {
            if (corner < 0 || corner >= _stages.Length) return;
            _stages[corner] = stage;
            _blackCount = CornerPenaltyModel.CountBlack(_stages);
        }

        private void HandleTelegraphStarted(int corner, float duration)
        {
            if (corner < 0 || corner >= _telegraphCount.Length) return;
            _telegraphCount[corner]++;
        }

        private void HandleAttackResolved(int corner, bool countered)
        {
            if (corner < 0 || corner >= _telegraphCount.Length) return;
            _telegraphCount[corner] = Mathf.Max(0, _telegraphCount[corner] - 1);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickActive(dt);

            if (!_running || config == null || silhouetteSprite == null) return;

            float interval = SilhouetteSpawnModel.SpawnInterval(_blackCount,
                config.SilhouetteBaseIntervalSec, config.SilhouetteIntervalGain, config.SilhouetteMinIntervalSec);
            if (interval < 0f)
            {
                _spawnTimer = 0f; // 흑 0 — 미출현 (게이지가 0이면 바늘도 없다)
                return;
            }

            _spawnTimer += dt;
            if (_spawnTimer < interval) return;
            _spawnTimer = 0f;

            int max = SilhouetteSpawnModel.MaxConcurrent(_blackCount, 1, config.SilhouetteMaxConcurrent);
            if (CountActive() >= max) return;
            TrySpawn();
        }

        private void TickActive(float dt)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;
                Ghost g = _pool[i];
                g.Age += dt;
                if (g.Age >= g.Lifetime)
                {
                    Despawn(i);
                    continue;
                }
                g.Renderer.transform.position += (Vector3)(g.Velocity * dt);
                float a = SilhouetteSpawnModel.FadeAlpha01(g.Age / g.Lifetime, fadePortion) * maxAlpha;
                g.Renderer.color = new Color(tint.r, tint.g, tint.b, a);
                _pool[i] = g;
            }
        }

        private void TrySpawn()
        {
            int slot = -1;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) { slot = i; break; }
            }
            if (slot < 0) return;

            bool fromLeft = _rng.Next(2) == 0;
            float y = ((float)_rng.NextDouble() * 2f - 1f) * travelYRange;
            float dir = fromLeft ? 1f : -1f;
            var start = new Vector2(fromLeft ? -spawnMarginX : spawnMarginX, y);
            // 가독성 판정은 **실제 이동 경로의 중간·끝** 기준 — 알파가 가장 높은 구간이 여기다.
            // (화면 중앙 고정으로 검사하면 이 실루엣이 지나가지도 않는 지점을 보호하게 된다)
            float travel = crossSpeed * lifetimeSec;
            var mid = new Vector2(start.x + dir * travel * 0.5f, y);
            var end = new Vector2(start.x + dir * travel, y);
            int telegraphCount = CollectTelegraphPositions();

            if (!SilhouetteSpawnModel.IsReadablePosition(mid, PlayerPos(), AltarPos(),
                    _telegraphPositions, telegraphCount, config.SilhouetteClearance)
                || !SilhouetteSpawnModel.IsReadablePosition(end, PlayerPos(), AltarPos(),
                    _telegraphPositions, telegraphCount, config.SilhouetteClearance))
            {
                return; // 이번 기회는 버린다 — 겹쳐 보이느니 안 나오는 게 낫다
            }

            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)_rng.NextDouble());
            Ghost g = _pool[slot];
            Transform tr = g.Renderer.transform;
            tr.position = start;
            tr.localScale = new Vector3(scale * (fromLeft ? 1f : -1f), scale, 1f); // 진행 방향으로 좌우 반전
            g.Renderer.color = new Color(tint.r, tint.g, tint.b, 0f);
            g.Renderer.gameObject.SetActive(true);
            g.Velocity = new Vector2(fromLeft ? crossSpeed : -crossSpeed, 0f);
            g.Age = 0f;
            g.Lifetime = lifetimeSec;
            g.Active = true;
            _pool[slot] = g;
        }

        /// <summary>전조 중인 귀퉁이 위치를 버퍼에 모아 개수를 돌려준다 (할당 없음).</summary>
        private int CollectTelegraphPositions()
        {
            int count = 0;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (_telegraphCount[i] <= 0) continue;
                Transform t = cornerTransforms != null && i < cornerTransforms.Length ? cornerTransforms[i] : null;
                if (t == null) continue;
                _telegraphPositions[count++] = t.position;
            }
            return count;
        }

        private Vector2 PlayerPos() => player != null ? (Vector2)player.position : Vector2.zero;
        private Vector2 AltarPos() => altar != null ? (Vector2)altar.position : new Vector2(999f, 999f);

        private int CountActive()
        {
            int n = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].Active) n++;
            }
            return n;
        }

        private void Despawn(int index)
        {
            Ghost g = _pool[index];
            if (g.Renderer != null) g.Renderer.gameObject.SetActive(false);
            g.Active = false;
            g.Age = 0f;
            _pool[index] = g;
        }
    }
}
