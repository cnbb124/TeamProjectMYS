# XR 메뉴 마우스 위치 및 Quit 선택 문제 작업 기록

- 작성일: 2026-08-03
- 실제 적용 대상 프로젝트: `C:\teamTeamProjectMYS`
- 이전 정상 비교본: `C:\TeamProjectMYS_qhr`
- 비교 전용 프로젝트: `C:\testTeamProjectMYS` — 수정하지 않음
- 별도 비교 프로젝트: `C:\TeamProjectMYS`
- 주요 참고 화면: `C:\Users\m\Pictures\Screenshots\스크린샷(2846).png`

## 1. 현재 상태 요약

이 문제는 하나의 원인이 아니라 다음 문제가 겹쳐 있었다.

1. Unity Editor에서 읽은 마우스 원본 좌표가 Game View 내부 좌표가 아니었다.
2. XR `Left Eye` 투영 행렬과 카메라 자식 패널 Transform 기준이 맞지 않았다.
3. EventSystem의 실제 Raycast 대상과 화면에 남는 버튼 Hover 표시가 서로 달랐다.
4. `LobbyButtonHover`가 별도의 `Image.fillAmount` 애니메이션을 사용해 기본 Button 상태와 다른 시각 표시를 만들었다.
5. 메뉴용 XR 리그 카메라와 씬 카메라가 동시에 존재하며, 메뉴 리그 카메라에는 `Tracked Pose Driver`가 없다.

좌표 진단이 완료된 시점에는 다음 사실이 로그로 확정되었다.

- 패널 영역: `(x:0.09, y:0.09, width:0.82, height:0.82)`
- 중앙 매핑: `(960, 540)`
- `NewGameButton`, `LoadGameButton`, `SettingButton`, `QuitButton` Raycast가 각각 정확히 구분됨
- `selected=<none>`이므로 Quit이 EventSystem의 선택 오브젝트로 고정된 것은 아님
- 수동 포인터 처리 실험에서는 New Game 클릭 후 `LOADING_SEQUENCE`를 거쳐 `BASE_STATION`에 정상 진입함

하지만 화면에서는 New Game과 Quit이 같이 활성화되어 보이는 현상이 남았다.

가장 마지막 실험은 현재 Raycast된 버튼만 강제로 표시하려는 `ApplyExclusiveHoverVisual` 방식이었다. 이 작업 이후 사용자 확인 결과는 다음과 같다.

> 다시 Quit만 선택되는 상태가 되었다.

따라서 이 마지막 강제 Hover 방식은 실패로 기록하며 그대로 다시 적용하면 안 된다.

## 2. 매우 중요한 현재 파일 상태 주의사항

문서 작성 시점의 디스크 상태는 마지막 런타임 테스트 당시 소스와 다르다.

- 마지막 런타임 로그에는 `ProcessManualPointer`, `XR Mouse Target Diagnostic` 등이 존재했다.
- 현재 디스크의 `XRMainMenuRenderTextureAdapter.cs`에는 그 수동 포인터 코드가 없다.
- 대신 현재 파일에는 `XRMenuSurface` 표식 기반 Canvas 선택 코드가 들어 있다.
- 다음 파일이 현재 새 파일로 존재한다.
  - `Assets/2.Scripts/UI/XRMenuSurface.cs`
  - `Assets/2.Scripts/UI/XRMenuSurface.cs.meta`
  - `Assets/Editor/XRMenuSurfaceSetup.cs`
  - `Assets/Editor/XRMenuSurfaceSetup.cs.meta`

즉, 마지막 실험 코드가 저장되지 않았거나 다른 작업에 의해 어댑터 파일이 교체된 상태다. 다음 작업을 시작할 때는 현재 파일을 무조건 마지막 테스트본으로 간주하면 안 된다.

현재 디스크에서 확인되는 관련 변경은 다음과 같다.

- `XRMainMenuRenderTextureAdapter.cs`
  - 현재는 `XRMenuSurface` 표식을 우선 사용하는 변경이 있음
  - 마지막으로 테스트했던 수동 포인터/직접 Hover 처리 코드는 없음
- `XRSceneUIAdapter.cs`
  - 런타임 자동 생성 활성화
  - 메뉴 씬 제외
  - 3D 씬에 `TrackedDeviceGraphicRaycaster`, `XRUIInputModule` 적용
