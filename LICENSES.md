# 에셋 출처·라이선스 기록

NHN NAN 2026 사전과제 규정에 따른 AI 생성·외부 에셋 기록.

## AI 생성 오디오 (2026-08-04)

전부 로컬 도구로 생성 — 외부 유료 서비스·저작권 소스 미사용.

| 파일 | 생성 방법 |
|------|-----------|
| `Audio/Voice/fake1_*.wav` `fake2_*.wav` | Windows TTS(Microsoft Heami, ko-KR) 합성 → ffmpeg 가공 (피치 다운 ~4반음, 템포 보정, 에코). 뭉갬 벌은 로우패스 580Hz — "할아버지 목소리를 흉내 내는 존재" 연출 |
| `Audio/Voice/popopo_*.wav` | 동일 TTS → 피치 다운 6반음 + 에코 + 로우패스 (속삭임) |
| `Audio/Voice/true_signal_*.wav` | TTS 곡소리("아이고…")·염불("나무아미타불 관세음보살") 2트랙 → ffmpeg 믹스 + 3탭 에코 |
| `Audio/Voice/rescue_*.wav` | 동일 TTS → 피치 다운 (K씨 남성 톤, 에코 없음 — 진짜 사람) |
| `Audio/Voice/tv_hint.wav` `urge.wav` | 동일 TTS 근사 원음 (소년 독백) |
| `Audio/SFX_Heartbeat/heartbeat_loop.wav` | 절차 합성 (Python — 저주파 감쇠 사인 lub-dub 루프) |
| `Audio/SFX_Window/window_knock.wav` `window_rattle.wav` | 절차 합성 (Python — 감쇠 사인 + 노이즈 어택 / 진폭 변조 노이즈) |
| `Art/UI/heart128.png` | 절차 생성 (에디터 스크립트 — 임플리시트 하트 곡선) |
| `Art/Smoke/white32.png` | 절차 생성 (에디터 스크립트 — 단색) |

## 외부 에셋

| 에셋 | 라이선스 |
|------|----------|
| Pretendard 폰트 | SIL OFL 1.1 |
| `Audio/BGM_*`, `Audio/SFX_Clock`, `Audio/SFX_Door`(원본 m4a), `Audio/SFX_Fear` | 사용자 수급분 — 출처 확인 후 기재 예정 |

AI 코딩: 개발 전 과정에 Claude Code(Anthropic) 사용 — AI 활용 기술 문서에 상세 기록 예정.
