# AI-ON 미니게임 제작 가이드

> 이 문서를 통째로 AI에게 붙여 넣고 "아래 규칙대로 ○○ 미니게임을 만들어 줘"라고 요청하세요.
> 결과물은 **C# 파일 1개**입니다. 그 파일을 `Assets/Scripts/`의 같은 이름 파일과 바꾸면 게임에 바로 들어갑니다.

## 0. 담당과 파일

| 미니게임 | 파일 / 클래스 이름 (그대로 유지) | 생성자 |
|---|---|---|
| 카드 짝맞추기 | `CardMatch.cs` / `CardMatch` | `public CardMatch(MiniGameTheme theme) : base(theme)` |
| 타이밍 맞추기 | `LoopTiming.cs` / `LoopTiming` | `public LoopTiming(MiniGameTheme theme) : base(theme)` |
| 커피 제조 | `CoffeeOrder.cs` / `CoffeeOrder` | `public CoffeeOrder(int professor, MiniGameTheme theme) : base(theme)` |
| 버그 사냥 (컴공 전용) | `BugHunt.cs` / `BugHunt` | `public BugHunt(MiniGameTheme theme, bool experienced = true) : base(theme)` |
| 멀티미터 고장 찾기 (스마트 전용) | `CircuitProbe.cs` / `CircuitProbe` | `public CircuitProbe(MiniGameTheme theme, bool experienced = true) : base(theme)` |
| 꾸러미 가격 정하기 (경영 전용) | `PriceTag.cs` / `PriceTag` | `public PriceTag(MiniGameTheme theme) : base(theme)` |
| 스트레칭 리듬 (체육 전용) | `StretchRhythm.cs` / `StretchRhythm` | `public StretchRhythm(MiniGameTheme theme) : base(theme)` |
| 배달이 길 안내 (스마트 전용) | `RobotRoute.cs` / `RobotRoute` | `public RobotRoute(MiniGameTheme theme) : base(theme)` |
| 점심 피크 계산대 (경영 전용) | `ChangeMaker.cs` / `ChangeMaker` | `public ChangeMaker(MiniGameTheme theme) : base(theme)` |
| 학식 식판 짜기 (체육 전용) | `PlateBalance.cs` / `PlateBalance` | `public PlateBalance(MiniGameTheme theme) : base(theme)` |

지금 들어 있는 파일은 동작하는 임시 버전입니다. 참고용으로 같이 AI에게 보여 줘도 됩니다.

버그 사냥은 스토리에서 선택한 전공 경험 여부를 `MiniGames.Create(..., experienced: experienced)`로 전달합니다. 비전공자는 문제별 사전 설명(시간 제한 없음), 코드 줄별 쉬운 설명, 풀이 확인 후 다음 문제 이동을 사용합니다. 전공자의 20초 제한·3초 자동 풀이 전환·점수는 유지합니다. 경험 선택이 없는 타이틀의 미니게임 연습은 기존 전공자 방식입니다. `Tools → Checks → Bug Hunt Guidance`에서 모드별 진행과 점수를 검사할 수 있습니다(Play 모드를 끈 상태).

멀티미터도 같은 경험 값을 전달합니다. 입문자에게만 ON/OFF의 의미, 측정과 교체의 차이, 작은 예시를 추가하며 3회 측정 성공 기준은 같습니다. 전체 미니게임은 실패 시 `ExplainFailure(원인, 해결 방법)`으로 복습을 표시합니다. 호스트는 `Draw` 대신 `Render`를 호출하고, `Reviewing` 동안 시계를 멈추고 결과 버튼을 숨깁니다. 카드·리듬은 종료 시 복습하고, 다른 게임은 실패한 문제의 피드백에서 복습합니다. 해설 확인은 점수나 성공 판정을 바꾸지 않습니다. `Tools → Checks → Minigame Failure Lessons`에서 데이터와 해결 예시를 검사할 수 있습니다.

## 1. 게임 배경 (대사·문구를 여기에 맞춰 주세요)