- `LobbyButtonHover.cs`
  - 시작/비활성화 시 시각 상태 초기화 코드가 현재 남아 있음
  - `SetImmediateState`가 현재 남아 있음
- `XRInteractionModeCoordinator.cs`
  - 확인만 했으며 이 작업에서는 수정하지 않음

## 3. 원래 증상

### MAIN

- 실제 마우스는 New Game 또는 로고 위에 있는데 Quit Game이 활성화됨
- New Game을 누르면 게임이 종료되는 것처럼 보이거나 실제 종료됨
- Game View보다 위에 있는 Scene View 중간까지 마우스를 올려야 New Game이 선택됨
- 로드가 선택되었을 때 실제 마우스는 New Game에 있음
- 마우스를 따라다니는 네모 아이콘이 보였다가 안 보임
- Game View 스케일이나 카메라가 바뀌면 메뉴 위치와 클릭 위치가 다시 어긋남

### BASE_HANGAR 및 다른 씬

- 마우스 위치 표시가 보이지 않는 경우가 있음
- 화면 위쪽을 선택해야 아래쪽 메뉴가 반응함
- 스테이션, 맵 등에서 UI가 보이지 않는 증상이 있었음

### 전투 관련 별도 문제

- 기체 락온이 되지 않는 문제도 있었음
- 이 문서의 메뉴 좌표 작업에서는 아직 락온 문제를 수정하지 않음

## 4. 프로젝트 비교에서 얻은 내용

### 비교 대상

- `C:\TeamProjectMYS_qhr`
  - 구조 변경 전 정상 동작했다고 보고된 이전본
- `C:\teamTeamProjectMYS`
  - 최신 develop 기반으로 다시 적용하기로 선택한 기준 프로젝트
- `C:\testTeamProjectMYS`
  - 확인용으로만 사용, 수정 금지

### Git 이력에서 확인한 경계

- `c8dc3e62` — 2026-07-30
  - `XRMainMenuRenderTextureAdapter` 도입
  - 이전 정상본에도 RenderTexture 기반 메뉴 구조는 이미 있었음
- `6aefb819` — 2026-07-31
  - `XRInteractionModeCoordinator.cs`
  - `XRMenuControllers.prefab`
  - `XRRenderTexturePanelInput` 추가
  - 마우스 외에 컨트롤러용 두 번째 UI 포인터 경로가 생김
- `ee3288a1` — 2026-08-02
  - 씬 이름 변경 중심
  - `BASE_LANDING → BASE_HANGAR`
  - `GAME_OVER → RESULT`

이 비교로 이전 정상본은 컨트롤러용 두 번째 포인터 경로가 들어오기 전 상태였다는 점을 확인했다. 따라서 메뉴 문제는 마우스와 컨트롤러 경로가 동시에 같은 EventSystem과 UI를 건드리기 시작한 시점과 관련될 가능성이 높다.

## 5. 로그로 확정된 사실

### 5.1 Game View 크기

초기 진단 예시:

```text
Game View window: 879 x 408
실제 렌더링 영역: 879 x 387
상단 툴바: 21 px
Screen: 879 x 387
```

다른 테스트에서는 다음과 같았다.

```text
Screen=688x387
```

Game View 렌더링 크기와 `Screen.width/height` 자체는 일치했다. 따라서 단순한 Screen 크기 불일치만이 원인은 아니었다.

### 5.2 원본 마우스 좌표 문제

초기 로그에서는 `Screen.height=387`인데 원본 마우스 Y가 다음처럼 나왔다.

```text
raw y=561
raw y=661
raw y=765
raw y=868
raw y=918
```

이는 마우스 좌표가 Game View 안에서 끝나지 않고 Scene View까지 계속 증가한다는 뜻이다. 기존 코드는 이 값을 1920×1080으로 Clamp해서 사용했기 때문에, Scene View까지 올렸을 때 아래/위 메뉴가 반응하는 현상이 생겼다.

Game View `OnGUI` 좌표를 사용한 뒤에는 다음처럼 로컬 좌표를 얻었다.

```text
[XR Mouse GameView Diagnostic] local=(...), Screen=688x387
```

이 단계에서 마우스와 네모 아이콘이 같은 방향으로 움직이는 상태까지 확인했다.

### 5.3 XR 눈 투영 문제

`Left Eye` 기준 레이와 투영은 실패했다.

```text
[XR Mouse Plane Diagnostic] eye=Left, centerHit=False
```

패널 네 모서리를 `Left Eye`로 투영했을 때도 화면 밖 값이 나왔다.

