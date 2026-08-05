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

        private const string Room = "Assets/_Project/Art/Room/";
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

            SetupFloorAndWalls();
            SetupWindowAndDoor();
            SetupPlayer();
            SetupSaltCorners();
            SetupClock();
            SetupTvBlanketProps();
            SetupWallTalisman();
            SetupDialogueBox();
            SetupTalismanStatus();
            SetupInteractPromptKeycap();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            D4Setup.Setup(); // 타이틀 Root 재생성 — 버튼 스킨·볼륨 슬라이더 포함 (D4가 씬 저장까지 수행)
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

        // ---------- 방 ----------

        private static void SetupFloorAndWalls()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMatPath);

            // 바닥: white32 14×9 → room_floor(12.8×7.8, 벽 프레임 구멍에 맞음)
            var floor = GameObject.Find("Room/Floor");
            SwapSprite(floor, Room + "room_floor.png", 0);

            // 벽 4면: 콜라이더만 유지, 렌더러는 끔 — 시각은 WallFrame이 담당
            foreach (string name in new[] { "Wall_Top", "Wall_Bottom", "Wall_Left", "Wall_Right" })
            {
                var wall = GameObject.Find("Room/" + name);
                if (wall == null) continue;
                var sr = wall.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
            }

            // 벽 프레임 (14×9, 중앙 투명 구멍)
            var frame = FindOrCreateSpriteChild("Room", "WallFrame", mat);
            frame.transform.position = Vector3.zero;
            SwapSprite(frame, Room + "room_wall_frame.png", 3);
        }

        private static void SetupWindowAndDoor()
        {
            // 창문 — 벽 밴드(3.9~4.5) 중앙 y=4.2로 정렬, 여명 라이트도 같은 위치로 (창호지 위에 틴트)
            var window = GameObject.Find("Room/Window");
            if (window != null) window.transform.position = new Vector3(0f, 4.2f, 0f);
            SwapSprite(FindChild(window, "Visual"), Room + "room_window.png", 4);
            var dawnLight = GameObject.Find("Lighting/WindowDawnLight");
            if (dawnLight != null) dawnLight.transform.position = new Vector3(0f, 4.2f, 0f);

            // 문 — 좌측 벽: 스프라이트는 정면 제작(1.6×0.9) → Z+90 회전으로 세움 (하단 걸쇠가 실내(+x) 방향)
            var doorVisual = FindChild(GameObject.Find("Room/Door"), "Visual");
            SwapSprite(doorVisual, Room + "room_door.png", 4);
            if (doorVisual != null) doorVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
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
            SwapSprite(visual, Props + "player_boy.png", 2); // 소팅 규약: 플레이어 2

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

        private static void SetupSaltCorners()
        {
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                SwapSprite(GameObject.Find($"Room/SaltCorner_{i}/Visual"), Props + "prop_salt_white.png", 1);
            }

            var view = GameObject.Find("Room").GetComponent<SaltCornersView>();
            WireSpriteArray(view, "stageSprites", new[]
            {
                Props + "prop_salt_white.png", Props + "prop_salt_gray.png",
                Props + "prop_salt_black.png", Props + "prop_salt_black_deep.png",
            });
        }

        private static void SetupClock()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMatPath);
            var clock = GameObject.Find("Room/Clock");
            SwapSprite(FindChild(clock, "Visual"), Props + "prop_clock_face.png", 4);

            // 디지털 텍스트 제거 — 아날로그 바늘로 대체
            var text = clock.transform.Find("ClockText");
            if (text != null) Object.DestroyImmediate(text.gameObject);

            var hour = FindOrCreateSpriteChild("Room/Clock", "HandHour", mat);
            SwapSprite(hour, Props + "prop_clock_hand_hour.png", 5);
            var minute = FindOrCreateSpriteChild("Room/Clock", "HandMinute", mat);
            SwapSprite(minute, Props + "prop_clock_hand_minute.png", 6);

            var view = clock.GetComponent<ClockView>();
            Wire(view, "label", null);
            Wire(view, "hourHand", hour.transform);
            Wire(view, "minuteHand", minute.transform);
        }

        private static void SetupTvBlanketProps()
        {
            // TV — off 기본 + TvScreenView 스왑 (TVLight는 LightingController 유지)
            var tv = GameObject.Find("Room/TV");
            var tvVisual = FindChild(tv, "Visual");
            SwapSprite(tvVisual, Props + "prop_tv_off.png", 1);
            var tvView = tv.GetComponent<TvScreenView>();
            if (tvView == null) tvView = tv.AddComponent<TvScreenView>();
            Wire(tvView, "screen", tvVisual.GetComponent<SpriteRenderer>());
            Wire(tvView, "offSprite", LoadSprite(Props + "prop_tv_off.png"));
            Wire(tvView, "onSprite", LoadSprite(Props + "prop_tv_on.png"));

            // 이불 — flat 기본 + InBlanket 시 bulge
            var blanket = GameObject.Find("Room/Blanket");
            var blanketVisual = FindChild(blanket, "Visual");
            SwapSprite(blanketVisual, Props + "prop_blanket_flat.png", 1);
            var blanketView = blanket.GetComponent<BlanketView>();
            if (blanketView == null) blanketView = blanket.AddComponent<BlanketView>();
            Wire(blanketView, "blanket", blanketVisual.GetComponent<SpriteRenderer>());
            Wire(blanketView, "flatSprite", LoadSprite(Props + "prop_blanket_flat.png"));
            Wire(blanketView, "bulgeSprite", LoadSprite(Props + "prop_blanket_bulge.png"));

            SwapSprite(FindChild(GameObject.Find("Room/Buddha"), "Visual"), Props + "prop_buddha_altar.png", 1);
            SwapSprite(FindChild(GameObject.Find("Room/Jar"), "Visual"), Props + "prop_jar.png", 1);
        }

        private static void SetupWallTalisman()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMatPath);
            var talisman = FindOrCreateSpriteChild("Room", "WallTalisman", mat);
            talisman.transform.position = new Vector3(-1.2f, 4.1f, 0f); // 상단 벽, 창문 좌측 — 시각 소품
            SwapSprite(talisman, Props + "prop_talisman_wall.png", 4);
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

            root.SetActive(false); // DialogueBoxView가 켠다

            Wire(view, "root", root);
            Wire(view, "portrait", portraitImage);
            Wire(view, "namePanel", namePanel);
            Wire(view, "nameLabel", nameLabel);
            Wire(view, "bodyLabel", body);
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

        // ---------- 헬퍼 ----------

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static GameObject FindChild(GameObject parent, string name)
        {
            if (parent == null) return null;
            var child = parent.transform.Find(name);
            return child != null ? child.gameObject : null;
        }

        private static GameObject FindOrCreateSpriteChild(string parentPath, string name, Material mat)
        {
            var parent = GameObject.Find(parentPath);
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (mat != null) sr.sharedMaterial = mat;
            return go;
        }

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
