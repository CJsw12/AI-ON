# iPhone 시선 추적 연결 장애 인수인계

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

- 현재 포인터는 VTube Studio의 `EyeLeft`/`EyeRight` 회전값을 사용한다. 얼굴 `Rotation`은 시선 좌표 계산에 포함하지 않는다.
- 9지점 보정은 고개를 화면 정면에 두고 눈으로 점을 따라간다. 고개를 좌우/상하로 움직이는 동작과 눈동자 시선 변화는 같은 입력이 아니다.
- 무보정 보기는 원시 눈 회전값에 고정 배율을 적용한 방향 확인용 미리보기다. 화면의 실제 응시 위치를 맞추려면 보정이 필요하다.
- 시선 별잡기는 보정 후에 실행한다. 30초 제한, 0.65초 dwell, 큰 포인터, 넉넉한 판정 반경으로 설계됐다.

## 이어받는 사람이 먼저 할 일

1. 최신 `main`을 받고 Unity `6000.3.23f1`에서 스크립트 컴파일 오류가 없는지 확인한다.
2. 자신의 iPhone/PC 주소와 실제 UDP 포트를 다시 읽고, 위 순서대로 현재 네트워크 프로필·Unity Block/Allow 규칙을 확인한다.
3. 포트 11125 리스너가 열린 상태에서 PktMon 캡처를 수행한다.
4. NIC 도착, WFP 드롭, ICMP unreachable, Unity 상태 문구/샘플 수 중 어느 단계까지 확인했는지 기록한다.
5. 학교 네트워크 정책상 기기 간 UDP가 차단되면 Windows 설정을 반복 변경하지 말고 승인된 네트워크 또는 USB/API 경로로 테스트한다.

