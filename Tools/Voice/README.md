# 교수님 목소리 (TTS)

`Professor.Voice`가 설정된 교수님은 대사가 나올 때 녹음된 목소리가 재생됩니다. 지금은 컴퓨터공학과 **심한뫼 교수**(`Voice = "ShimHanmoe"`)만 목소리가 있습니다.

- 음성 파일: `Assets/Resources/Voice/<Voice>/<해시>.ogg`
- 대사 목록: `Assets/Resources/Voice/<Voice>/lines.txt` (`파일<TAB>대사`). 게임은 화면에 나온 대사를 이 목록에서 찾아 재생합니다.
- 재생 코드: `Assets/Scripts/ProfessorVoice.cs`(대사 → 음성 찾기), `NovelAudio.Speak`(재생), `GameController.SpeakOnce`(대사·결과·엔딩·시선 경고에서 호출)
- 설정 화면의 **교수님 목소리** 슬라이더로 크기를 바꾸고, 0%면 재생하지 않습니다. 자동 진행은 목소리가 끝날 때까지 기다립니다.
- 목소리가 나오지 않는 대사: AI 모드에서 실시간으로 만든 대사와 힌트, 엔딩 도감 화면.

## 대사를 고친 뒤 음성 다시 만들기

목소리 복제 환경은 로컬 `voice-clone` 디렉토리에 구성합니다(CosyVoice3, 본인 녹음 참조 `voice/best.json`).

```powershell
.\Tools\Voice\export_lines.ps1 -Voice ShimHanmoe
<voice-clone-path>\cosyvoice\.venv\Scripts\python.exe Tools\Voice\generate.py Tools\Voice\ShimHanmoe.lines.txt Assets\Resources\Voice\ShimHanmoe
```

1. `export_lines.ps1`이 스토리 코드에서 그 교수님의 대사(본문·후속 대사·선택지/문제/미니게임 반응·엔딩·Fond/Wary·시선 경고)를 모두 뽑습니다.
2. `generate.py`는 새로 생기거나 바뀐 대사만 만들고, 목록에서 빠진 음성 파일은 지웁니다. 전부 다시 만들려면 `--force`를 붙입니다.
3. Unity를 열어 새 파일을 가져오게 한 뒤 `.ogg.meta`까지 함께 커밋합니다.

영어·기호 읽는 법(`i++` → "아이 플러스 플러스", `Git` → "깃" 등)은 `generate.py`의 `SAY` 표에서 고칩니다. 화면 글자는 바뀌지 않습니다.

다른 교수님에게 목소리를 주려면 그 교수님의 `Professor`에 `Voice = "폴더이름"`을 넣고 위 두 명령의 이름만 바꿔 실행합니다.