```text
rect=(x:4.14, y:2.93, width:1.01, height:0.59)
centerHit=False
```

`Mono` 기준으로 변경한 후에는 정상 영역이 나왔다.

```text
[XR Mouse Panel Diagnostic]
eye=Mono
fallback=False
rect=(x:0.09, y:0.09, width:0.82, height:0.82)
centerHit=True
mapped=(960.0, 540.0)
```

따라서 이 프로젝트의 메뉴 마우스 좌표 계산에서는 `Left Eye` 행렬을 직접 기준으로 사용하면 안 된다.

### 5.4 Y축 반전 실험

Y축을 한 번 더 반전하는 실험을 했으나 실패했다.

- 마우스와 네모 아이콘이 화면 중심을 기준으로 서로 반대로 움직임
- 즉시 되돌림
- 정상값은 `yFlipped=False`

이 실험은 다시 적용하면 안 된다.

### 5.5 실제 UI Raycast 결과

진단 로그에서 버튼별 좌표는 정확했다.

```text
selectable=NewGameButton
selectable=LoadGameButton
selectable=SettingButton
selectable=QuitButton
selected=<none>
```

대표 좌표 예시:

```text
NewGameButton: mapped y 약 674~790
LoadGameButton: mapped y 약 528~647
SettingButton: mapped y 약 385~501
QuitButton: mapped y 약 300~355
```

이 결과로 좌표 계산과 `GraphicRaycaster`는 정상이라는 사실이 확정되었다. 이후에도 Quit이 같이 보인다면 그것은 좌표가 아니라 시각 Hover 상태 또는 별도 입력 경로 문제다.

## 6. 진행한 작업과 결과

### 6.1 이전에 실패하거나 되돌린 작업

| 작업 | 결과 | 상태 |
|---|---|---|
| 전체 Screen을 1920×1080으로 단순 정규화 | Scene View까지 올라가는 원본 좌표 때문에 Clamp됨 | 실패 |
| 마우스 `ScreenPointToRay` → Quad Collider | 중앙 히트 실패 | 되돌림 |
| 명시적 `Left Eye` 레이 | `centerHit=False` | 실패 |
| 컨트롤러 브리지만 비활성화 | 위치 오프셋이 남음 | 단독 해결 실패 |
| 한 카메라 Screen Space Camera | UI 확대/잘림 발생 | 되돌림 |
| 한 카메라 World Space 방식 | 위치 문제 해결 안 됨 | 되돌림 |
| 카메라 1개/2개 전환 | 증상 동일 | 원인 아님 |
| Game View 사각형 계산 초기 실험 | 입력 원본이 데스크톱 좌표여서 실패 | 당시 실패 |
| 패널 Collider 물리 Raycast | 중심 히트 실패 | 실패 |
| Y축 추가 반전 | 마우스와 네모 아이콘이 반대로 움직임 | 즉시 되돌림 |
| Quit PointerExit만 전송 | Quit 시각 상태가 계속 남음 | 불충분 |
| 모든 버튼 Hover 코루틴 초기화 | Quit 표시가 계속 남았다고 보고됨 | 불충분 |
| 현재 버튼만 강제 `fillAmount=1` | 사용자 보고: 다시 Quit만 선택됨 | 실패, 재적용 금지 |

### 6.2 Library 삭제 결과

Library를 삭제하고 재생성한 뒤 한 번은 Quit 고정 상태가 풀렸다고 보고되었다. 그러나 위쪽을 선택해야 하는 좌표 오프셋은 그대로 남았다.

따라서 Library 삭제는 캐시된 상태를 제거했을 뿐, 소스상의 좌표 문제를 해결한 것은 아니다. 반복 삭제는 필요 없다.

### 6.3 이전 정상 어댑터 복원

`C:\TeamProjectMYS_qhr`의 2026-07-30 버전 어댑터를 기준으로 복원한 적이 있다.

- 최신 씬 이름은 유지
  - MAIN
  - LOGIN
  - MAP_SELECT
  - MULTIPLAYER
  - BASE_HANGAR
  - RESULT
  - LOADING_SEQUENCE
- 컨트롤러용 `XRRenderTexturePanelInput` 경로를 제거해 마우스 우선 테스트
- 런타임에서 Navigation과 첫 선택을 차단해 Quit 자동 선택을 분리

