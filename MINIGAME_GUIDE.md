# AI-ON 미니게임 제작 가이드

> 이 문서를 통째로 AI에게 붙여 넣고 "아래 규칙대로 ○○ 미니게임을 만들어 줘"라고 요청하세요.
> 결과물은 **C# 파일 1개**입니다. 그 파일을 `Assets/Scripts/`의 같은 이름 파일과 바꾸면 게임에 바로 들어갑니다.

## 0. 담당과 파일

| 미니게임 | 파일 / 클래스 이름 (그대로 유지) | 생성자 |
|---|---|---|
| 카드 짝맞추기 | `CardMatch.cs` / `CardMatch` | `new CardMatch()` |
| 타이밍 맞추기 | `LoopTiming.cs` / `LoopTiming` | `new LoopTiming()` |
| 커피 제조 | `CoffeeOrder.cs` / `CoffeeOrder` | `new CoffeeOrder(int professor)` |

지금 들어 있는 파일은 동작하는 임시 버전입니다. 참고용으로 같이 AI에게 보여 줘도 됩니다.

## 1. 게임 배경 (대사·문구를 여기에 맞춰 주세요)

- 장르: 대학교 1학기 연애 시뮬레이션 비주얼 노벨 (Unity 6000.3.23f1, AI-SW융합학부 신입생이 주인공).
- 공략 교수 두 명. `professor` 값이 0이면 김교수, 1이면 이교수.
  - **0 · 김가람 교수**: 엄격한 원칙파. 근거·검증·준비를 중시. 말투는 "~합니다/~하세요" (예: "입력을 바꿔 결과를 맞추면 버그를 숨기는 겁니다.")
  - **1 · 이명희 교수**: 유쾌한 소통파. 호기심·협업을 중시. 말투는 "~해요/~죠!" (예: "앗, 테스트를 버그에 맞추면 안 돼요! 같이 찾아봐요.")
- 각 미니게임의 상황:
  - **카드 짝맞추기 (챕터 2, 학식)**: 첫 수업이 끝나고 학식에서 동기와 "용어 카드"로 복습. 진 사람이 디저트를 삽니다. 첫 수업 용어(변수, 반복문, 조건문, 자료구조, 알고리즘, Git 등)와 뜻을 짝짓는 게임.
  - **타이밍 맞추기 (챕터 3, 첫 실습)**: 반복문 `for (int i = 0; i < 10; i++)`를 배우는 중. 교수님이 "화면의 숫자가 10이 되는 순간 멈춤 버튼을 누르세요"라고 합니다.
  - **커피 제조 (챕터 5, 발표 전날 연구실)**: 교수님이 "연구실 커피 머신 쓸 줄 알아요? 제 것도 한 잔 부탁해요."라고 합니다. 김교수 주문은 진한 아메리카노, 이교수 주문은 바닐라 라떼입니다. 동기 것도 함께 만듭니다.

## 2. 꼭 지켜야 할 규칙

1. **아래 `MiniGame` 클래스를 상속한 클래스 1개**로 만듭니다. `MonoBehaviour`가 아니며, 씬·프리팹·Canvas(uGUI)·UI Toolkit을 쓰지 않습니다.
2. 화면은 **IMGUI(`GUI.Label`, `GUI.Button`, `GUI.DrawTexture` 등)만** 사용합니다. `Draw()`는 매 프레임 `OnGUI` 안에서 불립니다.
3. 좌표는 **1280×900 고정 캔버스** 기준입니다(창 크기에 맞춘 확대·축소는 게임이 알아서 합니다). `Screen.width`는 쓰지 마세요.
   - 게임이 그리는 것: 배경의 진한 자주색 둥근 패널 `(60, 40, 1160, 830)`, 상단의 분홍 "MINI GAME" 태그와 `Title` (y 58~102), 끝났을 때 **"결과 보기" 버튼 `(980, 790, 210, 50)`**.
   - 내가 쓸 수 있는 영역: **x 100~1180, y 115~770**. 이 밖, 특히 오른쪽 아래 버튼 자리는 비워 두세요.
   - 패널 바깥 오른쪽 위 `(1228, 10)`에는 게임의 설정 아이콘이 있습니다. 설정 창이 열려 있는 동안 미니게임은 입력을 받지 못하지만 `Time.unscaledTime`은 계속 흐릅니다(따로 처리할 필요 없음).
