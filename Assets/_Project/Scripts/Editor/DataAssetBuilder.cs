using Morae.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// SO 데이터 에셋 4종 생성 + 명세 수치 입력 (architecture §2, 명세 §1~§4).
    /// - Build(): 테이블 3종을 명세 초기값으로 강제 재설정 (menu Morae/Build Data Assets). 튜닝 값이 덮어써지므로 주의.
    /// - Ensure(): 없는 에셋만 생성 — MainSceneBuilder가 호출 (기존 튜닝 보존).
    /// BalanceConfig는 필드 초기값 = 명세값이라 생성만 한다 (재설정 대상 아님 — 튜닝은 에셋에서).
    /// </summary>
    public static class DataAssetBuilder
    {
        private const string DataDir = "Assets/_Project/Data";
        public const string PhaseTablePath = DataDir + "/PhaseTable.asset";
        public const string AttackTablePath = DataDir + "/AttackTable.asset";
        public const string EventTablePath = DataDir + "/EventTable.asset";
        public const string BalanceConfigPath = DataDir + "/BalanceConfig.asset";

        [MenuItem("Morae/Build Data Assets (명세값 재설정)")]
        public static void Build()
        {
            EnsureFolder();
            FillPhaseTable(GetOrCreate<PhaseTable>(PhaseTablePath));
            FillAttackTable(GetOrCreate<AttackTable>(AttackTablePath));
            FillEventTable(GetOrCreate<EventTable>(EventTablePath));
            GetOrCreate<BalanceConfig>(BalanceConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[DATA-BUILDER] SO 에셋 4종 — 명세 초기값으로 생성/재설정 완료");
        }

        /// <summary>
        /// 명세값 재설정 + 음성 클립 재배선 일괄 — Build()의 EventTable 재생성은 VoiceSetup이 배선한
        /// audioClip 참조를 지우므로, 재설정 후에는 반드시 이 경로(또는 수동 VoiceSetup.Setup)를 쓴다.
        /// CLI: -executeMethod Morae.EditorTools.DataAssetBuilder.BuildAndRewireAudio
        /// </summary>
        [MenuItem("Morae/Build Data Assets + 음성 재배선")]
        public static void BuildAndRewireAudio()
        {
            Build();
            VoiceSetup.Setup();
        }

        /// <summary>
        /// BalanceConfig에 새 직렬화 필드가 추가됐을 때 기존 에셋을 필드 초기값(명세값)으로 갱신.
        /// 기존 에셋은 이전 스키마 값만 갖고 있어 새 필드가 0으로 남는 문제 방지 — 파괴적이므로 튜닝 덮어씀 주의.
        /// </summary>
        [MenuItem("Morae/Reset Balance Config (명세값 재설정)")]
        public static void ResetBalanceConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceConfigPath);
            if (existing == null)
            {
                GetOrCreate<BalanceConfig>(BalanceConfigPath);
            }
            else
            {
                var fresh = ScriptableObject.CreateInstance<BalanceConfig>(); // 필드 초기값 = 명세값
                EditorUtility.CopySerialized(fresh, existing);
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[DATA-BUILDER] BalanceConfig — 필드 초기값(명세값)으로 재설정 완료");
        }

        /// <summary>없는 에셋만 생성 (기존 값 보존). 씬 빌더의 선행 단계.</summary>
        public static void Ensure()
        {
            EnsureFolder();
            bool created = false;

            if (AssetDatabase.LoadAssetAtPath<PhaseTable>(PhaseTablePath) == null)
            {
                FillPhaseTable(GetOrCreate<PhaseTable>(PhaseTablePath));
                created = true;
            }
            if (AssetDatabase.LoadAssetAtPath<AttackTable>(AttackTablePath) == null)
            {
                FillAttackTable(GetOrCreate<AttackTable>(AttackTablePath));
                created = true;
            }
            if (AssetDatabase.LoadAssetAtPath<EventTable>(EventTablePath) == null)
            {
                FillEventTable(GetOrCreate<EventTable>(EventTablePath));
                created = true;
            }
            if (AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceConfigPath) == null)
            {
                GetOrCreate<BalanceConfig>(BalanceConfigPath);
                created = true;
            }

            if (created) AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // ---------- 명세 v0.3 — 페이즈 배분표 (8페이즈, 합 420s. 본편 01:00~07:30 원 시간 축) ----------

        private static void FillPhaseTable(PhaseTable table)
        {
            table.EditorSetPhases(new[]
            {
                //           id         dur  시작   끝    시계 모드          param  dawnS  dawnE  drain
                new PhaseDef(PhaseId.P1, 60f,  60, 140, ClockMode.Sync,      0,    0f,    0f,    0f),   // 시동 01:00~02:20 정상
                new PhaseDef(PhaseId.P2, 60f, 140, 220, ClockMode.Frozen,   -5,    0f,    0f,    0f),   // 교란 02:20~03:40 — 03:35에서 멈춤 (5분 멈춤)
                new PhaseDef(PhaseId.P3, 75f, 220, 300, ClockMode.Offset,   40,    0f,    0f,    0f),   // 본색 03:40~05:00 — +40분 점프 (노골)
                new PhaseDef(PhaseId.P4, 40f, 300, 340, ClockMode.Offset,  -30,    0f,    0f,    0f),   // 소강 05:00~05:40 — −30분 역행
                new PhaseDef(PhaseId.P5, 85f, 340, 410, ClockMode.Offset,  -30,    0f,    0.3f,  0.5f), // 절정 05:40~06:50 — 표시 혼란 지속, 상시 −0.5/s 시작
                new PhaseDef(PhaseId.P6, 40f, 410, 420, ClockMode.Fixed,   445,    0.3f,  0.5f,  0.5f), // 최후의 함정 06:50~07:00 — 07:25 표시 (핵심 기만), 애매한 여명
                new PhaseDef(PhaseId.P7, 40f, 420, 450, ClockMode.Fixed,   445,    0.5f,  0.85f, 0.5f), // 정적 07:00~07:30 — 시계 정지(07:25 유지), 여명 진행
                new PhaseDef(PhaseId.P8, 20f, 450, 470, ClockMode.Fixed,   445,    0.85f, 1f,    0.5f), // 탈출 07:30~ — 완연한 아침 밝기
            });
            EditorUtility.SetDirty(table);
        }

        // ---------- 명세 v0.3 공격 열: 3/3/3/2/4 = 15행 + P6 함정 2웨이브(코드 시퀀스) = 총 17회 ----------
        // 배치 제약: baseOffset×(1+jitter)+telegraph ≤ duration (지터 상한에서도 전조 판정이 페이즈 안에서 끝남)

        private static void FillAttackTable(AttackTable table)
        {
            const float jitter = 0.2f;
            const float telegraph = 3f;
            table.EditorSetAttacks(new[]
            {
                //            id         phase       offset jitter  min max  targetRule                     telegraph  resolves
                new AttackDef("atk-p1-1", PhaseId.P1, 12f, jitter, 1, 1, AttackTargetRule.RandomCorner, telegraph, true), // 단일 ×3
                new AttackDef("atk-p1-2", PhaseId.P1, 28f, jitter, 1, 1, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p1-3", PhaseId.P1, 45f, jitter, 1, 1, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p2-1", PhaseId.P2, 10f, jitter, 2, 2, AttackTargetRule.RandomCorner, telegraph, true), // 2동시 ×3
                new AttackDef("atk-p2-2", PhaseId.P2, 27f, jitter, 2, 2, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p2-3", PhaseId.P2, 44f, jitter, 2, 2, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p3-1", PhaseId.P3, 12f, jitter, 2, 3, AttackTargetRule.RandomCorner, telegraph, true), // 2~3동시 랜덤 ×3
                new AttackDef("atk-p3-2", PhaseId.P3, 32f, jitter, 2, 3, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p3-3", PhaseId.P3, 55f, jitter, 2, 3, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p4-1", PhaseId.P4,  6f, jitter, 1, 2, AttackTargetRule.RandomCorner, telegraph, true), // 1~2동시 ×2, 간격 넓게 (소강 착각)
                new AttackDef("atk-p4-2", PhaseId.P4, 30f, jitter, 1, 2, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p5-1", PhaseId.P5, 10f, jitter, 1, 4, AttackTargetRule.RandomCorner, telegraph, true), // 1~4동시 랜덤 ×4 (절정)
                new AttackDef("atk-p5-2", PhaseId.P5, 30f, jitter, 1, 4, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p5-3", PhaseId.P5, 50f, jitter, 1, 4, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p5-4", PhaseId.P5, 67f, jitter, 1, 4, AttackTargetRule.RandomCorner, telegraph, true),
                // P6 최후의 함정은 스케줄 행 없음 — AttackScheduler 전용 시퀀스(TrapTimeline + BalanceConfig trap*)
            });
            EditorUtility.SetDirty(table);
        }

        // ---------- 명세 §1 주요 이벤트 + §4 진위표 (자막 문구는 D4 튜닝 대상, 오디오 클립은 수급 후 배선) ----------

        private static void FillEventTable(EventTable table)
        {
            table.EditorSetEvents(new[]
            {
                new EventDef("tv-hint", PhaseId.P1, 15f, GameEventKind.Hint, AudioChannel.Room, 0f, false,
                    Lines(Line("나", "…TV라도 켜 두면 좀 나을지 몰라.", 3f))),
                new EventDef("window-knock", PhaseId.P1, 40f, GameEventKind.Scare, AudioChannel.Window, 0f, false,
                    Lines(Line("", "…통, 통.", 2.5f))),

                new EventDef("fake-voice-1", PhaseId.P2, 35f, GameEventKind.FakeVoice, AudioChannel.Door, 0f, false,
                    Lines(Line("???", "얘야… 할애비다. 문 좀 열어보거라.", 3.5f)),
                    Lines(Line("???", "…할애비다. 이 문 좀… 열어보거라. 응…?", 4f))),

                new EventDef("popopo", PhaseId.P3, 15f, GameEventKind.Scare, AudioChannel.Door, -10f, false,
                    Lines(Line("", "…포, 포, 포…", 3f))),
                new EventDef("silhouette", PhaseId.P3, 40f, GameEventKind.Scare, AudioChannel.Window, -10f, false,
                    Lines(Line("", "(창밖으로 옷자락 같은 것이 스쳐 지나간다)", 3f))),
                new EventDef("window-rattle", PhaseId.P3, 65f, GameEventKind.Scare, AudioChannel.Window, -10f, false,
                    Lines(Line("", "(유리창이 드르륵 떨린다)", 2.5f))),

                // P4 소강(40s)은 연출 이벤트 없음 — "힘이 빠졌나" 착각 유도 (v0.3)

                new EventDef("urge", PhaseId.P5, 15f, GameEventKind.Scripted, AudioChannel.Room, 0f, false,
                    Lines(Line("나", "…아랫배가 무겁다. 언제까지 참을 수 있을까.", 3f))), // FR-15, 컷 1순위 — 이 행 삭제만으로 제거
                new EventDef("triple-assault", PhaseId.P5, 55f, GameEventKind.Scare, AudioChannel.Door, -10f, false,
                    Lines(Line("", "(손잡이가 덜컹거린다 — 전화벨 — 노크 소리가 동시에 울린다)", 4f))),

                // P6 최후의 함정 진입 즉시 — 이후 TrapVoiceLeadSec+TrapQuietSec 동안 완전 무공격 (AttackScheduler 함정 시퀀스)
                new EventDef("fake-voice-2", PhaseId.P6, 0f, GameEventKind.FakeVoice, AudioChannel.Door, 0f, false,
                    Lines(Line("???", "얘야! 할애비다! 벌써 아침이야, 어서 문 열어라! 시계를 보거라!", 4.5f)),
                    Lines(Line("???", "…아침이라니까! 시계가 안 보이느냐! 어서, 어서!", 4f))),

                // P7 정적(40s)은 완전 무자극 — 대기 시험 (명세 §4)

                new EventDef("true-signal", PhaseId.P8, 0f, GameEventKind.TrueSignal, AudioChannel.Door, 0f, true,
                    Lines(Line("", "(할머니의 울음소리와 염불 소리가 겹쳐 들린다 — 창밖이 밝다)", 4f))),
                new EventDef("rescue-open", PhaseId.P8, 60f, GameEventKind.Scripted, AudioChannel.Door, 0f, false,
                    Lines(Line("K", "문 열겠네. …밤새 잘 버텼구먼.", 3f))), // P8은 종단 페이즈 — duration(20) 초과 offset 허용 (07:40 K씨 개문)
            });
            EditorUtility.SetDirty(table);
        }

        private static SubtitleLine Line(string speaker, string text, float duration)
            => new SubtitleLine(speaker, text, duration);

        private static SubtitleLine[] Lines(params SubtitleLine[] lines) => lines;
    }
}
