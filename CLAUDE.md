# The Puzzler — 작업 인수인계

원소 조각을 맞춰 적을 공격하는 3매치 퍼즐 RPG(로그라이크). Unity 6.3 LTS (6000.3.26f1), URP, 새 Input System.
모바일(세로 1080x1920, 터치)과 PC(마우스)를 모두 지원한다.

## 기준 문서
- **기획서 (모든 설계의 기준):** https://claude.ai/artifact/WN45S4LX9Zy45eNH9RchhT
  - Claude Docs 문서다. 웹 fetch 말고 docs 도구로 읽는다.
  - 기획서와 이 파일이 다르면 기획서가 맞다.

## 저장소와 두 컴퓨터 작업
- GitHub: https://github.com/Logan3731/The-Puzzler (브랜치 `main`)
- Windows와 맥북을 오가며 작업한다. **작업을 시작할 때 `git pull`, 끝낼 때 커밋하고 `git push`** 한다. 커밋과 푸시는 사용자가 요청할 때 한다.
- 작업을 마칠 때 이 파일의 "현재 구현 상태"와 "다음 할 일"을 갱신해서, 다른 컴퓨터의 Claude가 이어받을 수 있게 한다.
- 모델·텍스처가 많아져 저장소가 무거워지면 Git LFS 도입을 검토한다.

## 사용자와 일하는 방식
- 한국어로 대화한다. 사용자는 기획자 겸 개발자이고, 한 단계씩 진행하는 걸 선호한다.
- 설계를 크게 바꿀 땐 **먼저 검토안을 보여주고** 확인받은 뒤 구현한다.
- 숫자 밸런스는 근거(시뮬레이션 등)와 함께 제안한다.
- 에셋은 사용자가 VARCO 3D(모델)와 VARCO Sounds(효과음)로 직접 만든다. Claude는 프롬프트를 짜고, 받은 파일을 Unity에 연결한다.

## Unity MCP (MCP for Unity, CoplayDev v10.3.0)
- `Packages/manifest.json`에 git URL로 들어 있다. Unity에서 Window > MCP for Unity로 세션을 시작하고, Claude Code 클라이언트를 설정한다.
- **Play 모드 중에 스크립트를 컴파일하지 않는다.** 실행 중인 게임에 코드가 다시 로드되면서 보드 데이터(`grid`)가 날아가 게임이 멈춘다. 코드 수정 전에 Play를 끄고, 끝나면 사용자에게 알린다.
- Unity 창이 뒤에 있으면 게임 시간이 멈춘다. 테스트할 땐 `execute_code`로 `Application.runInBackground = true;` 를 먼저 실행한다.
- 씬에 저장된 값이 스크립트 기본값보다 우선한다. 기본값을 바꿨으면 씬 값도 MCP `set_property`로 맞추거나, 필드 이름을 새로 바꾼다.

## 현재 구현 상태 (시스템 프로토타입 완성)
씬: `Assets/Scenes/SampleScene.unity`. 오브젝트는 Board, GameSystems, Enemy, ComboText, Main Camera(CameraShake, PixelRender)다.
모든 그래픽은 아직 임시 도형이고, 효과음은 코드로 합성한 임시음이다.