이 접근만으로는 좌표 오프셋이 완전히 해결되지 않았고, 이후 Game View 로컬 좌표와 Mono 투영 진단으로 넘어갔다.

### 6.4 Game View 로컬 좌표 적용

`OnGUI`의 Game View 로컬 좌표를 읽어 데스크톱/Scene View 좌표와 분리했다.

결과:

- 마우스 원본 좌표가 Game View 내부 크기로 제한됨
- 마우스와 네모 아이콘 방향이 일치함
- Scene View 위쪽까지 올려야 하는 현상의 핵심 원인을 제거함

### 6.5 Mono 패널 영역 적용

`Left Eye` 투영을 제거하고 `Mono` 기준으로 패널 영역을 계산했다.

결과:

- 패널 영역 `0.09, 0.09, 0.82, 0.82`
- 중앙점 `960, 540`
- 버튼별 Raycast 정상

이 단계의 좌표 계산은 성공으로 기록한다.

### 6.6 수동 포인터 처리 실험

기존 `StandaloneInputModule`의 내부 Hover 상태와 직접 진단 Raycast 결과가 다르다고 판단해, 검증된 Raycast를 사용하여 Hover/클릭/드래그 이벤트를 직접 전달하는 실험을 했다.

결과:

- New Game 클릭이 실제 New Game으로 처리됨
- `LOADING_SEQUENCE` 생성 로그 확인
- `BASE_STATION` 진입 확인
- `XRSceneUIAdapter`가 BASE_STATION UI에 적용되는 로그 확인
- 하지만 화면에는 New Game과 Quit이 같이 활성화되어 보임

이 시점이 기능적으로 가장 나았던 체크포인트다. 클릭은 맞았고, 남은 문제는 Quit의 시각 표시였다.

단, 이 수동 포인터 코드는 현재 디스크의 어댑터 파일에는 존재하지 않는다.

### 6.7 LobbyButtonHover 수정 실험

각 버튼에는 기본 `Button` 외에 `LobbyButtonHover`가 붙어 있고 다음 값을 조절한다.

```text
sliderFill.fillAmount
```

진행한 실험:

- Awake에서 fillAmount를 0으로 초기화
- OnDisable에서 초기화
- 진행 중 Coroutine 정지
- PointerUp, PointerExit, Deselect 전송
- `ResetVisualState`, `SetImmediateState` 추가
- 현재 Raycast 버튼만 fillAmount=1로 강제

마지막 강제 방식 이후 사용자 보고:

> 다시 Quit만 선택되게 되었다.

따라서 강제 단일 Hover 방식은 실패로 남긴다.

## 7. 3D 씬 UI 작업

`XRSceneUIAdapter`는 현재 디스크에 다음 변경이 남아 있다.

- `RuntimeInitializeOnLoadMethod` 추가
- MAIN 등 RenderTexture 메뉴 씬은 제외
- 3D 씬의 Screen Space UI를 World Space로 변환
- `TrackedDeviceGraphicRaycaster` 추가
- 기존 `StandaloneInputModule` 비활성화
- `XRUIInputModule` 추가
- XR 입력과 마우스 입력 활성화

BASE_STATION에서 다음 UI 적용 로그가 확인되었다.

```text
PlayerButtonUI
AffinityUI
SettingMenuUI
PauseMenuUI
```

따라서 3D 씬 UI 어댑터가 실행되는 것까지는 확인했다. 실제 VR 화면에서 UI 표시와 입력은 별도로 최종 확인해야 한다.

## 8. 관련 없거나 별도 처리해야 하는 로그

### STAGE2, STAGE3, STAGE4 없음

```text
[GameManager] enum에만 있고 실제 씬은 없음: STAGE2, STAGE3, STAGE4
```

메뉴 마우스 좌표 문제와 직접 관련 없다. 씬을 만들거나 `SCENE_TYPE`에서 제거하는 별도 정리 작업이다.

### Tracked Pose Driver 경고

```text
Camera "Main Camera" does not use a Tracked Pose Driver
```

`XRInteractionModeCoordinator.EnsureMenuRig()`가 만든 메뉴 XR 리그의 카메라 경고다. 마우스 좌표 문제의 유일한 원인은 아니지만, `Left Eye` 투영값이 비정상으로 나온 구조적 원인과 관련될 가능성이 있다.

이 작업에서는 `XRInteractionModeCoordinator.cs`를 수정하지 않았다. 카메라/리그 문제는 메뉴 마우스가 안정된 뒤 별도 단계로 처리해야 한다.

