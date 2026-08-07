using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 소금 뿌리기 입자 (표현 계층 — 구독만: SaltChannelChanged).
    /// 홀드하는 동안 흰 알갱이가 귀퉁이 위로 흩뿌려져 <b>바닥에 쌓인다</b>.
    ///
    /// <para>
    /// <b>왜 필요한가.</b> 이 게임이 무튜토리얼로 가르쳐야 하는 인과는 "검은 소금 = 위험"이 아니라
    /// <b>"새 소금을 뿌리면 해결된다"</b>인데, 그 동사가 화면에 없었다. 진행 바가 차는 건 시스템의 언어지
    /// 행동의 언어가 아니다 — 플레이어는 자기가 <b>뿌리고 있다</b>는 걸 봐야 한다.
    /// 탑뷰에서 가장 직접적인 표현이 바닥에 실제로 쌓이는 알갱이다.
    /// </para>
    ///
    /// <para>
    /// 알갱이는 미리 만들어 재사용한다(런타임 생성·파괴 금지). 수명이 끝나면 그 자리에 눌어붙지 않고
    /// 알파로 사라진다 — 정화가 끝나면 소금 스프라이트 자체가 하얘지므로 알갱이까지 남으면 이중이다.
    /// </para>
    /// </summary>
    public sealed class SaltPourFx : MonoBehaviour
    {
        [SerializeField] private Transform[] cornerAnchors = new Transform[CornerIndex.Count];
        [SerializeField] private Sprite grainSprite;
        [SerializeField] private Material unlitMaterial;   // 감광을 받으면 어두운 방에서 소금이 안 보인다
        [SerializeField] private int poolSize = 28;
        [SerializeField] private float spawnPerSec = 34f;
        [SerializeField] private float grainScale = 0.07f;
        [SerializeField] private float spreadRadius = 0.45f;  // 귀퉁이 주변 흩뿌림 반경
        [SerializeField] private float riseHeight = 0.55f;    // 손 높이에서 떨어지는 느낌 (탑뷰라 y로 표현)
        [SerializeField] private float fallSec = 0.32f;       // 떨어지는 시간
        [SerializeField] private float restSec = 0.55f;       // 바닥에 쌓인 뒤 남아 있는 시간
        [SerializeField] private Color grainColor = new Color(1f, 0.99f, 0.94f);
        [SerializeField] private int sortingOrder = 3;        // 소금(2)보다 위, 플레이어(8)보다 아래

        private struct Grain
        {
            public SpriteRenderer Renderer;
            public Vector3 From;
            public Vector3 To;
            public float Age;
            public bool Active;
        }

        private Grain[] _pool;
        private int _corner = CornerIndex.None;
        private float _spawnAccum;
        private System.Random _rng;

        private void Awake()
        {
            _rng = new System.Random(SessionContext.Seed ^ 0x5A17);
            _pool = new Grain[Mathf.Max(1, poolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("SaltGrain_" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = grainSprite;
                if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
                sr.sortingOrder = sortingOrder;
                sr.color = new Color(grainColor.r, grainColor.g, grainColor.b, 0f);
                go.transform.localScale = Vector3.one * grainScale;
                go.SetActive(false);
                _pool[i] = new Grain { Renderer = sr };
            }
        }

        private void OnEnable() => GameEvents.SaltChannelChanged += HandleSaltChannel;
        private void OnDisable() => GameEvents.SaltChannelChanged -= HandleSaltChannel;

        private void HandleSaltChannel(int corner, float progress01)
            => _corner = progress01 > 0f ? corner : CornerIndex.None;

        private void Update()
        {
            float dt = Time.deltaTime;
            TickGrains(dt);

            if (_corner == CornerIndex.None || grainSprite == null) { _spawnAccum = 0f; return; }
            if (_corner < 0 || _corner >= cornerAnchors.Length) return;
            Transform anchor = cornerAnchors[_corner];
            if (anchor == null) return;

            _spawnAccum += spawnPerSec * dt;
            while (_spawnAccum >= 1f)
            {
                _spawnAccum -= 1f;
                Spawn(anchor.position);
            }
        }

        private void Spawn(Vector3 center)
        {
            int idx = -1;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].Active) continue;
                idx = i;
                break;
            }
            if (idx < 0) return; // 풀이 꽉 찼으면 이번 알갱이는 버린다 (할당하지 않는다)

            float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float rad = Mathf.Sqrt((float)_rng.NextDouble()) * spreadRadius; // 면적 균등
            var landing = center + new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * 0.6f, 0f);

            Grain g = _pool[idx];
            g.From = landing + new Vector3(0f, riseHeight, 0f);
            g.To = landing;
            g.Age = 0f;
            g.Active = true;
            g.Renderer.transform.position = g.From;
            g.Renderer.gameObject.SetActive(true);
            _pool[idx] = g;
        }

        private void TickGrains(float dt)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;
                Grain g = _pool[i];
                // 런타임 생성물이라 씬 정리·플레이 종료 때 먼저 파괴될 수 있다
                if (g.Renderer == null) { _pool[i].Active = false; continue; }

                g.Age += dt;
                float total = fallSec + restSec;
                if (g.Age >= total)
                {
                    g.Active = false;
                    g.Renderer.gameObject.SetActive(false);
                    _pool[i] = g;
                    continue;
                }

                if (g.Age < fallSec)
                {
                    // 떨어지는 구간 — 가속(제곱)이라 "쏟아진다"로 읽힌다
                    float t = g.Age / fallSec;
                    g.Renderer.transform.position = Vector3.Lerp(g.From, g.To, t * t);
                    g.Renderer.color = grainColor;
                }
                else
                {
                    // 쌓인 뒤 사라지는 구간 — 정화가 끝나면 소금 스프라이트가 하얘지므로 알갱이는 물러난다
                    float t = (g.Age - fallSec) / Mathf.Max(0.01f, restSec);
                    var c = grainColor;
                    c.a = 1f - t;
                    g.Renderer.color = c;
                }
                _pool[i] = g;
            }
        }
    }
}
