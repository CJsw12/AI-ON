# 두 가지 입력을 지원하는 시선 미니게임 구상

- 상태: 기획안. 두 경로의 실제 동작과 정확도는 아직 검증하지 않았다.
- 대상: Windows 노트북에서 실행하는 AI-ON 미니게임.
- 이 문서는 방 선택에 한정했던 시선 입력 설계를 대체한다.

## 목표

같은 미니게임에서 사용자가 **노트북 웹캠** 또는 **iPhone** 시선 입력을 선택한다. 마우스·키보드도 대체 입력으로 제공한다. 방 선택은 필수가 아니며, 실제 추적 성능을 확인한 뒤 게임을 정한다. 시작 후보는 화면 왼쪽·가운데·오른쪽에 나타나는 표적을 바라보는 짧은 게임이다.

## 입력 경로

1. **노트북 웹캠:** UnitEye의 얼굴·눈 추적 및 시선 추정 기능을 검토한다. 사용자 보정 후 게임 입력으로 전달한다. Unity 6000.3.23f1 및 Windows 빌드에서 호환성을 확인해야 한다.
2. **iPhone:** VTube Studio iOS 앱에서 두 전송 방식을 선택한다. Wi-Fi는 `3rd Party PC Clients` UDP 데이터(눈 회전)를 Unity가 직접 받는다. USB는 VTube Studio PC 앱에 연결된 iPhone을 USB로 연결한 뒤, Unity가 PC 앱의 WebSocket API에서 눈 추적 파라미터를 읽는다. 두 방식 모두 눈 추적값을 9지점(3×3) 보정으로 PC 화면 좌표에 매핑한다. 실제 구분 성능은 기기에서 검증한다.
3. **대체 입력:** 카메라가 없거나 추적이 불안정할 때 마우스·키보드로 플레이한다.

## 사용자 흐름

입력 선택 → 해당 장치/전송 방식 연결과 추적 상태 확인 → 간단한 보정 → 미니게임 → 종료 시 입력 정리. 웹캠과 iPhone을 선택할 수 있으며, iPhone은 Wi-Fi UDP 또는 USB와 PC 앱 API 중에서 고른다. 입력을 동시에 켤 필요는 없다. 게임 화면에 현재 입력과 추적 상태를 표시한다.

## 프로젝트 연결

기존 AI-ON 미니게임은 일반 C# 클래스이므로 카메라와 UDP 수명은 `GameController`에 연결된 별도 `MonoBehaviour` 호스트가 담당한다. 웹캠과 iPhone 제공자는 추적 가능 여부와 공통 방향 또는 좌표 표본을 제공하고, 게임은 입력 장치의 세부 구현을 알 필요 없이 표본을 읽는다. 제공자 전환, 설정 메뉴, 게임 종료 시 선택 이벤트를 막고 장치를 정리한다.

## 확인할 사항

- 웹캠: UnitEye 패키지 및 모델 설치, 프로젝트의 기존 입력 설정과 호환성, Windows 빌드, 시선 보정과 성능.
- iPhone Wi-Fi: VTube Studio 앱에서 `3rd Party PC Clients`를 켜고 iPhone과 PC를 같은 Wi-Fi에 연결한다. 앱에 표시된 iPhone 수신 포트를 사용한다. Windows 방화벽의 로컬 UDP 수신 허용, 패킷 끊김, 9지점 보정 및 화면 구분 성능을 확인한다.
- iPhone USB: VTube Studio PC 앱과 iOS 앱의 USB 연결, Windows Apple Devices/iTunes 드라이버, PC 앱의 API 접근 허용, 인증 승인 및 9지점 보정을 확인한다.
- 공통: 실측 결과에 맞는 게임 방식과 선택 시간, 추적이 끊겼을 때의 대체 입력, 모델 및 패키지 고지.
- 두 입력 경로 모두 실제 기기에서 동작할 때까지 구현 가능성과 정확도는 확정하지 않는다.

참고: [UnitEye](https://github.com/wgnrto/uniteye), [VTube Studio iOS 데이터 수신 및 Unity 예제](https://github.com/DenchiSoft/VTubeStudioBlendshapeUDPReceiverTest).
