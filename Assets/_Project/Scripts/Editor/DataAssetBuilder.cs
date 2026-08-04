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

        // ---------- 명세 §1 — 페이즈 배분표 (합 420s) ----------

        private static void FillPhaseTable(PhaseTable table)
        {
            table.EditorSetPhases(new[]
            {
                //           id          dur   시작   끝    시계 모드          param  dawnS  dawnE  drain
                new PhaseDef(PhaseId.P1,  60f,  60, 150, ClockMode.Sync,      0,    0f,    0f,    0f),   // 01:00~02:30 정상
                new PhaseDef(PhaseId.P2,  75f, 150, 240, ClockMode.Frozen,   -5,    0f,    0f,    0f),   // 03:55에서 멈춤 (5분 멈춤 — 미세)
                new PhaseDef(PhaseId.P3, 105f, 240, 360, ClockMode.Offset,   40,    0f,    0f,    0f),   // +40분 점프 (노골)
                new PhaseDef(PhaseId.P4,  75f, 360, 410, ClockMode.Offset,  -30,    0f,    0.35f, 0.5f), // −30분 역행, P4 이후 상시 −0.5/s
                new PhaseDef(PhaseId.P5,  30f, 410, 420, ClockMode.Fixed,   445,    0.35f, 0.45f, 0.5f), // 07:25 표시 (핵심 기만), 애매한 여명
                new PhaseDef(PhaseId.P6,  45f, 420, 450, ClockMode.Fixed,   445,    0.45f, 0.85f, 0.5f), // 정지 (07:25 유지), 여명 진행
                new PhaseDef(PhaseId.P7,  30f, 450, 460, ClockMode.Fixed,   445,    0.85f, 1f,    0.5f), // 탈출 — 아침 밝기 1.0
            });
            EditorUtility.SetDirty(table);
        }

        // ---------- 명세 §1 공격 열: 1/1/3/3/1 = 9행 (baseOffset ≤ duration×0.9 — §2.2) ----------

        private static void FillAttackTable(AttackTable table)
        {
            const float jitter = 0.2f;
            const float telegraph = 3f;
            table.EditorSetAttacks(new[]
            {
                new AttackDef("atk-p1-1", PhaseId.P1, 30f, jitter, false, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p2-1", PhaseId.P2, 40f, jitter, false, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p3-1", PhaseId.P3, 20f, jitter, false, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p3-2", PhaseId.P3, 48f, jitter, false, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p3-3", PhaseId.P3, 78f, jitter, true,  AttackTargetRule.RandomCorner, telegraph, true), // 동시 2곳 ×1
                new AttackDef("atk-p4-1", PhaseId.P4, 15f, jitter, true,  AttackTargetRule.RandomCorner, telegraph, true), // 동시 2곳 ×2
                new AttackDef("atk-p4-2", PhaseId.P4, 35f, jitter, false, AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p4-3", PhaseId.P4, 55f, jitter, true,  AttackTargetRule.RandomCorner, telegraph, true),
                new AttackDef("atk-p5-1", PhaseId.P5, 10f, jitter, false, AttackTargetRule.FarthestFromPlayer, telegraph, true), // 원거리 전조
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
                new EventDef("silhouette", PhaseId.P3, 45f, GameEventKind.Scare, AudioChannel.Window, -10f, false,
                    Lines(Line("", "(창밖으로 옷자락 같은 것이 스쳐 지나간다)", 3f))),
                new EventDef("window-rattle", PhaseId.P3, 75f, GameEventKind.Scare, AudioChannel.Window, -10f, false,
                    Lines(Line("", "(유리창이 드르륵 떨린다)", 2.5f))),

                new EventDef("urge", PhaseId.P4, 20f, GameEventKind.Scripted, AudioChannel.Room, 0f, false,
                    Lines(Line("나", "…아랫배가 무겁다. 언제까지 참을 수 있을까.", 3f))), // FR-15, 컷 1순위 — 이 행 삭제만으로 제거
                new EventDef("triple-assault", PhaseId.P4, 45f, GameEventKind.Scare, AudioChannel.Door, -10f, false,
                    Lines(Line("", "(손잡이가 덜컹거린다 — 전화벨 — 노크 소리가 동시에 울린다)", 4f))),

                new EventDef("fake-voice-2", PhaseId.P5, 5f, GameEventKind.FakeVoice, AudioChannel.Door, 0f, false,
                    Lines(Line("???", "얘야! 할애비다! 벌써 아침이야, 어서 문 열어라! 시계를 보거라!", 4.5f)),
                    Lines(Line("???", "…아침이라니까! 시계가 안 보이느냐! 어서, 어서!", 4f))),

                new EventDef("true-signal", PhaseId.P7, 0f, GameEventKind.TrueSignal, AudioChannel.Door, 0f, true,
                    Lines(Line("", "(할머니의 울음소리와 염불 소리가 겹쳐 들린다 — 창밖이 밝다)", 4f))),
                new EventDef("rescue-open", PhaseId.P7, 60f, GameEventKind.Scripted, AudioChannel.Door, 0f, false,
                    Lines(Line("K", "문 열겠네. …밤새 잘 버텼구먼.", 3f))), // P7은 종단 페이즈 — duration(30) 초과 offset 허용
            });
            EditorUtility.SetDirty(table);
        }

        private static SubtitleLine Line(string speaker, string text, float duration)
            => new SubtitleLine(speaker, text, duration);

        private static SubtitleLine[] Lines(params SubtitleLine[] lines) => lines;
    }
}