## 9. 수정하지 않기로 한 항목과 사용자 작업 보호

- `XRInteractionModeCoordinator.cs`는 최신화 후 내용만 확인하고 수정하지 않음
- `C:\testTeamProjectMYS`는 확인용이며 수정하지 않음
- 아래 변경은 사용자 또는 Unity/병합 작업일 수 있으므로 임의로 되돌리지 않음
  - `MAIN.unity`
  - `Unit_Player_Photon.prefab`
  - RenderTexture asset
  - OpenXR Settings
  - ProjectSettings
  - Forge3D meta 삭제

현재 `XRMenuSurface` 관련 새 파일도 소유자와 의도를 확인하기 전에는 삭제하거나 덮어쓰면 안 된다.

## 10. 다음 재개 시 권장 순서

### 1단계 — 현재 소스 기준 고정

가장 먼저 다음을 확인한다.

1. 현재 `XRMenuSurface` 방식이 의도된 최신 작업인지 확인
2. 마지막 테스트본의 수동 포인터 코드를 다시 가져올지 결정
3. 두 방식을 동시에 합치지 말고 하나를 기준으로 선택
4. 현재 변경분을 백업 또는 별도 커밋한 뒤 진행

### 2단계 — 마지막 실패 작업 제외

다음은 다시 적용하지 않는다.

- `Left Eye` 패널 투영
- Y축 추가 반전
- `ApplyExclusiveHoverVisual`로 매 프레임 하나의 fill만 강제하는 마지막 방식

`LobbyButtonHover.SetImmediateState`도 마지막 실패와 관련되어 있으므로 유지 여부를 다시 검토한다.

### 3단계 — 기능적으로 가장 나았던 체크포인트 복원

권장 체크포인트:

- Game View 로컬 좌표 사용
- Mono 패널 영역 사용
- 버튼별 Raycast 정확
- New Game 클릭 시 BASE_STATION으로 정상 이동
- Quit은 클릭되지 않음
- 단, New와 Quit이 같이 보이는 시각 문제는 남겨둠

먼저 이 기능 상태를 복원하고, 입력 코드는 더 건드리지 않는다.

### 4단계 — 시각 요소를 화면으로 특정

New와 Quit이 같이 보이는 상태의 최신 스크린샷을 확보한다.

확인할 항목:

- 파란 배경 fill인지
- Button Color Tint인지
- 왼쪽 아이콘인지
- 네모 마우스 커서가 복제되어 보이는지
- 두 Canvas 또는 두 카메라가 같은 UI를 중복 렌더링하는지

이 시각 요소를 특정한 후 해당 Graphic만 수정한다. 좌표와 EventSystem은 이미 정상 Raycast가 확인되었으므로 다시 바꾸지 않는다.

### 5단계 — 다른 씬 확인

MAIN이 고정된 뒤 다음 순서로 확인한다.

1. BASE_HANGAR
2. MAP_SELECT
3. MULTIPLAYER
4. RESULT
5. BASE_STATION
6. 전투 씬 HUD
7. 락온 기능

### 6단계 — 컨트롤러 입력은 마지막

마우스가 완전히 고정된 뒤 컨트롤러 입력을 별도 모드로 추가한다.

- 마우스 EventSystem 경로와 컨트롤러 경로를 동시에 활성화하지 않음
- 컨트롤러 리그의 카메라/높이/Tracked Pose Driver부터 확인
- `XRInteractionModeCoordinator` 수정 전 현재 최신 내용을 다시 비교

## 11. 최종 결론

지금까지의 로그는 메뉴 좌표와 Raycast가 정상화될 수 있음을 보여준다. 특히 New Game 클릭이 실제로 BASE_STATION까지 이어진 테스트가 있었으므로 버튼 기능 자체가 깨진 것은 아니다.

현재 남은 핵심은 다음 두 가지다.

1. 마지막 테스트 코드와 현재 디스크 코드가 서로 다름
2. 좌표가 정상인데도 Quit 활성 표시가 남는 시각 상태 문제

마지막 강제 Hover 작업은 문제를 해결하지 못했고 오히려 Quit만 선택되는 상태로 돌아갔다. 다음 작업에서는 이 방식을 반복하지 말고, 기능적으로 성공했던 체크포인트를 먼저 복원한 뒤 최신 스크린샷으로 실제 남아 있는 Graphic을 특정해야 한다.
