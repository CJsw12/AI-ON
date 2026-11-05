# iPhone 시선 추적 연결 장애 인수인계

## 2026년 11월 5일 후속 인수인계 상하 추적과 머리 위치 문제

이 절은 아래 과거 기록보다 최신이다. 작업 브랜치는 `feature/player-photo-avatar`이며, 플레이어 사진 기능 커밋 `8027b74` 위에서 진행했다. 현재 증상은 **같은 화면 지점을 보더라도 머리 위치를 바꾸면 포인터 위치가 달라지는 것으로 사용자가 보고한 상태**다. 해결 완료로 취급하지 말 것.

### 이번 세션 변경

- `GameController.IPhoneGaze.cs`: 연결 후 사용할 수 있는 **상하 추적 진단** 버튼 추가. 위/아래 목표를 각각 4초 표시하고 첫 1초를 제외한 새 표본을 수집한다. 얼굴/연결이 유효하며 Blink < 0.7 또는 미제공인 표본만 평균한다. 한 방향에서 표본 5개 미만이면 종료한다. 진단은 보정 계수나 드리프트를 수정하지 않는다.
- 진단 요약은 Unity Console 및 `%LOCALAPPDATA%/Unity/Editor/Editor.log`의 `[iPhone vertical diagnostic]`에서 확인한다. 순서는 `eye/look/head`의 세로 성분이며 `delta`는 아래 평균에서 위 평균을 뺀 값이다.
- `GazeMapper.Fit(..., requireVerticalEye: true)`: iPhone 세로 모델에서 고개만 사용하는 후보를 제외했다. 눈 회전 또는 눈모양을 포함한 후보는 허용하며 고개와의 조합도 여전히 가능하다. 웹캠은 기존 기본값 false를 사용한다. **눈 신호의 기여가 충분함을 보장하지 않으며, 고개 이동 보정의 해결책도 아니다. 실험적 변경으로 평가 필요.**
- `GazeMapper.PredictUnclamped`: 화면 경계 제한 전 좌표를 진단용으로 노출한다. 기존 `Predict`는 이 결과를 기존처럼 0~1로 제한한다.
- `[iPhone gaze trace]`: 보정 완료 또는 진단 시작 시 최대 40초, 진단 완료 후 30초간 0.5초 간격으로 기록한다. `phase`, `seq`, `fresh`, `face`, `blink`, `held`, `valid`, `eyeY`, `lookY`, `headY`, `rawY`, `shownY`, `driftY` 포함. rawY와 shownY는 정규화 좌표이며 아래쪽이 증가한다. shownY는 필터 후 마지막 포인터이므로 invalid일 때 유효한 위치로 해석하면 안 된다.
- `GameController.GazeTest.cs`: 진단 화면 분기와 프레임별 추적 기록 호출 연결.

### 실제 관찰한 결과

Wi-Fi UDP 실기기 진단 로그에서 확인한 수치다. 오차는 GazeMapper가 보정 데이터로 추정한 값이며 독립적인 정확도 검증 결과가 아니다. 각 실행의 자세/보정 표본이 같지 않아 전후 성능 개선율로 해석하지 말 것.

| 실행 | 선택된 세로 모델 | 추정 오차 px | 위 eye/look/head | 아래 eye/look/head | 위와 아래 표본 수 |
|---|---|---:|---|---|---|
| 초기 | 고개 | 294.2 | 4.624 / -0.119 / -0.305 | 7.293 / -0.188 / -0.704 | 86 / 91 |
| 수정 후 1 | 고개+눈모양 | 156.6 | 3.303 / -0.085 / -0.899 | 3.979 / -0.103 / -0.798 | 151 / 147 |
| 같은 보정 재진단 | 고개+눈모양 | 156.6 | 4.374 / -0.113 / 2.748 | 7.487 / -0.193 / 2.961 | 140 / 145 |
| 재보정 후 | 눈모양 | 268.7 | 1.089 / -0.028 / 1.924 | 5.903 / -0.152 / 1.830 | 152 / 148 |

