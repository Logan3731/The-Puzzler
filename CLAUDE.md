# The Puzzler — 작업 인수인계

원소 조각을 맞춰 적을 공격하는 3매치 퍼즐 RPG(로그라이크). Unity 6.3 LTS (6000.3.26f1), URP, 새 Input System.
**스팀(PC) 출시가 목표**이고 모바일은 정식판 이후에 이식한다. 화면은 가로 16:9(왼쪽 보드, 오른쪽 길과 몬스터)다.
- 스팀 데모: 챕터 1(10스테이지, 몬스터 4종) + 레벨업 능력 + 설정 화면
- 정식판: 챕터 3~4, 능력 30~50종, 동료 4~5명, 해금 요소

## 기준 문서
- **기획서 (모든 설계의 기준):** https://claude.ai/artifact/WN45S4LX9Zy45eNH9RchhT
  - Claude Docs 문서다. 웹 fetch 말고 docs 도구로 읽는다. 사용자가 직접 고치고 댓글도 단다.
  - 기획서와 이 파일이 다르면 기획서가 맞다.

## 저장소와 두 컴퓨터 작업
- GitHub: https://github.com/Logan3731/The-Puzzler (브랜치 `main`)
- Windows와 맥북을 오가며 작업한다. **작업을 시작할 때 `git pull`, 끝낼 때 커밋하고 `git push`** 한다. 커밋과 푸시는 사용자가 요청할 때 한다.
- 작업을 마칠 때 이 파일의 "현재 구현 상태"와 "다음 할 일"을 갱신해서, 다른 컴퓨터의 Claude가 이어받을 수 있게 한다.
- 에셋이 많아져 저장소가 무거워지면 Git LFS 도입을 검토한다.

## 사용자와 일하는 방식
- 한국어로 대화한다. 사용자는 기획자 겸 개발자이고, 한 단계씩 진행하는 걸 선호한다.
- 설계를 크게 바꿀 땐 **먼저 검토안을 보여주고** 확인받은 뒤 구현한다. 사용자가 "임시로 만들어서 확인"을 원하면 바로 구현한다.
- 숫자 밸런스는 근거(시뮬레이션 등)와 함께 제안한다.
- 에셋은 사용자가 VARCO(이미지·3D·Sounds)로 직접 만든다. Claude는 프롬프트를 짜고(기획서 8·9장), 받은 파일을 Unity에 연결한다.

## Unity MCP (MCP for Unity, CoplayDev v10.3.0)
- `Packages/manifest.json`에 git URL로 들어 있다. Unity에서 Window > MCP for Unity로 세션을 시작하고, Claude Code 클라이언트를 설정한다.
- **Play 모드 중에 스크립트를 컴파일하지 않는다.** 실행 중인 게임에 코드가 다시 로드되면서 보드 데이터(`grid`)가 날아가 게임이 멈춘다. 코드 수정 전에 Play를 끄고, 끝나면 사용자에게 알린다.
- Unity 창이 뒤에 있으면 게임 시간이 멈춘다. 테스트할 땐 `execute_code`로 `Application.runInBackground = true;` 를 먼저 실행한다.
- 씬에 저장된 값이 스크립트 기본값보다 우선한다. 기본값을 바꿨으면 씬 값도 MCP `set_property`로 맞추거나, 필드 이름을 새로 바꾼다.
- Game 창은 1920×1080(또는 16:9 Aspect)로 둔다: `UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "The Puzzler 1920x1080")`.
- UI 버튼 클릭 테스트는 `EventSystem.RaycastAll` + `ExecuteEvents.pointerClickHandler`로 한다. 비공개 메서드(`TrySwap`, `ResolveBoard`, `RewardScreen.Pick` 등)는 리플렉션으로 불러 테스트한다.

## 현재 구현 상태 (시스템 프로토타입)
씬: `Assets/Scenes/SampleScene.unity`. 오브젝트는 Board, GameSystems(콤보·동료·턴 흐름·스킬·UI 컴포넌트), Enemy(Enemy, EnemyIntent), ComboText, Main Camera(CameraShake, PixelRender).

