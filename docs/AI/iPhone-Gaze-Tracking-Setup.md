# iPhone 시선 추적 팀원 설치·연결 가이드

작성일: 2026-11-04
대상 프로젝트: AI-ON, Unity `6000.3.23f1`
현재 통합 방식: VTube Studio iOS 추적 데이터 → Unity 시선 테스트 화면

이 문서는 팀원이 새 Windows PC에서 필요한 앱을 설치하고, Wi-Fi UDP 또는 USB 경로로 iPhone 추적을 확인하는 절차입니다. Unity 게임 화면에는 VTube Studio 캐릭터를 띄우지 않습니다. USB 경로에서 VTube Studio PC 앱은 백그라운드 데이터 중계 역할을 합니다.

## 1. 필요한 것

- Face ID를 지원하는 iPhone/iPad 또는 A12 Bionic 이상 기기. 지원 여부는 [VTube Studio 요구 사항](https://github.com/DenchiSoft/VTubeStudio/wiki/Introduction-%26-Requirements)을 확인하세요.
- Windows 10/11 PC와 노트북/PC에 연결된 네트워크.
- iPhone 앱 **VTube Studio**: [App Store](https://apps.apple.com/app/vtube-studio/id1511435444)
- Unity 프로젝트 편집용 **Unity Hub** 및 Unity Editor `6000.3.23f1`: [Unity Hub 설치](https://unity.com/download), [Unity 버전 아카이브](https://unity.com/releases/editor/archive)
- USB 경로를 쓸 때 추가로 필요한 **VTube Studio PC 앱**: [Steam](https://store.steampowered.com/app/1325860/VTube_Studio/)
- USB 경로를 쓸 때 Windows의 Apple 기기 드라이버: Microsoft Store에서 [Apple Devices](https://support.apple.com/en-us/118290) 앱을 설치하거나 iTunes를 설치합니다. USB 페어링이 안 되면 Apple Devices/iTunes를 한 번 실행하세요. [Apple USB 연결 안내](https://github.com/DenchiSoft/VTubeStudio/wiki/Streaming-to-Mac-PC)

현재 구현은 별도 iOS 앱이나 Xcode 빌드를 요구하지 않습니다. VTube Studio 외 앱을 사용하는 연결은 아직 이 Unity 코드에서 지원하지 않습니다.

VTube Studio 캐릭터를 사용할 필요는 없습니다. 추적값만 중계하므로 PC 앱은 최소화해도 됩니다. iPhone 앱에서도 모델을 열지 않고 카메라 미리보기를 끌 수 있습니다. [VTube Studio FAQ](https://github.com/DenchiSoft/VTubeStudio/wiki/FAQ)

## 2. Unity 프로젝트 준비

1. 팀 저장소에서 **iPhone gaze tracking 구현이 포함된 최신 브랜치/커밋**을 받습니다. 프로젝트에 `Assets/Scripts/GameController.IPhoneGaze.cs`와 `Assets/Scripts/IPhoneGazeUdpReceiver.cs`가 있어야 합니다.
2. Unity Hub에서 `6000.3.23f1`을 설치합니다. 프로젝트 버전은 `ProjectSettings/ProjectVersion.txt`에서 확인할 수 있습니다.
3. Unity Hub의 **Add/Open project**로 프로젝트 폴더를 엽니다. 최초 실행 때 패키지 가져오기와 스크립트 컴파일이 끝날 때까지 기다립니다.
4. `Assets/Scenes/Main.unity`를 열고 Play를 누릅니다.
5. 인트로를 지나 학과 선택 첫 화면에 도달하면 우측 아래 **시선 추적 테스트**를 누르고 상단의 **iPhone 추적**을 선택합니다.

시선 점을 화면 좌표로 맞추려면 연결 후 9지점 보정이 필요합니다. 점은 화면에 3×3 격자로 표시됩니다. 휴대폰을 눈높이에 고정하고, 각 분홍 점을 눈으로 바라본 뒤 **이 지점 측정** 버튼을 누르세요. 점이 바뀔 때까지 고개와 휴대폰 위치를 유지합니다.

## 3. 방법 A — Wi-Fi UDP 직접 연결 (첫 테스트 권장)

이 경로는 iPhone VTube Studio 앱이 Unity에 직접 UDP 추적 데이터를 보냅니다. VTube Studio PC 앱이나 USB 케이블은 필요하지 않습니다.

### iPhone 설정

1. iPhone에서 VTube Studio를 열고 카메라 권한을 허용합니다. 얼굴이 카메라에 잡히는지 확인하세요.
2. 설정 첫 번째 탭을 열고 아래쪽의 **3rd Party PC Clients**를 켭니다. 이 기능이 외부 PC 프로그램의 UDP 요청을 받습니다. [공식 UDP 프로토콜 예제](https://github.com/DenchiSoft/VTubeStudioBlendshapeUDPReceiverTest)
3. VTube Studio에 표시된 포트 번호를 확인합니다. 보통 `21412`입니다.
4. iOS **설정 → Wi-Fi → 현재 네트워크 옆 ⓘ → IP 주소**에서 그 Wi-Fi의 iPhone IPv4 주소를 확인합니다. 앱의 **IP 목록 보기**에 여러 네트워크 주소가 보이면 현재 연결된 Wi-Fi 세부 정보의 IP를 기준으로 선택하세요.

### Unity 연결

1. Unity 시선 테스트의 **Wi-Fi UDP**를 선택합니다.
2. iPhone Wi-Fi 설정의 IP 주소와 VTube Studio 포트를 입력합니다. 예: `192.168.0.26`, 포트 `21412`. 이 값은 예시이며 팀원마다 다릅니다.
3. **iPhone Wi-Fi 연결**을 누르고 얼굴 추적 데이터를 기다립니다.
4. 얼굴 추적이 잡히면 **9지점 보정 시작**을 누르고 안내에 따라 측정합니다.

### 네트워크 조건

- PC와 iPhone이 서로 통신할 수 있는 같은 로컬 네트워크에 연결돼야 합니다. 게스트 Wi-Fi, 공용 캠퍼스 Wi-Fi, 공유기의 AP/client isolation은 기기 간 통신을 막을 수 있습니다.
- Windows 방화벽 창이 뜨면 신뢰하는 네트워크에서 Unity Editor의 **개인 네트워크** 통신을 허용하세요. 방화벽 전체를 끄지 마세요.
- Unity 수신기는 PC의 UDP `11125` 포트를 사용합니다. VTube Studio 앱의 포트(기본 `21412`)와 혼동하지 마세요.
- VPN은 끄고 테스트하세요.

## 4. 방법 B — USB 연결 + VTube Studio PC API

이 경로의 데이터 흐름은 **iPhone VTube Studio → USB → VTube Studio PC 앱 → 로컬 WebSocket API → Unity**입니다. Unity의 USB 선택은 iPhone USB 장치를 직접 읽는 기능이 아니라 PC 앱의 API를 읽는 연결입니다.

### 준비 및 연결

1. PC에 VTube Studio를 설치하고 실행합니다.
2. Windows에 Apple Devices 또는 iTunes를 설치합니다. iPhone을 케이블로 PC에 연결하고 잠금을 해제한 뒤 **이 컴퓨터를 신뢰**를 승인합니다. Apple Devices/iTunes가 iPhone을 인식하는지 확인합니다.
3. iPhone VTube Studio에서 **Connect USB**를 먼저 켭니다. `USB started. Waiting for PC app to connect.`와 비슷한 대기 상태가 표시되어야 합니다.
4. PC VTube Studio에서도 **Connect USB**를 켭니다.
5. PC VTube Studio 설정에서 **Allow Plugin API access**를 켜고 실제 API 포트를 확인합니다. 기본 포트는 `8001`이며 다른 값으로 설정돼 있으면 그 값을 씁니다. [VTube Studio API 안내](https://github.com/DenchiSoft/VTubeStudio/wiki/Plugins)
6. Unity 테스트 화면에서 **USB · VTube Studio**를 선택하고 포트를 입력한 뒤 **VTube Studio USB 연결**을 누릅니다.
7. PC VTube Studio에 뜨는 **AI-ON Gaze Test** API 접근 요청을 허용합니다.
8. Unity에서 얼굴 추적 상태가 확인되면 9지점 보정을 진행합니다.

USB 연결이 PC 앱에 나타나지 않으면 케이블 연결, iPhone 잠금 해제/신뢰 승인, Apple Devices/iTunes 설치 및 1회 실행 여부를 확인합니다. iPhone에서 USB 연결을 먼저 시작해야 합니다. [공식 USB 연결 안내](https://github.com/DenchiSoft/VTubeStudio/wiki/Streaming-to-Mac-PC)

## 5. 연결 방식 선택 기준

| 방식 | PC의 추가 앱 | 네트워크/케이블 | Unity 입력 |
| --- | --- | --- | --- |
| Wi-Fi UDP | 불필요 | 같은 로컬 Wi-Fi | iPhone Wi-Fi IP, VTube Studio 포트(보통 21412) |
| USB/API | VTube Studio PC 앱, Apple Devices 또는 iTunes | USB 케이블과 페어링 | PC VTube Studio API 포트(보통 8001) |

두 방법을 동시에 켜지 말고 Unity 화면에서 한 번에 하나씩 선택해 테스트합니다. VTube Studio iOS 무료 버전은 PC 스트리밍이 5분 뒤 끊길 수 있습니다. 짧은 연결/보정 확인에는 사용할 수 있고, 장시간 테스트 조건은 [VTube Studio FAQ](https://github.com/DenchiSoft/VTubeStudio/wiki/FAQ)를 확인하세요.

## 6. 문제 해결

### Unity가 계속 “iPhone 데이터를 기다리는 중”이라고 표시

1. Wi-Fi 경로라면 iPhone에서 **3rd Party PC Clients**가 켜져 있는지 확인합니다. 단순히 Wi-Fi가 연결된 것만으로는 UDP 추적 요청에 응답하지 않습니다.
2. Unity IP 입력란에는 **노트북 IP가 아니라 iPhone의 현재 Wi-Fi IPv4**를 입력합니다. iOS 설정의 Wi-Fi 세부 정보에서 확인하세요. VTube Studio의 IP 목록에는 다른 인터페이스 주소도 나올 수 있습니다.
3. VTube Studio 앱의 포트가 `21412`인지 확인합니다. PC 수신 포트 `11125`를 iPhone 포트 칸에 넣지 않습니다.
4. 두 기기의 IP와 서브넷이 같은 로컬 네트워크인지 확인합니다. 예를 들어 PC `192.168.0.27/24`, iPhone `192.168.0.26/24`라면 같은 대역입니다. 팀원은 자신의 실제 주소를 사용해야 합니다.
5. Windows 방화벽의 신뢰 네트워크 허용, VPN, 게스트 Wi-Fi, 공유기 기기 간 통신 차단을 확인합니다.
6. Unity에 **UDP 포트 11125를 열 수 없다**는 메시지가 나오면 다른 프로그램이나 이전 실행이 포트를 사용 중인지 확인하고, Unity 테스트를 종료한 뒤 다시 시작합니다.

### USB/API 경로가 연결되지 않음

- PC VTube Studio에서 iPhone 추적이 실제로 들어오는지 확인합니다.
- PC 앱에서 API 접근을 켰는지, Unity 입력 포트가 PC 앱에 표시된 API 포트와 같은지 확인합니다.
- API 승인 창에서 **AI-ON Gaze Test** 접근을 허용합니다. API 기본 포트는 `8001`이지만 사용자가 바꿀 수 있습니다.
- iPhone USB를 먼저 켜고 PC 앱 USB 연결을 다음에 켭니다. 케이블과 기기 신뢰 상태를 확인합니다.

### 얼굴은 잡히지만 보정 점이 안 나옴

- 화면에 얼굴 추적/눈 추적 상태가 표시된 뒤 **9지점 보정 시작**을 누릅니다.
- 각 점 측정 중에는 눈을 점에 고정하고 고개와 기기를 움직이지 않습니다.
- 조명을 밝고 고르게 하고, 얼굴 전체가 전면 카메라에 잡히도록 합니다. 앱이 5분 뒤 끊겼다면 무료 스트리밍 제한인지 확인합니다.

## 7. 확인 상태와 범위

- **Wi-Fi UDP:** 프로젝트 소유자가 실제 iPhone에서 연결 동작을 확인했습니다. 팀원별 네트워크 주소와 방화벽 정책은 다르므로 각 PC에서 위 절차가 필요합니다.
- **USB/API:** 코드와 절차는 준비돼 있지만 실제 iPhone/PC USB 연결은 아직 별도 확인이 필요합니다.
- 두 경로 모두 9지점 보정 후 화면에 추정 위치를 표시합니다. 9개의 화면 분포 샘플을 하나의 최소제곱 affine fit에 사용합니다. 이는 실험용 시선 표시 기능이며 연구/의료 수준의 정밀 아이트래킹을 의미하지 않습니다.