눈의 세로 입력은 변한다. 따라서 세로 입력이 아예 오지 않는 현상으로 확정할 수 없다. 고개 기준과 선택 모델이 달라지며 결과가 불안정하다. 사용자는 처음에는 괜찮다가 진단/보정 후 이상하게 느껴진다고 보고했고, 이어 머리 위치에 따라 포인터가 이동한다고 밝혔다. 원인으로 고개 회귀 가중치, 머리 평행이동/거리, 클램핑, 깜빡임 정지 등을 구분해야 한다.

### 다음 담당자가 먼저 할 일

1. Unity `6000.3.23f1`에서 컴파일 후 Play 재시작. iPhone Wi-Fi 연결 → 13점 보정 → 상하 추적 진단. 종료 후 화면 안내대로 30초간 위/아래를 번갈아 보며 `[iPhone gaze trace]` 수집. 마지막 연속 좌표 기록 기능은 아직 실제 실행 결과를 확보하지 못했다.
2. 화면 중앙 같은 목표를 계속 보면서 고개 회전, 좌우/상하 평행이동, 앞뒤 거리 변화를 각각 분리해 측정할 것. 현 진단은 눈을 위아래로 움직이는 시험뿐이라 머리 영향 분리를 위해 보강 필요.
3. `rawY`가 0~1을 벗어나면 화면 경계 포화, 입력은 변하지만 `held`가 true면 깜빡임 정지, `rawY`는 변하지만 `shownY`가 늦으면 필터/갱신 경로를 확인한다. `fresh`, `face`, `valid`가 false인 샘플을 정상 추적으로 취급하지 않는다.
4. 현재 UDP 입력은 `EyeFeature`가 양쪽 눈 회전 평균, `HeadFeature`가 얼굴 Rotation의 yaw/pitch, `LookFeature`가 EyeLook blendshape 조합이다. 머리 평행이동과 얼굴-카메라 거리는 현 인터페이스에 없다. VTube Studio 실제 패킷에서 추가 위치 정보를 제공하는지 확인한 뒤 도입 여부 판단. 머리 영향이라고 해서 보정된 고개 항을 무조건 제거하지 말고 동일 목표 재현으로 비교한다.
5. requireVerticalEye 변경은 보정 후보 제한일 뿐이다. 독립 검증 점과 머리 이동 조건에서 기존 후보 선택과 비교하고, 필요하면 되돌릴 것. 현재 정확도 회복/문제 해결은 검증되지 않았다.

### 연결 환경과 검증 범위

- 학교에서 친구 핫스팟으로 접속. Windows Public 프로필에서 Unity 6000.3.23f1의 명시적 인바운드 Block 규칙이 활성화돼 있었다. 사용자 관리자 PowerShell에서 해당 Block 비활성화 및 UDP 11125 Private/Public Allow 생성 후 실제 iPhone 데이터 수신과 진단 실행 확인. 방화벽 설정은 Git 코드 변경이 아니며 다른 PC에 자동 적용되지 않는다. 과거에 쓰던 고정 IP를 복사하지 말고 현재 아이폰 Wi-Fi IPv4와 앱 포트를 사용할 것.
- 상하 진단 UI가 표시되고 실제 요약 로그가 기록됨을 확인했다. 최종 30초 연속 추적 기록 및 머리 움직임에 대한 개선 효과는 미검증이다. 문서의 과거 합성 테스트 수치나 타 세션 테스트 통과를 이번 변경의 실기기 검증으로 간주하지 말 것.
- 푸시 전 `git diff --check` 통과. `Tests/*.ps1` 5종 모두 exit 0: ImpactLabIntegration, PhysicsExperiencedDialogue, PhysicsImpactRules, PhysicsStory, ProfessorPersonality. 마지막 스크립트는 컨트롤러를 컴파일 대상에서 제외하므로 이것만으로 Unity UI 코드 전체 컴파일을 보장하지 않는다. 기존 AiProfessor/CoffeeRecipeSO 경고 출력 있음. 최종 Unity 전체 컴파일 완료 여부 및 추가 추적 로그의 실기 검증은 다음 담당자가 확인할 것.
- 이전 커밋 `00de523`에는 웹캠 아래보기 표본 예외, 별잡기 보정의 새 표본 집계/정지 목표/분산 조건, 보정량 35%→15%, 복잡 모델 패널티 4%→8%가 이미 포함돼 있다. 이번 변경과 함께 필요 시 비교한다. 포인터 확대는 현재 브랜치에 포함돼 있지 않다.

