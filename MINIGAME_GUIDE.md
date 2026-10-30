# AI-ON 미니게임 제작 가이드

> 이 문서를 통째로 AI에게 붙여 넣고 "아래 규칙대로 ○○ 미니게임을 만들어 줘"라고 요청하세요.
> 결과물은 **C# 파일 1개**입니다. 그 파일을 `Assets/Scripts/`의 같은 이름 파일과 바꾸면 게임에 바로 들어갑니다.

## 0. 담당과 파일

| 미니게임 | 파일 / 클래스 이름 (그대로 유지) | 생성자 |
|---|---|---|
| 카드 짝맞추기 | `CardMatch.cs` / `CardMatch` | `public CardMatch(MiniGameTheme theme) : base(theme)` |
| 타이밍 맞추기 | `LoopTiming.cs` / `LoopTiming` | `public LoopTiming(MiniGameTheme theme) : base(theme)` |
| 커피 제조 | `CoffeeOrder.cs` / `CoffeeOrder` | `public CoffeeOrder(int professor, MiniGameTheme theme) : base(theme)` |

지금 들어 있는 파일은 동작하는 임시 버전입니다. 참고용으로 같이 AI에게 보여 줘도 됩니다.

## 1. 게임 배경 (대사·문구를 여기에 맞춰 주세요)

- 장르: 대학교 1학기 연애 시뮬레이션 비주얼 노벨 (Unity 6000.3.23f1). 주인공은 **학부 4개 중 하나**의 신입생입니다.
- 네 학부 모두 같은 미니게임 3종을 쓰고, **학부마다 제목·대사·카드 내용 같은 글자만 다릅니다**. 그 글자는 `Theme`(`MiniGameTheme`)으로 들어옵니다(아래 2번 규칙 8).
- 공략 교수는 학부마다 두 명. `professor` 값이 0이면 엄격한 교수, 1이면 유쾌한 교수입니다(사진은 학부와 상관없이 같음).
  - **0 · 엄격한 원칙파**: 근거·검증·준비를 중시. 말투는 "~합니다/~하세요" (예: "입력을 바꿔 결과를 맞추면 버그를 숨기는 겁니다.")
  - **1 · 유쾌한 소통파**: 호기심·협업을 중시. 말투는 "~해요/~죠!" (예: "앗, 테스트를 버그에 맞추면 안 돼요! 같이 찾아봐요.")

| 학부 | 0번 교수 | 1번 교수 | 카드 짝맞추기 | 타이밍 맞추기 | 커피 제조 |
|---|---|---|---|---|---|
| AI-SW융합학부 | 김가람 | 이명희 | 챕터 2 · 학식, 프로그래밍 용어 | 챕터 3 · 반복문 `i`가 10일 때 멈춤 | 챕터 5 · 발표 전날 연구실 |
| 스마트시스템공학부 | 박태준 | 최유나 | 챕터 2 · 실습실, 뜯은 라디오 부품에 이름표 붙이기 | 챕터 3 · 배달 로봇 '배달이'를 10번째 펄스에서 세우기 | 챕터 6 · 운행 재개 첫 배달로 실을 커피 |
| 경영-휴먼라이프학부 | 한석진 | 윤소희 | 챕터 3 · 강의실, 영수증 뒷면 경영 용어 카드 | 챕터 5 · 신학기 꾸러미에 문구 10개 담기 | 챕터 2 · 문구점 커피 코너 일일 알바 |
| 예술-건강학부 | 서진호 | 강하늘 | 챕터 3 · 학식, 몸·표현 용어 (반찬 내기) | 챕터 1 · 새벽 운동장, 열 번째 박에 팔 내리기 | 챕터 5 · 공연 전날 밤 휴게실 |

- 커피 제조의 첫 잔은 교수님 것입니다. 0번 교수는 진한 아메리카노(샷 2·물 2), 1번 교수는 바닐라 라떼(샷 1·우유 2·시럽 1)이고, 이어서 동기 주문 두 잔을 만듭니다. 이 레시피는 학부와 상관없이 같습니다.
- 미니게임은 타이틀의 **"미니게임" 연습 메뉴**에서도 따로 실행됩니다. 학부 탭과 교수님을 고를 수 있고, 결과는 호감도에 반영되지 않습니다. 끝나면 "결과 보기" 자리에 "돌아가기" 버튼이 나옵니다. 미니게임 쪽에서 따로 할 일은 없습니다.

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
| `Replies` | `[교수][Delta]` 반응 대사 2×3개. **`Theme.Replies ?? new[] { ... }`** 형태로, 학부가 준 대사가 있으면 그것을 씁니다 |
| `CountsForStat` | 엔딩 스탯을 올릴 만큼 잘했는지. 기본은 `Delta > 0`, 커피는 "교수님 잔을 맞혔는지" |

   `Title`은 기본 클래스가 `Theme.Title`로 이미 정합니다. 만들거나 덮어쓰지 마세요.
8. **학부마다 달라지는 글자는 `Theme`에서 읽고, 비어 있으면(null) 내 기본 글자(AI-SW융합학부용)를 씁니다.** 예: `string[,] pairs = theme.Pairs ?? Pairs;`, `header = theme.Header ?? "LOOP DEBUGGER";`. 게임 안에 학부 이름이나 특정 학부 용어를 직접 박아 넣지 마세요.
   - 카드: `Pairs`(용어·뜻 6쌍, `{ 용어, 뜻 }`)
   - 타이밍: `Header`(패널 머리글), `Rule`(코드나 규칙 한 줄), `Counter`(숫자 표시 형식, 예 `"i = {0}"`), `Running`(숫자 아래 줄), `Button`(멈춤 버튼), `Instructions`(시작 전 설명), `Hit`/`Near`/`Miss`(판정 문구), `Levels`(세 단계 이름)
   - 커피: 지금은 `Title`과 `Replies`만 학부별로 다릅니다
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
    protected readonly MiniGameTheme Theme;                 // 학부별 글자
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

// 학부 하나의 미니게임 글자. null인 칸은 게임의 기본(AI-SW) 글자를 씁니다.
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
- [ ] 학부별 글자는 `Theme`에서 읽고, 게임 안에 특정 학부 용어를 박아 넣지 않았다
- [ ] 창 크기를 바꿔도 화면이 1280×900 안에서 깨지지 않는다
- [ ] `Time.unscaledTime`을 쓰지 않고 `Now`만 쓴다 (설정 창을 열었다 닫아도 남은 시간이 그대로다)

테스트 방법: 타이틀의 **"미니게임"** 버튼 → 위쪽 학부 탭 선택 → 해당 게임 선택 (아래에서 교수님도 고를 수 있음). 네 학부 탭을 모두 눌러 글자가 학부마다 바뀌는지 확인하세요. 실제 흐름에서 확인하려면 게임 시작 → 학부·수준 선택 → 교수 선택 → 위 표의 챕터 대화를 끝까지 진행 → 선택 자리에서 "도전하기".