4. 시간은 **`Time.unscaledTime`**만 사용합니다(`Update`, 코루틴, `Time.deltaTime` 없음). 입력은 `GUI.Button` 또는 `Event.current`로 받습니다.
5. 외부 에셋(이미지·사운드·폰트) 없이 만듭니다. 색 사각형은 `Box(rect, color)` 헬퍼를 쓰세요. 꼭 필요하면 `Resources` 폴더 이미지 이름을 따로 알려 주세요.
6. 게임은 **30초~1분 안에 끝나야** 하고, 실패해도 반드시 끝나야 합니다(무한 대기 금지).
7. 끝나면 아래 값을 채우고 `Done = true`로 만듭니다.

| 멤버 | 채울 값 |
|---|---|
| `Done` | 끝나면 `true` |
| `Delta` | 성적에 따른 호감도 0, 1, 2 (음수 금지) |
| `Summary` | 결과 한 줄. 예: `"6쌍 완성 · 틀린 횟수 2"` |
| `Title` | 화면 제목. `MiniGames.Title(챕터번호)`를 그대로 반환해도 됩니다 |
| `Replies` | `[교수][Delta]` 반응 대사 2×3개. 위 교수 말투와 상황에 맞게 |
| `CountsForStat` | 엔딩 스탯을 올릴 만큼 잘했는지. 기본은 `Delta > 0`, 커피는 "교수님 잔을 맞혔는지" |

8. 끝난 뒤에도 `Draw()`는 계속 불립니다. 결과 화면(예: `Summary`를 크게)을 보여 주고, 더 이상 입력은 받지 마세요.
9. **다른 파일은 고치지 마세요.** 클래스 이름과 생성자 형태는 위 표대로 유지합니다.

## 3. 상속할 기본 클래스 (이미 게임에 있음, 수정 금지)

```csharp
using UnityEngine;

public abstract class MiniGame
{
    public bool Done;
    public int Delta;            // Affection change, 0 to 2.
    public string Summary;
    public abstract string Title { get; }
    public abstract string[][] Replies { get; }   // [professor][Delta]
    // Whether the result counts toward the chapter's ending stat.
    public virtual bool CountsForStat => Delta > 0;
    public abstract void Draw(GUIStyle title, GUIStyle body, GUIStyle button);

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
    float startedAt = Time.unscaledTime;
    public override string Title => MiniGames.Title(3);
    public override string[][] Replies => new[] {
        new[] { "김교수 · 0점 대사", "김교수 · 1점 대사", "김교수 · 2점 대사" },
        new[] { "이교수 · 0점 대사", "이교수 · 1점 대사", "이교수 · 2점 대사" }
    };

    public override void Draw(GUIStyle title, GUIStyle body, GUIStyle button)
    {
        if (Done) { GUI.Label(new Rect(100, 300, 1080, 120), Summary, title); return; }
        int i = (int)((Time.unscaledTime - startedAt) / .12f) % 15;
        GUI.Label(new Rect(100, 280, 1080, 120), "i = " + i, title);
        if (GUI.Button(new Rect(440, 560, 400, 90), "멈춤!", button))
        {
            Done = true;
            Delta = i == 10 ? 2 : 0;
            Summary = i == 10 ? "정확히 10!" : "i = " + i + "에서 멈춤";
        }
    }
}
```

## 5. 넘기기 전 확인

- [ ] 파일 1개, 클래스 이름·생성자가 표와 같다
- [ ] 이 파일을 `Assets/Scripts/`에 넣었을 때 Console에 컴파일 에러가 없다
- [ ] 끝까지 플레이하면 `Done`이 켜지고, "결과 보기" 버튼과 겹치지 않는다
- [ ] 일부러 못해도 게임이 끝나고 `Delta`가 0~2 안에 있다
- [ ] 김교수·이교수 대사가 각각 3개씩, 말투가 맞다
- [ ] 창 크기를 바꿔도 화면이 1280×900 안에서 깨지지 않는다

테스트 방법: 게임 시작 → 학부·전공 선택 → 교수 선택 → 해당 챕터 대화를 끝까지 진행 → 두 번째 선택 자리에서 "도전하기" (카드는 챕터 2, 타이밍은 챕터 3, 커피는 챕터 5).