---

작성일: 2026-11-05  
프로젝트: AI-ON · Unity `6000.3.23f1`  
대상: 다음 담당자/에이전트가 Windows PC에서 iPhone VTube Studio UDP 연결을 빠르게 진단할 때

## 현재 구현과 포트

- 데이터 흐름: VTube Studio iOS → PC Unity UDP 수신기
- Unity는 아이폰의 현재 IPv4 주소와 VTube Studio UDP 포트(보통 `21412`)로 `iOSTrackingDataRequest`를 매초 보냅니다.
- Unity 수신기는 PC UDP `11125`에 바인딩하고 추적 JSON을 받아 처리합니다.
- 프로젝트의 수신 코드: `Assets/Scripts/IPhoneGazeUdpReceiver.cs`
- 설정/보정/테스트 UI: `Assets/Scripts/GameController.IPhoneGaze.cs`
- Wi-Fi/USB 일반 절차: `docs/AI/iPhone-Gaze-Tracking-Setup.md`

## 2026-11-04~05에 실제 발생한 증상

1. iPhone VTube Studio에는 3rd Party PC Clients가 켜져 있고 연결 클라이언트 `1`이 표시됐지만 Unity는 계속 “아이폰 데이터를 기다리는 중”이었다.
2. 두 기기는 같은 서브넷이었다. 당시 PC 이더넷은 `192.168.0.9`, iPhone Wi-Fi는 `192.168.0.26`, VTube Studio UDP 포트는 `21412`, PC 수신 포트는 `11125`였다. 다른 PC에서 이 주소를 그대로 사용하지 말 것.
3. `Get-NetUDPEndpoint -LocalPort 11125`는 Unity Editor가 포트를 열었다고 표시했지만, 이 사실만으로 패킷 수신까지 확인되지는 않았다.
4. 관리자 권한 `pktmon` 캡처에서 iPhone→PC 패킷이 Ethernet 어댑터까지 들어왔고, 추적 패킷은 대략 2.6 KB로 Ethernet MTU보다 커 IP 조각으로 관찰됐다.
5. 초기에는 Windows에서 `DropReason INET: receive inspection` 및 `transport endpoint was not found`가 반복됐다. UDP 11125 포트 허용 규칙만 추가해도 해결되지 않았다.
6. 실제 차단 규칙은 `Unity 6000.3.23f1 Editor`의 **Public 프로필 인바운드 Block**이었다. 해당 차단 규칙을 비활성화하고 `AI-ON iPhone Gaze UDP 11125` 허용 규칙을 유지한 뒤, 재캡처에서 패킷이 11125로 들어오고 IP 수신 드롭 표시가 사라졌다.
7. 이후 한 캡처에서는 앱이 UDP `21412`에 연결할 수 없다는 ICMP 응답이 관찰됐다. VTube Studio가 백그라운드/비활성 상태일 수 있었다. 사용자가 앱을 다시 켠 후에는 추적 데이터 조각이 다시 PC `11125`로 도착했다.
8. 네트워크 캡처에서 NIC 도착은 확인했지만 Unity 화면의 얼굴 추적 완료/눈 표본 증가까지 마지막으로 재확인하지는 못했다. 다음 담당자는 Unity 상태 문구와 샘플 수를 확인해 앱 전달까지 마무리할 것.

## 현재 작업 PC에서 확인된 방화벽 상태

마지막 점검 시:

- `AI-ON iPhone Gaze UDP 11125`: Enabled, Inbound Allow, UDP 11125, Private/Public
- `Unity 6000.3.23f1 Editor`의 Domain Allow: Enabled
- `Unity 6000.3.23f1 Editor`의 Public Block: Disabled
- `codex_sandbox_offline_block_inbound`: Disabled (이 개발 PC에만 있던 전역 차단 규칙)
- Public 방화벽 자체는 Enabled 상태

`codex_sandbox_offline_block_inbound`는 이 개발 환경에서 발견된 광범위한 로컬 차단 규칙이다. 학교 PC에서 같은 이름의 규칙이 보여도 그대로 비활성화하지 말고, 먼저 PC 소유자/관리자와 정책을 확인한다. 학교 관리 기기의 보안 정책을 임의로 우회하지 않는다.