| 영역 | 스크립트 (Assets/Scripts) | 내용 |
| --- | --- | --- |
| 보드 | Board/BoardManager, Block, MatchFinder | 8x8, 5원소, 스왑(드래그·스와이프·탭), 매치, 가속 낙하·리필, 연쇄 |
| 콤보·동료 | Combo/ComboSystem, MagicMissile, WeaponStrike | 3콤보 마법사(고정 100), 5콤보 전사(고정 250), 동료는 턴당 1회 |
| 전투 | Battle/Enemy, DamageCalculator, TurnManager | 블록 데미지 = N×10×(1+0.2(c−1)), 20턴 제한, 적 HP 1800×1.3^(s−1) |
| 연출 | Juice/* | 흔들림, 줌 펀치, 히트스톱(3·5·7콤보 돌파 때만), 콤보 글씨와 누적 데미지, 빛구슬, 파편, 충격파, 화면 플래시, 합성 효과음, 픽셀 렌더 |
| 공용 | Common/MaterialUtil, UiUtil | 임시 머티리얼, 코드로 만드는 UI |

검증: 일반 매치, 5콤보(구슬 + 100 + 250 정확히 반영), 턴 소진 실패, 재도전, 다음 스테이지까지 콘솔 에러 없이 확인했다.

## 기획에서는 확정됐지만 코드에는 아직 없는 것
1. **원소 상성**: 번개 > 물 > 불 > 어둠 > 바람 > 번개 순환. 약점 ×2, 저항 ×0.5. 동료 데미지는 상성과 무관. 빛구슬이 원소를 들고 가서 "WEAK!"·"RESIST"를 표시한다.
2. **챕터 1 '여행의 시작'**: 10스테이지. 1~9는 초록 슬라임(바람), 불꽃 고블린(불), 늪지 두꺼비(물) 중 무작위(같은 몬스터 3연속 금지). 10은 보스 그림자 늑대(어둠, HP ×1.5).
3. **로그라이크**: 턴 소진 = 게임 오버, 이어하기 없이 스테이지 1부터. (지금 코드는 같은 스테이지 재도전이라 바꿔야 한다.)
4. **클리어 보상 3중 택1**: 보상은 그 판 동안 유지되고 중복 시 효과가 쌓인다. 예시 8종은 기획서 6장에 있다.
5. **전투 화면 구성**: 위쪽은 원근 카메라로 본 길과 길 위의 몬스터, 아래쪽은 직교 카메라로 본 보드. 클리어하면 약 1.5초 동안 길이 다가오는 달리기 연출 후 다음 몬스터가 멀리서 다가와 착지한다. 주인공은 화면에 나오지 않는다.
6. **동료 표시**: 동료는 화면 옆에 서 있다. 게이지는 보여주지 않는다. 충전은 한 턴 안의 연쇄로만 이뤄진다.

## 아트 방향 (진행 중)
- VARCO 3D 공통 문구: `low poly 3D game asset, smooth shading, simple solid colors, minimal details, simple chunky shapes, indie game style, matte, not glossy, not realistic, single object, centered, front view, flat bottom, plain white background`
- 사용자가 원하는 최종 느낌은 **도트(픽셀) 질감의 인디 스타일**이다. 결정한 방식은 "매끈한 로우폴리 3D 모델 + Unity 픽셀 렌더"다.
  - `Juice/PixelRender.cs`: 화면을 세로 360픽셀로 그린 뒤 점 필터로 확대한다. 입력 좌표 변환도 처리한다.
  - **임시 적용 상태이고, 사용자의 최종 확인을 기다리는 중이다.** 확인되면 기획서 8장을 "3D 로우폴리 + 픽셀 렌더"로 고친다.
- 슬라임 모델은 사용자가 생성 중이다. 받은 파일(FBX 권장, GLB면 glTF 패키지 필요)을 연결할 곳은 이렇다.
  - 블록: Board → Block Prefabs (빨·파·초·노·보 순서)
  - 적: Enemy → Body
  - 투사체: GameSystems → Missile Prefab / Weapon Prefab (검은 칼날이 +Y)

## 다음 할 일 (사용자 확인 순서대로)
1. 픽셀 렌더 느낌 확정 → 기획서 8장 반영
2. VARCO 3D 에셋 받아서 연결 (원소 조각 5종, 몬스터 4종, 길·소품, 투사체·검)
3. 위 '아직 없는 것' 1~6 구현 (원소 상성 → 로그라이크/보상 → 챕터 1 → 화면 구성·달리기 연출)
4. VARCO Sounds 효과음 6종 교체 (`Juice/Sfx.cs`)
5. 모바일 실기기 빌드 테스트

## 미결정 (기획서 10장)
- 적 HP 증가율: 1.3배를 유지하면 10스테이지 보스 HP가 약 28,600이라 너무 높다. 보상, 원소 상성(평균 ×1.1)과 함께 다시 맞춘다.
- 여신이 원소 조각을 흩뿌린 이유 (메인 스토리 동기)
- 7·10콤보 동료, 4·5매치 특수 블록 도입 여부