| 영역 | 스크립트 (Assets/Scripts) | 내용 |
| --- | --- | --- |
| 보드 | Board/BoardManager, Block, MatchFinder | 8x8, 5원소, 스왑, 매치, 가속 낙하, 연쇄. 특수 블록(줄 파괴·원소 폭발·폭탄 3×3)과 특수끼리 조합(십자·3줄·5×5). 점액·불씨 상태. 액티브 스킬 실행. 블록 1개 = EXP 1·마나 1 |
| 콤보·동료 | Combo/ComboSystem, MagicMissile, WeaponStrike, ArcherVolley | 콤보 연출, 빛구슬(원소별 데미지), 동료(마법사 3콤보 100·전사 5콤보 250·궁수 7콤보 400, 보상으로 합류, 턴당 1회). 동료가 부순 블록도 데미지, 특수 블록이면 발동 |
| 전투 | Battle/Enemy, EnemyIntent, Player, RunManager, DamageCalculator, RoadScene, ManaCrystal | 플레이어 HP 100(턴 제한 없음), 적 행동 예고(공격·점액·불씨·회복), 원소 상성, 적 HP 600×1.15^(s−1), 4·9 엘리트·10 보스, 원 턴 킬 보너스, 게임 오버 → 처음부터, 보스 처치 → 챕터 클리어, 초과 데미지 → 마력 포인트(게임 오버 후에도 유지, 저장 안 함) |
| 성장 | Skills/SkillCatalog, PlayerSkills, PlayerLevel, RewardScreen, SkillBar | 경험치 12+4×(L−1), 레벨업마다 능력 3중 택1, 4등급(일반 55·희귀 30·영웅 12·전설 3), 능력 24종, 액티브 4종(마나+쿨타임) |
| 연출 | Juice/* | 흔들림, 줌, 히트스톱, 콤보 글씨, 빛구슬, 파편, 충격파, 빛줄기, 화면 플래시, 효과음, PixelRender(640×360) |
| 공용 | Common/MaterialUtil, UiUtil, ArtLibrary | 임시 머티리얼, 코드 UI(1920×1080 기준), VARCO 그림 불러오기 |
| 에디터 | Editor/ArtImport | 메뉴 Puzzler > Import VARCO Art and Sound |

화면: 왼쪽 보드, 오른쪽 길 풍경(임시 도형, 원근 흉내) + 몬스터. 오른쪽 위 STAGE·HP·EXP·수정구슬, 오른쪽 아래 마나·액티브 버튼·얻은 능력 목록. 클리어하면 레벨업 선택 → 1.5초 달리기 → 다음 몬스터가 멀리서 다가온다.

## 에셋 (VARCO)
- 원본은 저장소 밖 `puzzle/picture`, `puzzle/sound`에 사용자가 넣는다 (저장소에는 가공본만 있다). 맥북에서 새로 뽑으면 `The-Puzzler` 폴더와 같은 위치에 `picture`, `sound` 폴더를 만들어 넣는다.
- **Puzzler > Import VARCO Art and Sound**: 이미지는 테두리에 이어진 흰 배경을 지워 투명하게 → 잘라내고 512px 이하로 → `Assets/Resources/Art/{영문 이름}.png`. 소리는 앞 무음 자르고 첫 소리만 남김 → `Assets/Resources/Audio/sfx_{이름}.wav`. 이름 대응은 `ArtImport.cs`의 표에 있다 (새 이름이 생기면 표에 추가).
- 게임은 이름으로 찾아 쓰고, 없으면 임시 도형·합성음을 쓴다. 그림은 판(쿼드)에 붙여 세운다 (URP Unlit + 알파 컷).
- 들어간 것: 원소 블록 5종, 슬라임(바람)·고블린(불)·두꺼비(물)·그림자 늑대(보스, 어둠), 마법미사일, 전사의 검, 소리 Pop·Hit.
- 아직 합성음: Swap, Whoosh, Impact, Chime. 기획서 9장의 나머지 소리(레벨업, 피격 등)는 재생 위치 코드가 아직 없다.
- **빌드 주의:** 그림용 머티리얼을 코드에서 만든다. PC 빌드 전에 머티리얼 에셋으로 바꿔 셰이더 변형이 빠지지 않게 해야 한다.

## 다음 할 일 (기획서 10장 순서)
1. 남은 VARCO 에셋 연결: 길·소품·배경, 궁수 화살, 남은 효과음, 엘리트 표현
2. 픽셀 해상도·글씨 통일 방식 확정 → 기획서 8장
3. 몬스터별 행동 목록, 같은 몬스터 3연속 금지
4. 게임 흐름: 타이틀, 설정 화면, 결과 화면, 여신 오프닝
5. 효과음 교체·추가 (레벨업, 피격, 카드 선택 등)
6. 밸런스 플레이 테스트 (적 HP, 경험치 곡선, 능력 수치, 쿨타임)
7. PC 빌드(머티리얼 에셋화 포함), 스팀 데모 준비
8. 모바일 이식 (정식판 이후)

## 미결정 (기획서 10장)
- 마력 포인트의 쓰임새, 게임 오버 후 유지 여부 (지금은 유지, 파일 저장 안 함)
- 밸런스 전반 (플레이 테스트로)
- 글씨 통일 방식: 전부 픽셀 폰트 vs 전부 매끈한 글씨
- 여신이 원소 조각을 흩뿌린 이유 (메인 스토리 동기)
- 10콤보 동료(여신 강림), 챕터 2 이후