## 학교 PC에서의 권장 진단 순서

### 1. 주소와 인터페이스

일반 PowerShell:

```powershell
Get-NetConnectionProfile
Get-NetIPConfiguration
```

- Unity 화면에는 **iPhone의 현재 Wi-Fi IPv4**와 iOS VTube Studio에 표시된 UDP 포트를 넣는다.
- `21412`는 흔한 기본값일 뿐이다. iPhone 앱 표시 포트를 우선한다.
- `11125`는 PC 수신 포트이므로 iPhone 주소/포트 입력칸에 넣지 않는다.
- 유선 PC와 Wi-Fi iPhone 조합은 가능하지만 같은 로컬 네트워크에서 서로 통신할 수 있어야 한다.
- VPN, 게스트/캠퍼스 Wi-Fi client isolation, VLAN 분리, 공유기 AP isolation, 학교 네트워크의 기기 간 통신 정책이 원인일 수 있다.

### 2. Unity 리스너와 방화벽 규칙

Unity 연결 버튼을 누른 상태에서 관리자 PowerShell:

```powershell
Get-NetUDPEndpoint -LocalPort 11125
Get-NetFirewallRule -Enabled True -Direction Inbound -Action Block |
  Select-Object DisplayName,Profile,PolicyStoreSourceType
Get-NetFirewallRule -DisplayName "AI-ON iPhone Gaze UDP 11125" |
  Format-List Enabled,Profile,Direction,Action
```

- UDP 11125 허용 규칙이 활성화돼 있는지, 현재 네트워크 프로필이 그 규칙에 포함되는지 확인한다.
- 허용 규칙보다 **Unity Editor/Unity Player의 명시적 Block 규칙이 우선**할 수 있다. Unity 버전/실행 파일에 맞는 Block 규칙이 있는지 찾는다.
- Block이 실제 원인으로 확인되면, PC 소유자/관리자 승인 아래 해당 Unity 프로그램의 해당 네트워크 프로필 Block만 조정한다. 방화벽 전체를 끄지 않는다.
- 학교 관리자가 방화벽을 정책으로 관리하거나 규칙 변경이 거부되면 우회하지 말고 네트워크/IT 담당자에게 UDP `11125` 인바운드 및 앱 통신 허용을 요청한다.

이 개발 PC에서 쓴 규칙 생성 명령(관리자 PowerShell):

```powershell
New-NetFirewallRule -DisplayName "AI-ON iPhone Gaze UDP 11125" `
  -Direction Inbound -Action Allow -Protocol UDP -LocalPort 11125 `
  -Profile Private,Public
```

### 3. 패킷 도착을 직접 확인

관리자 PowerShell에서 5초 캡처한다. `<PHONE_IP>`를 실제 아이폰 주소로 바꾼다.

```powershell
pktmon filter remove
pktmon filter add iPhone -i <PHONE_IP>
pktmon start --capture --comp all --pkt-size 0 --file-name C:\Temp\gaze.etl
Start-Sleep -Seconds 5
pktmon stop
pktmon etl2txt C:\Temp\gaze.etl --out C:\Temp\gaze.txt
Select-String C:\Temp\gaze.txt -Pattern '11125|DropReason|unreachable'
```

로그 해석:

- `<PHONE_IP>.<임시포트> > <PC_IP>.11125`가 보이면 아이폰 추적 패킷이 PC 네트워크 어댑터까지 도착했다.
- `DropReason INET: receive inspection` 또는 `transport endpoint was not found`가 보이면 Windows 네트워크/방화벽 계층에서 전달되지 않았을 수 있다. 같은 시각 Unity 리스너 PID와 명시적 Block 규칙을 대조한다.
- PC의 `<PC_IP>.11125 > <PHONE_IP>.21412` 요청만 보이고, 아이폰에서 `ICMP ... udp port 21412 unreachable`가 돌아오면 iOS 앱의 UDP 리스너가 그 시점에 요청을 받지 않는 것이다. 앱을 화면에 띄우고 3rd Party PC Clients를 껐다 켠 다음 Unity 연결을 재시작한다.
- NIC 패킷 도착만으로 Unity의 JSON 파싱이나 얼굴 인식이 증명되지는 않는다. Unity 화면의 상태와 수신 샘플 수를 이어서 확인한다.
- PktMon은 관리자 권한이 필요하다. 학교 정책상 승격/캡처가 차단되면 관리자에게 요청한다.