- 장르: 대학교 1학기 연애 시뮬레이션 비주얼 노벨 (Unity 6000.3.23f1). 주인공은 **학과 4개 중 하나**의 신입생입니다.
- 공용 미니게임 3종(카드·타이밍·커피)은 학과마다 글자만 다르고, 각 학과는 그중 두 개와 **학과 전용 미니게임 하나**를 씁니다. 전용 게임은 규칙과 화면 자체가 학과 전공을 닮게 만듭니다. 그 글자는 `Theme`(`MiniGameTheme`)으로 들어옵니다(아래 2번 규칙 8).
- 공략 교수는 학과마다 두 명. `professor` 값이 0이면 엄격한 교수, 1이면 유쾌한 교수입니다(사진은 학과마다 다름).
  - **0 · 엄격한 원칙파**: 근거·검증·준비를 중시. 말투는 "~합니다/~하세요" (예: "입력을 바꿔 결과를 맞추면 버그를 숨기는 겁니다.")
  - **1 · 유쾌한 소통파**: 호기심·협업을 중시. 말투는 "~해요/~죠!" (예: "앗, 테스트를 버그에 맞추면 안 돼요! 같이 찾아봐요.")

| 학과 | 0번 교수 | 1번 교수 | 전용 게임 | 게임 2 | 게임 3 |
|---|---|---|---|---|---|
| 컴퓨터공학과 | 김가람 | 이명희 | 챕터 3 · 버그 사냥: 코드 세 개에서 버그 줄 찾기 | 챕터 2 · 카드, 편의점에서 프로그래밍 용어 | 챕터 5 · 커피, 발표 전날 과방 |
| 스마트시스템공학과 | 박태준 | 최유나 | 챕터 2 · 멀티미터로 끊긴 라디오 부품 찾기 | 챕터 3 · 배달이 길 안내(명령 줄 세우기) | 챕터 6 · 커피, 첫 배달로 실을 커피 |
| 경영학과 | 한석진 | 윤소희 | 챕터 5 · 사흘 시범 판매로 꾸러미 가격 정하기 | 챕터 3 · 교내 카페 계산대 거스름돈 | 챕터 2 · 커피, 문구점 커피 코너 |
| 체육학과 | 서진호 | 강하늘 | 챕터 1 · 메트로놈에 맞춘 스트레칭 리듬 | 챕터 3 · 학식 식판 짜기 | 챕터 5 · 커피, 공연 전날 휴게실 |

- 커피 제조의 첫 잔은 교수님 것입니다. 0번 교수는 진한 아메리카노(샷 2·물 2), 1번 교수는 바닐라 라떼(샷 1·우유 2·시럽 1)이고, 이어서 동기 주문 두 잔을 만듭니다. 이 레시피는 학과와 상관없이 같습니다.
- 미니게임은 타이틀의 **"미니게임" 연습 메뉴**에서도 따로 실행됩니다. 학과 탭과 교수님을 고를 수 있고, 결과는 호감도에 반영되지 않습니다. 끝나면 "결과 보기" 자리에 "돌아가기" 버튼이 나옵니다. 미니게임 쪽에서 따로 할 일은 없습니다.

## 2. 꼭 지켜야 할 규칙

1. **아래 `MiniGame` 클래스를 상속한 클래스 1개**로 만듭니다. `MonoBehaviour`가 아니며, 씬·프리팹·Canvas(uGUI)·UI Toolkit을 쓰지 않습니다.
2. 화면은 **IMGUI(`GUI.Label`, `GUI.Button`, `GUI.DrawTexture` 등)만** 사용합니다. `Draw()`는 매 프레임 `OnGUI` 안에서 불립니다.
3. 좌표는 **1280×900 고정 캔버스** 기준입니다(창 크기에 맞춘 확대·축소는 게임이 알아서 합니다). `Screen.width`는 쓰지 마세요.
   - 게임이 그리는 것: 배경의 진한 자주색 둥근 패널 `(60, 40, 1160, 830)`, 상단의 분홍 "MINI GAME" 태그와 `Title` (y 58~102), 끝났을 때 **"결과 보기" 버튼 `(980, 790, 210, 50)`**.
   - 내가 쓸 수 있는 영역: **x 100~1180, y 115~770**. 이 밖, 특히 오른쪽 아래 버튼 자리는 비워 두세요.
   - 패널 바깥 오른쪽 위 `(1228, 10)`에는 게임의 설정 아이콘이, 연습 모드에서는 그 아래 `(1228, 62)`에 닫기 아이콘이 있습니다. 설정 창이 열려 있는 동안 미니게임은 입력을 받지 못하고 `Now`도 멈춥니다(아래 4번).
