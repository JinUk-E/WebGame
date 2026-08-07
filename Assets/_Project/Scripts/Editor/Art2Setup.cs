using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.EditorTools
{
    /// <summary>
    /// 아트 2단계 — 절차 생성 스프라이트(34종)를 저장된 Main.unity에 배선 (씬 재생성 없음 — 수동 배선 보존, D3/D4 방식. 멱등).
    /// ⚠ MainSceneBuilder.Build(씬 재생성)는 호출하지 않는다.
    /// 내용: 임포트 설정 강제 → 방·소품 스프라이트 교체 → 플레이어(소년 탑뷰 + InBlanket 숨김) → 시계 아날로그화 → TV/이불 스왑 뷰 →
    ///       프롤로그 대화상자 → 부적 상태 UI → E 키캡 → D4 재실행(타이틀 버튼 스킨·볼륨 슬라이더).
    /// 소팅 순서 규약: 바닥0 / 실내 소품1 / 플레이어2 / 벽 프레임3 / 벽걸이(창·문·시계·부적)4 / 시곗바늘 5·6.
    /// CLI: -executeMethod Morae.EditorTools.Art2Setup.Setup
    /// </summary>
    public static class Art2Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Pretendard-Regular SDF.asset";
        private const string SpriteLitMatPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // Art/Room/ 경로 상수는 삭제됐다 — 방 아트를 씬에 꽂는 일은 Room 프리팹의 몫이다.
        private const string Props = "Assets/_Project/Art/Props/";
        private const string UI = "Assets/_Project/Art/UI/";
        private const string Portraits = "Assets/_Project/Art/Portraits/";

        [MenuItem("Morae/Setup Art2 (스프라이트 배선)")]
        public static void Setup()
        {
            EnforceImportSettings();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            VerifyRoomPrefab(); // 방은 프리팹이 원본 — 만들지 않고 확인만 한다
            SetupPlayer();
            SetupDialogueBox();
            SetupTalismanStatus();
            SetupInteractPromptKeycap();
            SetupPrayerHint();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 심장 UI 배선 + 화면 3종 프리팹 인스턴스 참조 검증 (D4가 씬 저장까지 수행).
            // 2026-08-06부터 D4는 타이틀/게임오버/엔딩을 만들지 않는다 — 화면은 프리팹이 단일 진실.
            D4Setup.Setup();
            Debug.Log("[ART2-SETUP] 스프라이트 배선·씬 저장 완료");
        }

        // ---------- 임포트 설정 강제 (2D 기본이지만 명시 고정 — meta 커밋 대상) ----------

        private static void EnforceImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                "Assets/_Project/Art/Room", "Assets/_Project/Art/Props",
                "Assets/_Project/Art/UI", "Assets/_Project/Art/Portraits",
            });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
                {
                    importer.spritePixelsPerUnit = 100f; // §3.4 전 스프라이트 PPU 100
                    dirty = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }
                if (path.EndsWith("ui_dialogue_frame.png"))
                {
                    var border = new Vector4(72f, 72f, 72f, 72f); // 9-slice (절차생성-스프라이트 권장값)
                    if (importer.spriteBorder != border)
                    {
                        importer.spriteBorder = border;
                        dirty = true;
                    }
                }
                if (dirty) importer.SaveAndReimport();
            }
            Debug.Log($"[ART2-SETUP] 임포트 설정 확인 완료 — {guids.Length}개");
        }

        // ---------- 방 (프리팹이 단일 진실 — 검증만) ----------

        /// <summary>
        /// **방은 <c>Assets/_Project/Prefab/Room.prefab</c>이 원본이다 — 여기서 만들지도 옮기지도 않는다.**
        ///
        /// <para>
        /// 원래 이 자리에는 바닥·벽·창·문·소금·시계·TV·이불·벽부적을 코드로 배치·교체하는 코드가 있었다.
        /// 그 코드는 v0.4 시절 방을 전제로 짜여 있었고, 2026-08-06 v0.6에서 아트 담당이 방을 L자 지오메트리로
        /// 갈아엎으면서 <b>조용히 옛것이 됐다</b>. 실제로 지금 그대로 실행하면 v0.6 작업물이 무너진다:
        /// 창문을 (2.5,2.13) → (0,4.2)로 옮기고, 삭제된 <c>room_wall_frame.png</c>를 쓰는 빈 WallFrame을 되살리고,
        /// 없어진 <c>Room/Door/Visual</c>·<c>room_door.png</c>를 찾다 실패하고, 소금 Visual 정렬을 2 → 1로 내린다.
        /// (2026-08-06 화면 3종에서 <c>D4Setup</c>이 아트 작업물을 단색 화면으로 덮던 것과 같은 종류의 사고다.)
        /// </para>
        ///
        /// <para>
        /// 그래서 생성·이동 코드를 <b>가드하지 않고 삭제</b>했다(git 이력이 복원 경로 — 죽은 분기를 남기면
        /// 다음 사람에게 여전히 선택지로 보인다). 남은 것은 프리팹이 제대로 배선돼 있는지 <b>확인하고 로그로 남기는</b> 일뿐이다.
        /// 방의 스프라이트·좌표·정렬순서를 고치려면 <b>에디터에서 Room 프리팹을 편집</b>할 것.
        /// </para>
        /// </summary>
        private static void VerifyRoomPrefab()
        {
            var room = GameObject.Find("Room");
            if (room == null)
            {
                Debug.LogError("[ART2-SETUP] 씬에 Room이 없다");
                return;
            }
            if (!PrefabUtility.IsPartOfPrefabInstance(room))
            {
                Debug.LogWarning("[ART2-SETUP] Room이 프리팹 인스턴스가 아니다 — " +
                                 "메뉴 'Morae/Convert Room To Prefab Instance'로 전환할 것");
            }

            // 프리팹이 담고 있어야 할 배선 — 하나라도 비면 프리팹 편집 중 끊긴 것이다
            RequireRenderer("Room/Floor");
            RequireRenderer("Room/Window/Visual");
            RequireRenderer("Room/Door/Closed");
            RequireRenderer("Room/Clock/Visual");
            for (int i = 0; i < CornerIndex.Count; i++) RequireRenderer($"Room/SaltCorner_{i}/Visual");

            RequireField(room.GetComponent<SaltCornersView>(), "stageSprites");
            RequireField(GameObject.Find("Room/Clock").GetComponent<ClockView>(), "hourHand");
            RequireField(GameObject.Find("Room/Clock").GetComponent<ClockView>(), "minuteHand");
            RequireField(GameObject.Find("Room/TV").GetComponent<TvScreenView>(), "onSprite");
            RequireField(GameObject.Find("Room/Blanket").GetComponent<BlanketView>(), "bulgeSprite");
            Debug.Log("[ART2-SETUP] 방 프리팹 배선 검증 완료 (방은 프리팹이 원본 — 이 스크립트는 방을 만들지 않는다)");
        }

        private static void RequireRenderer(string path)
        {
            var go = GameObject.Find(path);
            SpriteRenderer sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
            if (go == null || sr == null || sr.sprite == null)
                Debug.LogError($"[ART2-SETUP] 방 프리팹 결손 — {path}의 스프라이트가 비었다");
        }

        private static void RequireField(Component target, string field)
        {
            if (target == null)
            {
                Debug.LogError($"[ART2-SETUP] 방 프리팹 결손 — 컴포넌트 없음 ({field})");
                return;
            }
            var prop = new SerializedObject(target).FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[ART2-SETUP] {target.GetType().Name}.{field} 프로퍼티 없음 — 이름이 바뀌었나?");
                return;
            }
            bool empty = prop.isArray
                ? prop.arraySize == 0
                : prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue == null;
            if (empty) Debug.LogError($"[ART2-SETUP] 방 프리팹 결손 — {target.GetType().Name}.{field} 미배선");
        }

        private static void SetupPlayer()
        {
            // 소년 탑뷰 1장 (70×90px = 0.7×0.9u). 스프라이트 기본 방향은 화면 위(+Y)이며,
            // 이동 방향 회전은 PlayerSpriteView가 Visual의 로컬 회전으로 처리한다 (루트는 회전 금지 — 콜라이더 유지).
            // SwapSprite가 localScale 1 복원(white32 시대 0.7×0.9 스케일 폐기).
            var player = GameObject.Find("Player");
            var visual = FindChild(player, "Visual");
            if (player == null || visual == null)
            {
                Debug.LogError("[ART2-SETUP] Player/Visual 없음 — 플레이어 스프라이트 배선 실패");
                return;
            }
            // 소팅 규약(v0.6): 플레이어 8. 이전 값 2는 벽걸이(창·문·시계·부적 = 4)보다 낮아
            // 플레이어가 문·시계 뒤로 숨는 상태였다 — v0.6에서 캐릭터 위층을 올려 고친 것을
            // 이 스크립트만 모르고 있었다(셋업을 다시 돌리면 그 버그가 되살아났다).
            SwapSprite(visual, Props + "player_boy.png", 8);

            // 씬 빌더가 플레이어 Visual에 라이트 머티리얼을 안 깔았음 — 2D 라이트 수광 보장
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMatPath);
            var sr = visual.GetComponent<SpriteRenderer>();
            if (mat != null && sr.sharedMaterial != mat) sr.sharedMaterial = mat;

            // InBlanket 동안 플레이어 숨김 (이불 bulge가 대신 표현) — PlayerSpriteView 구독 컴포넌트
            var view = player.GetComponent<PlayerSpriteView>();
            if (view == null) view = player.AddComponent<PlayerSpriteView>();
            Wire(view, "body", sr);
            Wire(view, "player", player.GetComponent<Morae.Game.Player.PlayerController>()); // 이동 방향 회전용
        }

        // ---------- UI ----------

        private static void SetupDialogueBox()
        {
            var canvas = GameObject.Find("UI");
            var holderTr = canvas.transform.Find("DialogueBox");
            GameObject holder;
            if (holderTr == null)
            {
                holder = new GameObject("DialogueBox");
                holder.transform.SetParent(canvas.transform, false); // 마지막 자식 — 자막·하트 위에 렌더
                var holderRect = holder.AddComponent<RectTransform>();
                Stretch(holderRect);
            }
            else
            {
                holder = holderTr.gameObject;
            }

            var view = holder.GetComponent<DialogueBoxView>();
            if (view == null) view = holder.AddComponent<DialogueBoxView>();

            // 레이아웃은 항상 재생성 (개편 잦음 — D4 타이틀 전례. 수동 편집 없음 전제)
            var oldRoot = holder.transform.Find("Root");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            var root = new GameObject("Root");
            root.transform.SetParent(holder.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 24f);
            rootRect.sizeDelta = new Vector2(1600f, 360f);

            var frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);
            var frameRect = frame.AddComponent<RectTransform>();
            Stretch(frameRect);
            var frameImage = frame.AddComponent<Image>();
            frameImage.sprite = LoadSprite(UI + "ui_dialogue_frame.png");
            frameImage.type = Image.Type.Sliced; // border 72 (임포트 설정)
            frameImage.raycastTarget = false;

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(root.transform, false);
            var portraitRect = portraitGo.AddComponent<RectTransform>();
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.zero;
            portraitRect.pivot = Vector2.zero;
            portraitRect.anchoredPosition = new Vector2(90f, 30f);
            portraitRect.sizeDelta = new Vector2(256f, 384f); // 프레임 위로 솟음 — 비주얼 노벨 관례
            var portraitImage = portraitGo.AddComponent<Image>();
            portraitImage.raycastTarget = false;
            portraitImage.preserveAspect = true;

            var namePanel = new GameObject("NamePanel");
            namePanel.transform.SetParent(root.transform, false);
            var nameRect = namePanel.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0f);
            nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(-280f, 350f); // 프레임 상단 모서리에 걸침
            nameRect.sizeDelta = new Vector2(360f, 90f);
            var nameImage = namePanel.AddComponent<Image>();
            nameImage.sprite = LoadSprite(UI + "ui_name_panel.png");
            nameImage.raycastTarget = false;

            TMP_Text nameLabel = MakeText(namePanel.transform, "NameLabel", font, 32f,
                Vector2.zero, new Vector2(340f, 80f), new Color(0.93f, 0.9f, 0.85f));

            // 본문은 프레임(1600×360, 로컬 y −180~180) 안에 들어와야 한다.
            // y=175는 텍스트 박스 절반이 프레임 위로 솟고 NamePanel(y 125~215)과도 겹쳤다 — 중앙 약간 아래로 내림.
            // x는 초상(오른쪽 끝 −454) 오른편에서 시작하도록 유지.
            TMP_Text body = MakeText(root.transform, "Body", font, 36f,
                new Vector2(170f, -20f), new Vector2(1120f, 250f), new Color(0.93f, 0.9f, 0.85f));
            body.alignment = TextAlignmentOptions.MidlineLeft;

            // 진행 가능 표시 ▼ (2026-08-06 수동 진행) — 프레임 우하단 안쪽. 수동 줄에서만 켜지고 깜빡인다.
            // Pretendard에 U+25BC 글리프가 있는 것을 확인하고 문자로 그린다 (전용 스프라이트 추가 없음).
            TMP_Text indicator = MakeText(root.transform, "AdvanceIndicator", font, 34f,
                Vector2.zero, new Vector2(60f, 60f), new Color(0.93f, 0.9f, 0.85f));
            indicator.text = "▼";
            var indicatorRect = indicator.rectTransform;
            indicatorRect.anchorMin = new Vector2(1f, 0f);
            indicatorRect.anchorMax = new Vector2(1f, 0f);
            indicatorRect.pivot = new Vector2(1f, 0f);
            indicatorRect.anchoredPosition = new Vector2(-46f, 26f);
            indicator.gameObject.SetActive(false); // DialogueBoxView가 수동 줄에서 켠다

            // 스킵 안내 — 화면 우상단. ⚠ 이 자리는 PrologueDirector.skipZoneViewport(0.80~1.0 × 0.88~1.0)와
            // 짝이다. 위치를 옮기면 그쪽 사각형도 함께 옮길 것 (표시와 판정이 어긋나면 "안 눌리는 버튼"이 된다).
            var oldSkip = holder.transform.Find("SkipHint");
            if (oldSkip != null) Object.DestroyImmediate(oldSkip.gameObject);
            TMP_Text skipHint = MakeText(holder.transform, "SkipHint", font, 26f,
                new Vector2(-40f, -30f), new Vector2(300f, 60f), new Color(0.82f, 0.78f, 0.72f, 0.75f));
            skipHint.text = "건너뛰기 ▶";
            skipHint.alignment = TextAlignmentOptions.MidlineRight;
            var skipRect = skipHint.rectTransform;
            skipRect.anchorMin = Vector2.one;
            skipRect.anchorMax = Vector2.one;
            skipRect.pivot = Vector2.one;
            skipRect.anchoredPosition = new Vector2(-40f, -30f);
            skipHint.gameObject.SetActive(false); // 첫 프롤로그 대사에서 켜진다

            root.SetActive(false); // DialogueBoxView가 켠다

            Wire(view, "root", root);
            Wire(view, "portrait", portraitImage);
            Wire(view, "namePanel", namePanel);
            Wire(view, "nameLabel", nameLabel);
            Wire(view, "bodyLabel", body);
            Wire(view, "advanceIndicator", indicator);
            Wire(view, "skipHint", skipHint.gameObject);
            WirePortraits(view, "portraits", new (string, string)[]
            {
                ("할아버지", Portraits + "portrait_grandfather.png"),
                ("할머니", Portraits + "portrait_grandmother.png"),
                ("나", Portraits + "portrait_boy.png"),
                ("무당", Portraits + "portrait_shaman.png"),
            });
        }

        private static void SetupTalismanStatus()
        {
            var canvas = GameObject.Find("UI");
            var holderTr = canvas.transform.Find("TalismanStatus");
            GameObject holder;
            if (holderTr == null)
            {
                holder = new GameObject("TalismanStatus");
                holder.transform.SetParent(canvas.transform, false);
                var rect = holder.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(36f, 36f);
                rect.sizeDelta = new Vector2(100f, 300f); // 원본 160×480 비율 유지 축소
            }
            else
            {
                holder = holderTr.gameObject;
            }

            var image = holder.GetComponent<Image>();
            if (image == null) image = holder.AddComponent<Image>();
            image.sprite = LoadSprite(UI + "ui_talisman_status_0.png");
            image.raycastTarget = false;

            var view = holder.GetComponent<TalismanStatusView>();
            if (view == null) view = holder.AddComponent<TalismanStatusView>();
            Wire(view, "talisman", image);
            WireSpriteArray(view, "stageSprites", new[]
            {
                UI + "ui_talisman_status_0.png", UI + "ui_talisman_status_1.png",
                UI + "ui_talisman_status_2.png", UI + "ui_talisman_status_3.png",
                UI + "ui_talisman_status_4.png",
            });
        }

        private static void SetupInteractPromptKeycap()
        {
            var holder = GameObject.Find("UI/InteractPrompt");
            var view = holder.GetComponent<InteractPromptView>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            // 구 단일 라벨 제거 → 키캡+라벨 행으로 대체 (항상 재생성)
            var oldLabel = holder.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var oldRow = holder.transform.Find("Row");
            if (oldRow != null) Object.DestroyImmediate(oldRow.gameObject);

            var row = new GameObject("Row");
            row.transform.SetParent(holder.transform, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0f);
            rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, 240f);
            rowRect.sizeDelta = new Vector2(0f, 52f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = row.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var keycap = new GameObject("Keycap");
            keycap.transform.SetParent(row.transform, false);
            keycap.AddComponent<RectTransform>();
            var keycapImage = keycap.AddComponent<Image>();
            keycapImage.sprite = LoadSprite(UI + "ui_key_prompt.png");
            keycapImage.raycastTarget = false;
            var element = keycap.AddComponent<LayoutElement>();
            element.preferredWidth = 48f;
            element.preferredHeight = 48f;

            TMP_Text keyLabel = MakeText(keycap.transform, "KeyLabel", font, 26f,
                Vector2.zero, new Vector2(48f, 48f), new Color(0.9f, 0.87f, 0.8f));
            keyLabel.text = "E";
            var keyRect = keyLabel.rectTransform;
            Stretch(keyRect);

            TMP_Text label = MakeText(row.transform, "Label", font, 30f,
                Vector2.zero, new Vector2(0f, 52f), new Color(0.8f, 0.78f, 0.7f, 0.9f));
            label.alignment = TextAlignmentOptions.MidlineLeft;

            row.SetActive(false); // InteractPromptView가 후보 있을 때 켠다

            Wire(view, "label", label);
            Wire(view, "promptRoot", row);
        }

        /// <summary>
        /// 기도 조작 힌트 (2026-08-06) — 프롤로그 강제 학습에서만 뜨는 키캡 안내.
        /// <para><b>자리 선정</b>: 화면 중상단(위 기준 −120, 560×170 → 1920×1080에서 x 680~1240 / y 790~960).
        /// 아래 것들과 전부 어긋난다 — 대화상자(x 160~1760 / y 24~438, 초상 포함) · 부적(x 36~136 / y 36~336) ·
        /// 이동한 하트(x 36~108 / y 480~552) · 모바일 스틱 예약(x 122~538 / y 42~458) ·
        /// 상호작용 버튼(x 1490~1750 / y 120~380) · 스킵 안내(x 1580~1880 / y 990~1050).
        /// 학습 무대(불상 = 방 좌상단)와 시선 거리가 가깝고, 대사가 차지한 하단을 피할 수 있는 유일한 넓은 여백이다.</para>
        /// </summary>
        private static void SetupPrayerHint()
        {
            var canvas = GameObject.Find("UI");
            var holderTr = canvas.transform.Find("PrayerHint");
            GameObject holder;
            if (holderTr == null)
            {
                holder = new GameObject("PrayerHint");
                holder.transform.SetParent(canvas.transform, false);
                Stretch(holder.AddComponent<RectTransform>());
            }
            else
            {
                holder = holderTr.gameObject;
            }

            var view = holder.GetComponent<PrayerHintView>();
            if (view == null) view = holder.AddComponent<PrayerHintView>();

            var oldRoot = holder.transform.Find("Root"); // 레이아웃은 항상 재생성 (대화상자와 같은 방침)
            if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var capColor = new Color(0.72f, 0.7f, 0.65f, 0.55f); // PrayerHintView.idleColor와 같은 값
            var labelColor = new Color(0.8f, 0.78f, 0.72f, 0.85f);

            var root = new GameObject("Root");
            root.transform.SetParent(holder.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -120f);
            rootRect.sizeDelta = new Vector2(560f, 170f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            Stretch(panel.AddComponent<RectTransform>());
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = LoadSprite(UI + "ui_dialogue_frame.png");
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.62f); // 방을 가리지 않게 반투명
            panelImage.raycastTarget = false;

            // ---- PC: [E] 홀드 + 방향키 크로스 ----
            var keyboard = new GameObject("Keyboard");
            keyboard.transform.SetParent(root.transform, false);
            Stretch(keyboard.AddComponent<RectTransform>());

            MakeKeycap(keyboard.transform, "KeyE", font, "E", new Vector2(-176f, 12f), 78f, capColor);
            MakeHintLabel(keyboard.transform, "HoldLabel", font, "홀드", new Vector2(-176f, -50f), labelColor);
            MakeHintLabel(keyboard.transform, "Plus", font, "+", new Vector2(-104f, 12f), labelColor);

            // 방향키 배치는 실제 키보드와 같은 역T — 방향과 손가락 위치가 그림에서 바로 읽힌다
            Image up = MakeKeycap(keyboard.transform, "KeyUp", font, "↑", new Vector2(24f, 48f), 66f, capColor);
            Image left = MakeKeycap(keyboard.transform, "KeyLeft", font, "←", new Vector2(-48f, -24f), 66f, capColor);
            Image down = MakeKeycap(keyboard.transform, "KeyDown", font, "↓", new Vector2(24f, -24f), 66f, capColor);
            Image right = MakeKeycap(keyboard.transform, "KeyRight", font, "→", new Vector2(96f, -24f), 66f, capColor);
            MakeHintLabel(keyboard.transform, "AimLabel", font, "조준", new Vector2(24f, -74f), labelColor);

            // ---- 모바일: 상호작용 버튼 홀드 + 스틱 기울임 ----
            var touch = new GameObject("Touch");
            touch.transform.SetParent(root.transform, false);
            Stretch(touch.AddComponent<RectTransform>());

            MakeCircle(touch.transform, "TouchButton", UI + "ui_touch_knob.png",
                new Vector2(-120f, 12f), 96f, capColor);
            MakeHintLabel(touch.transform, "HoldLabel", font, "홀드", new Vector2(-120f, -56f), labelColor);
            MakeHintLabel(touch.transform, "Plus", font, "+", new Vector2(-30f, 12f), labelColor);
            Image ring = MakeCircle(touch.transform, "StickRing", UI + "ui_touch_ring.png",
                new Vector2(96f, 12f), 118f, capColor);
            // 노브는 링의 자식 — 기울임 오프셋 기준이 링 중심이 된다
            Image knob = MakeCircle(ring.transform, "StickKnob", UI + "ui_touch_knob.png",
                Vector2.zero, 54f, new Color(1f, 0.93f, 0.72f, 0.95f));
            MakeHintLabel(touch.transform, "AimLabel", font, "조준", new Vector2(96f, -56f), labelColor);

            root.SetActive(false); // PrayerHintView가 학습 안내 대사에 맞춰 켠다

            Wire(view, "root", root);
            Wire(view, "keyboardGroup", keyboard);
            Wire(view, "touchGroup", touch);
            Wire(view, "touchKnob", knob.rectTransform);
            WireArray(view, "arrowKeys", new Object[] { up, down, left, right }); // AimKey 순서 고정
        }

        /// <summary>키캡 1개 (배경 스프라이트 + 글자). 반환값은 강조 대상 Graphic.</summary>
        private static Image MakeKeycap(Transform parent, string name, TMP_FontAsset font, string glyph,
            Vector2 pos, float size, Color color)
        {
            Image image = MakeCircle(parent, name, UI + "ui_key_prompt.png", pos, size, color);
            TMP_Text label = MakeText(image.transform, "Glyph", font, size * 0.46f,
                Vector2.zero, new Vector2(size, size), new Color(0.96f, 0.94f, 0.88f));
            label.text = glyph;
            Stretch(label.rectTransform);
            return image;
        }

        private static Image MakeCircle(Transform parent, string name, string spritePath,
            Vector2 pos, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.color = color;
            image.raycastTarget = false;
            CenterAnchor(image.rectTransform, pos, new Vector2(size, size));
            return image;
        }

        private static void MakeHintLabel(Transform parent, string name, TMP_FontAsset font, string text,
            Vector2 pos, Color color)
        {
            TMP_Text label = MakeText(parent, name, font, 22f, pos, new Vector2(140f, 30f), color);
            label.text = text;
            CenterAnchor(label.rectTransform, pos, new Vector2(140f, 30f));
        }

        /// <summary>부모 중심 기준 배치 — 앵커 기본값에 기대지 않고 명시한다 (기본값이 바뀌면 레이아웃이 통째로 어긋난다).</summary>
        private static void CenterAnchor(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        // ---------- 헬퍼 ----------

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static GameObject FindChild(GameObject parent, string name)
        {
            if (parent == null) return null;
            var child = parent.transform.Find(name);
            return child != null ? child.gameObject : null;
        }

        // FindOrCreateSpriteChild(방 하위에 스프라이트 자식을 만들던 헬퍼)는 삭제됐다 —
        // 방은 프리팹이 원본이므로 코드가 방에 오브젝트를 더하면 프리팹을 되돌릴 때 조용히 사라진다.

        /// <summary>스프라이트 교체 공통 — 스케일 1 복원(도형 시대의 localScale 크기 지정 폐기) + 색 white + 정렬 순서.</summary>
        private static void SwapSprite(GameObject go, string spritePath, int sortingOrder)
        {
            if (go == null)
            {
                Debug.LogError($"[ART2-SETUP] 대상 없음 — {spritePath} 배선 실패");
                return;
            }
            var sr = go.GetComponent<SpriteRenderer>();
            var sprite = LoadSprite(spritePath);
            if (sr == null || sprite == null)
            {
                Debug.LogError($"[ART2-SETUP] 스프라이트/렌더러 없음 — {go.name} ← {spritePath}");
                return;
            }
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one;
            Transform parent = go.transform.parent;
            string parentName = parent != null ? parent.name : "(루트)";
            Debug.Log($"[ART2-SETUP] {parentName}/{go.name} ← {System.IO.Path.GetFileName(spritePath)}");
        }

        private static TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font, float size,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            label.text = string.Empty;
            var rect = label.rectTransform;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Wire(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[ART2-SETUP] 배선 실패 — {target.GetType().Name}.{field} 프로퍼티 없음");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Object check = new SerializedObject(target).FindProperty(field).objectReferenceValue;
            Debug.Log($"[ART2-SETUP] {target.GetType().Name}.{field} = {(check != null ? check.name : value == null ? "(의도된 null)" : "NULL!")}");
        }

        private static void WireSpriteArray(Component target, string field, string[] spritePaths)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = spritePaths.Length;
            int nulls = 0;
            for (int i = 0; i < spritePaths.Length; i++)
            {
                var sprite = LoadSprite(spritePaths[i]);
                if (sprite == null) nulls++;
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ART2-SETUP] {target.GetType().Name}.{field} = {spritePaths.Length}개 (NULL {nulls})");
        }

        /// <summary>오브젝트 배열 필드 배선 — 원소 순서가 의미를 갖는 배열(예: AimKey 순서)에 쓴다.</summary>
        private static void WireArray(Component target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[ART2-SETUP] 배선 실패 — {target.GetType().Name}.{field} 프로퍼티 없음");
                return;
            }
            prop.arraySize = values.Length;
            int nulls = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) nulls++;
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ART2-SETUP] {target.GetType().Name}.{field} = {values.Length}개 (NULL {nulls})");
        }

        private static void WirePortraits(Component target, string field, (string speaker, string path)[] entries)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("speaker").stringValue = entries[i].speaker;
                element.FindPropertyRelative("sprite").objectReferenceValue = LoadSprite(entries[i].path);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ART2-SETUP] {target.GetType().Name}.{field} = {entries.Length}명");
        }
    }
}