## 코드상 진단 표시

수신 코드에서 연결 후 5초 이상 데이터가 없으면 UDP 패킷 0건인지, 패킷은 왔지만 유효한 `Timestamp` 데이터가 없는지 다른 문구로 보여준다. Unity Editor에서 스크립트가 재컴파일되지 않았거나 오래된 빌드를 쓰면 이 문구가 없을 수 있다. Unity Console의 컴파일 오류를 먼저 확인한다.

## 방향/보정 관련 주의

- 2026-11-05부터 포인터는 눈 회전(`EyeLeft`/`EyeRight`, USB는 `EyeLeftX/Y`·`EyeRightX/Y`)과 얼굴 회전(`Rotation`, USB는 `FaceAngleX/Y`)을 함께 쓴다. `GazeMapper`가 눈만·고개만·눈+고개 모델 중 leave-one-out 오차가 가장 작은 것을 고르므로, 보정 때 고개를 움직인 사람도 맞춰진다.
- 눈 깜박임(`EyeBlinkLeft/Right` ≥ 0.7, USB는 `EyeOpenLeft/Right` ≤ 0.3) 프레임은 보정 표본에서 빠지고 포인터를 움직이지 않는다. 눈 뜬 표본이 점당 10개 미만이면 그 점을 다시 측정한다. 별잡기에서는 눈을 감은 동안 응시 진행도가 늘지도 줄지도 않는다.
- 보정 후 상태 줄의 "오차 약 Npx"는 보정점 예측 오차와 프레임 단위 흔들림을 합친 추정치다. 실기기 정확도는 아직 측정하지 않았다.
- 무보정 보기는 원시 눈 회전값에 고정 배율을 적용한 방향 확인용 미리보기다. 화면의 실제 응시 위치를 맞추려면 보정이 필요하다.
- 시선 별잡기(`GameController.GazeStarGame.cs`)는 웹캠과 iPhone 모두에서 된다. 보정 후 실행. 3초 카운트다운, 30초 제한, 0.65초 dwell, 들어갈 때 165px·나갈 때 215px 판정, 잠깐 벗어나면 진행도가 줄어들 뿐 리셋되지 않음, 눈 감는 동안 진행도 정지. 나타난 지 1.2초 안에 잡으면 2점, 10초 뒤부터 별이 움직이고 마지막 10초엔 더 빨라진다. 최고 점수는 PlayerPrefs `GazeTest.StarBestScore`.
- 게임 중 자동 보정(드리프트 보정): 별을 잡았다는 건 그 별을 보고 있었다는 뜻이므로, iPhone은 잡는 동안의 평균 빗나감만큼 `GazeMapper.AddAnchor`로 위치를 옮긴다(한 번에 35%, 최대 화면 15%, 화면 20% 넘게 빗나간 건 무시, 재보정 시 초기화). 웹캠은 UnitEye의 `onlineDriftCorrection`을 켜고(클릭 앵커는 끔) 별이 나타날 때 `ReportAttentionEvent`, 움직이는 별은 `FeedPursuitTarget`으로 알려 준다.
- 2026-11-05 3차(옆·아래 개선): 보정점이 13개(3×3 + 중앙 십자 안쪽 4점 (.50,.31) (.29,.50) (.71,.50) (.50,.69))로 늘었다. 0.75초 정착 후 1.2초 측정.
- Wi-Fi UDP는 ARKit 눈모양 블렌드셰이프(`EyeLookIn/Out/Up/DownLeft·Right`)를 `LookFeature`로 함께 읽는다. 아래를 보면 눈꺼풀 때문에 눈 회전값이 작게 나오고 옆 끝에서는 포화되는데, 눈모양 값은 그때도 비례해서 움직인다. USB/API 경로는 이 값이 없어 0이다.
- `GazeMapper`는 가로·세로를 따로 고른다. 축마다 눈/고개/눈모양/눈+고개/눈+눈모양/고개+눈모양/셋 다/2차 곡선/3차 곡선/눈+고개+3차 후보 중 (leave-one-out 오차² + 프레임 흔들림²)^½ × (1 + 0.04×(입력 수−1))이 가장 작은 것. 상태 줄에 "가로 … · 세로 …"로 표시된다.
- 아래를 볼 때 눈꺼풀이 내려와 깜박임으로 오인되지 않도록, 깜박임 기준이 아래를 보는 정도(−LookY)에 따라 0.7에서 최대 0.9까지 올라간다.
- 합성 테스트(아래 볼 때 눈 회전 60% 감소 + 옆 포화): 기존 9점·눈모양 없음 49px → 13점·눈모양 20px(옆 45→19, 아래 37→21). USB 경로(눈모양 없음)도 13점이면 34px. 이상적인 트래커에선 13→12px로 차이 없음.
- 웹캠(UnitEye): 추적 모델 선택 추가 — 기본(EyeMU, 빠름) / 정밀(EyeMU+ResNet34, 눈+얼굴 방향, 기본값). PlayerPrefs `GazeTest.WebcamModel`. 모델을 바꾸면 다시 보정해야 한다. 보정은 2바퀴(`LoadCalibration(9, 20, 2)`). 초당 샘플 수와 지연을 표시하고, 정밀 모델이 초당 12회 미만이면 기본 모델을 권한다.
- 웹캠 "화면 맞춤 (13점)"(`GameController.WebcamFit.cs`): UnitEye 보정(1번) 뒤에 같은 13점을 응시하면, UnitEye 출력 + 고개 각도(`Provider.HeadPoseEuler`) + MediaPipe eyeLook 블렌드셰이프(`IGazeRecordingSource.TryCopyEyeBlendshapes`)로 `GazeMapper`를 한 번 더 맞춘다. UnitEye 릿지 보정이 가운데로 끌어당기고 노트북 카메라가 화면 위에 있어 아래가 눌리는 문제를 편다. 합성 테스트: 가운데 쏠림+아래 눌림 185→87px(옆 259→85, 아래 227→89), 원래 정확한 경우 55→54px(손해 없음). 화면 맞춤 중에는 UnitEye `onlineDriftCorrection`을 끄고 별잡기 앵커를 `webcamMapper.AddAnchor`로 받는다. 1번 보정을 다시 하거나 모델을 바꾸면 화면 맞춤은 지워진다. 기존 "가장자리 정확도 높이기"(HomulerGazeEvaluation) 버튼은 Input System 설정이 필요하고 이 기능과 겹쳐서 뺐다.
- 깜박임 판정: 아래를 보면 눈꺼풀이 내려와 깜박임으로 잡힌다. 웹캠·iPhone 모두 0.45초 넘게 이어지는 '감김'은 깜박임이 아니라 아래 보기로 보고 포인터·별 진행도를 멈추지 않는다(`ShortBlink`, 확실히 감은 값은 예외: iPhone EyeBlink ≥ 0.92, 웹캠 eyeBlink ≥ 0.85). UnitEye `maxBlinkHoldSeconds`도 0.45초로 줄였다. 보정 표본은 여전히 감긴 프레임을 모두 뺀다.
- `GazeMapper` 축별 후보에 비대칭 곡선(눈+눈²+눈³)과 눈+눈모양+눈³을 추가했다. iPhone USB(눈모양 없음) 합성 34→28px.

## 이어받는 사람이 먼저 할 일

1. 최신 `main`을 받고 Unity `6000.3.23f1`에서 스크립트 컴파일 오류가 없는지 확인한다.
2. 자신의 iPhone/PC 주소와 실제 UDP 포트를 다시 읽고, 위 순서대로 현재 네트워크 프로필·Unity Block/Allow 규칙을 확인한다.
3. 포트 11125 리스너가 열린 상태에서 PktMon 캡처를 수행한다.
4. NIC 도착, WFP 드롭, ICMP unreachable, Unity 상태 문구/샘플 수 중 어느 단계까지 확인했는지 기록한다.
5. 학교 네트워크 정책상 기기 간 UDP가 차단되면 Windows 설정을 반복 변경하지 말고 승인된 네트워크 또는 USB/API 경로로 테스트한다.