4. 시간은 기본 클래스의 **`Now`**(초 단위)만 사용합니다. `Time.unscaledTime`, `Time.time`, `Time.deltaTime`, `Update`, 코루틴은 쓰지 마세요. `Now`는 설정 창이 열려 있는 동안 멈추기 때문에 제한 시간이나 타이밍이 설정 창 때문에 깨지지 않습니다. 필드 초기값(`float startedAt = Now;`)에서도 쓸 수 있습니다. 입력은 `GUI.Button` 또는 `Event.current`로 받습니다.
5. 외부 에셋(이미지·사운드·폰트) 없이 만듭니다. 색 사각형은 `Box(rect, color)` 헬퍼를 쓰세요. 꼭 필요하면 `Resources` 폴더 이미지 이름을 따로 알려 주세요.
6. 게임은 **30초~1분 안에 끝나야** 하고, 실패해도 반드시 끝나야 합니다(무한 대기 금지).
7. 끝나면 아래 값을 채우고 `Done = true`로 만듭니다.

| 멤버 | 채울 값 |
|---|---|
| `Done` | 끝나면 `true` |
| `Delta` | 성적에 따른 호감도 0, 1, 2 (음수 금지) |
| `Summary` | 결과 한 줄. 예: `"6쌍 완성 · 틀린 횟수 2"` |
| `Replies` | `[교수][Delta]` 반응 대사 2×3개. **`Theme.Replies ?? new[] { ... }`** 형태로, 학과가 준 대사가 있으면 그것을 씁니다 |
| `CountsForStat` | 엔딩 스탯을 올릴 만큼 잘했는지. 기본은 `Delta > 0`, 커피는 "교수님 잔을 맞혔는지" |

   `Title`은 기본 클래스가 `Theme.Title`로 이미 정합니다. 만들거나 덮어쓰지 마세요.
8. **학과마다 달라지는 글자는 `Theme`에서 읽고, 비어 있으면(null) 내 기본 글자(컴퓨터공학과용)를 씁니다.** 예: `string[,] pairs = theme.Pairs ?? Pairs;`, `header = theme.Header ?? "LOOP DEBUGGER";`. 게임 안에 학과 이름이나 특정 학과 용어를 직접 박아 넣지 마세요.
   - 카드: `Pairs`(용어·뜻 6쌍, `{ 용어, 뜻 }`)
   - 타이밍: `Header`(패널 머리글), `Rule`(코드나 규칙 한 줄), `Counter`(숫자 표시 형식, 예 `"i = {0}"`), `Running`(숫자 아래 줄), `Button`(멈춤 버튼), `Instructions`(시작 전 설명), `Hit`/`Near`/`Miss`(판정 문구), `Levels`(세 단계 이름)
   - 커피: 지금은 `Title`과 `Replies`만 학과별로 다릅니다
   - 새 문구가 더 필요하면 쓰지 말고 필드 이름과 기본값을 따로 알려 주세요(`MiniGameTheme`에 추가합니다).
9. 끝난 뒤에도 `Draw()`는 계속 불립니다. 결과 화면(예: `Summary`를 크게)을 보여 주고, 더 이상 입력은 받지 마세요.
10. **다른 파일은 고치지 마세요.** 클래스 이름과 생성자 형태는 위 표대로 유지합니다.

## 3. 상속할 기본 클래스 (이미 게임에 있음, 수정 금지)

```csharp
using UnityEngine;

public abstract class MiniGame
{
    public bool Done;
    public int Delta;            // Affection change, 0 to 2.
    public string Summary;
    protected readonly MiniGameTheme Theme;                 // 학과별 글자
    protected MiniGame(MiniGameTheme theme) { Theme = theme; }
    public string Title => Theme.Title;
    public abstract string[][] Replies { get; }   // [professor][Delta]; use Theme.Replies when the faculty sets it.
    // Whether the result counts toward the chapter's ending stat.
    public virtual bool CountsForStat => Delta > 0;
    public abstract void Draw(GUIStyle title, GUIStyle body, GUIStyle button);

    // 미니게임 시계(초). 설정 창이 열려 있는 동안 멈춥니다. Time.unscaledTime 대신 이것을 쓰세요.
    public static float Now { get; private set; }
    public static void Tick(float seconds) => Now += seconds;   // 게임이 매 프레임 부릅니다. 미니게임에서 부르지 마세요.

    static GUIStyle face;
    protected static GUIStyle Face(GUIStyle body)   // body를 가운데 정렬·20pt로 바꾼 스타일
    {
        if (face == null) face = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        return face;
    }
    protected static void Box(Rect rect, Color color)
    {
        GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
    }
}

// 학과 하나의 미니게임 글자. null인 칸은 게임의 기본(컴퓨터공학) 글자를 씁니다.
public class MiniGameTheme
{
    public MiniGameKind Kind;
    public string Title;
    public string Place, Note;     // 배경 그림, 연습 메뉴 설명 (게임이 씀)
    public string[][] Replies;     // [professor][Delta]
    public string[,] Pairs;        // 카드: { 용어, 뜻 } 6쌍
    public string Header, Rule, Counter, Running, Button, Instructions, Hit, Near, Miss;   // 타이밍
    public string[] Levels;        // 타이밍: 세 단계 이름
}
```

