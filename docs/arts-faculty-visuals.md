# 예체능 3개 학과 교수 이미지·배경 적용

## 목적과 범위

실용음악과, 시각디자인학과, 연극영화과에 학과별 교수 초상과 전용 공간 배경을 추가하고 각 스토리 데이터에 연결했다. 체육 관련 학과는 이번 변경 범위에 포함하지 않았다. 이미지 소스는 새로 생성했으며 기존 학과 이미지를 덮어쓰지 않았다.

## 학과별 구성

### 실용음악과

| 교수 | 이미지 콘셉트 | Resource 이름 |
| --- | --- | --- |
| 신도윤 교수 | 검은 셔츠와 스튜디오 헤드폰, 절제된 표정의 완벽주의 프로듀서. 믹싱 페이더를 든 모습 | `ProfessorShinDoYoon` |
| 류보라 교수 | 플럼 계열 재킷과 밴드 티셔츠, 탬버린을 든 자유로운 즉흥 연주자 | `ProfessorRyuBoRa` |

전용 배경 `MusicRehearsalStudio`는 녹음·합주 장비가 있는 실내 스튜디오다. 1장 및 축제/버스킹 장면은 기존 배경을 유지하며, 스튜디오가 배경인 나머지 구간에 사용한다.

### 시각디자인학과

| 교수 | 이미지 콘셉트 | Resource 이름 |
| --- | --- | --- |
| 남궁현 교수 | 은발과 검은 하이넥 재킷, 컬러 휠을 든 격식 있는 미학자 | `ProfessorNamgungHyun` |
| 하은채 교수 | 트렌디한 캐주얼 셔츠와 태블릿, 빠르고 솔직한 피드백을 주는 디자이너 | `ProfessorHaEunChae` |

전용 배경 `VisualDesignStudio`는 작업 테이블, 색상 샘플과 디자인 도구를 갖춘 실내 작업실이다. 컴퓨터실·도서관·카페·강당 등 기존 챕터 장소는 유지하고, 작업실 장면과 기본 배경에서 새 이미지를 사용한다.

### 연극영화과

| 교수 | 이미지 콘셉트 | Resource 이름 |
| --- | --- | --- |
| 엄태오 교수 | 버건디 벨벳 재킷과 대본, 과장되고 낭만적인 연극 연출가 | `ProfessorEomTaeO` |
| 차유리 교수 | 검은 의상과 헤드폰·카메라 뷰파인더, 차분하고 정확한 감독 | `ProfessorChaYuRi` |

전용 배경 `TheaterBlackBox`는 조명과 무대 장비가 보이는 실내 블랙박스 극장/촬영 공간이다. 1·2장과 기본 무대 배경에 연결했다. 야외 및 비 오는 장면처럼 장소가 대사에 명시된 챕터는 기존 배경을 유지한다.

## 파일 및 연결 구조

- 교수 이미지: `Assets/Resources/Professor{이름}Transparent.png`
- 표정 슬롯 호환 파일: 같은 초상을 `HappyTransparent`, `FrownTransparent` 접미사 파일로도 제공한다. 현재 세 파일은 같은 그림이며, 표정별 새 아트가 생기면 해당 두 파일만 교체하면 된다.
- 배경 이미지: `Assets/Resources/MusicRehearsalStudio.png`, `VisualDesignStudio.png`, `TheaterBlackBox.png`
- 교수 이름 및 배경 선택: `Assets/Scripts/Stories/ArtsSports/MusicStory.cs`, `VisualDesignStory.cs`, `TheaterFilmStory.cs`
- 새 초상은 자연과학부 커스텀 교수와 같은 전체 인물 렌더링·선택 화면 얼굴 크롭 경로를 사용한다. 관련 이름 판별은 `Assets/Scripts/GameController.cs`, `Assets/Scripts/GameMenus.cs`에 등록했다.
- 각 PNG 옆 `.meta` 파일에는 개별 GUID가 지정되어 있다. 이미지 파일명은 `Resources.Load`가 사용하는 리소스 키와 일치한다.

## 확인 사항

- 모든 스토리 리소스 이름에 대응하는 PNG가 있고, Unity `.meta` 파일도 함께 추가했다.
- 교수 초상 메타 GUID 중복 여부와 `git diff --check`를 확인했다.
- Unity 에디터에서 실제 플레이 화면 및 빌드 검증은 이 문서 작성 시 수행하지 않았다.