`Draw`로 넘어오는 스타일 (폰트는 모두 귀여운 한글 폰트 **고운돋움**):
- `title`: 40pt 굵게, 가운데, 흰색
- `body`: 21pt, 왼쪽 위 정렬, 흰색, `<color>` 태그 사용 가능
- `button`: 모서리가 둥근 분홍 버튼, 18pt 굵게, 글자는 진한 자주색

화면 분위기: 게임 전체가 **귀엽고 청춘물 느낌**(분홍·크림·하트)입니다. 어둡거나 웅장한 연출은 피하고, 색은 분홍 `#ff8fb1`, 진분홍 `#e0457b`, 크림 `#fffaf5`, 연분홍 `#ffe3ec`, 하늘 `#8ec9ff`, 민트 `#8fdcc0`, 레몬 `#ffe08a`를 쓰면 잘 어울립니다.
더 예쁘게 하고 싶으면 게임에 이미 있는 `NovelUI` 도우미를 써도 됩니다(선택): `NovelUI.Fill(rect, color, 반지름)` 둥근 사각형, `NovelUI.Card(...)` 그림자 있는 카드, `NovelUI.Heart(rect, color)` 하트, `NovelUI.Pill(x, y, 높이, 글자, 배경색, 글자색, 크기)` 둥근 태그, `NovelUI.Button(rect, 글자)` 분홍 버튼, 색상 `NovelUI.Pink` 등.

## 4. 최소 예시 (형태 참고용)

```csharp
using UnityEngine;

public class LoopTiming : MiniGame
{
    float startedAt = Now;
    readonly string counter;
    public LoopTiming(MiniGameTheme theme) : base(theme)
    {
        counter = theme.Counter ?? "i = {0}";
    }
    public override string[][] Replies => Theme.Replies ?? new[] {
        new[] { "0번 교수 · 0점 대사", "0번 교수 · 1점 대사", "0번 교수 · 2점 대사" },
        new[] { "1번 교수 · 0점 대사", "1번 교수 · 1점 대사", "1번 교수 · 2점 대사" }
    };

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (Done) { GUI.Label(new Rect(100, 300, 1080, 120), Summary, title); return; }
        int i = (int)((Now - startedAt) / .12f) % 15;
        GUI.Label(new Rect(100, 280, 1080, 120), string.Format(counter, i), title);
        if (GUI.Button(new Rect(440, 560, 400, 90), "멈춤!", button))
        {
            Done = true;
            Delta = i == 10 ? 2 : 0;
            Summary = i == 10 ? "정확히 10!" : string.Format(counter, i) + "에서 멈춤";
        }
    }
}
```

## 5. 넘기기 전 확인

- [ ] 파일 1개, 클래스 이름·생성자가 표와 같다 (`: base(theme)` 포함)
- [ ] 이 파일을 `Assets/Scripts/`에 넣었을 때 Console에 컴파일 에러가 없다
- [ ] 끝까지 플레이하면 `Done`이 켜지고, "결과 보기" 버튼과 겹치지 않는다
- [ ] 일부러 못해도 게임이 끝나고 `Delta`가 0~2 안에 있다
- [ ] 0번·1번 교수 대사가 각각 3개씩, 말투가 맞고 `Theme.Replies ??`로 감싸져 있다
- [ ] 학과별 글자는 `Theme`에서 읽고, 게임 안에 특정 학과 용어를 박아 넣지 않았다
- [ ] 창 크기를 바꿔도 화면이 1280×900 안에서 깨지지 않는다
- [ ] `Time.unscaledTime`을 쓰지 않고 `Now`만 쓴다 (설정 창을 열었다 닫아도 남은 시간이 그대로다)

테스트 방법: 타이틀의 **"미니게임"** 버튼 → 위쪽 학과 탭 선택 → 해당 게임 선택 (아래에서 교수님도 고를 수 있음). 네 학과 탭을 모두 눌러 글자가 학과마다 바뀌는지 확인하세요. 실제 흐름에서 확인하려면 게임 시작 → 학과·수준 선택 → 교수 선택 → 위 표의 챕터 대화를 끝까지 진행 → 선택 자리에서 "도전하기".
