# UI & 물리 엔진 사용법 및 개선사항 정리

**담당:** 송성조

**최종 업데이트:** 2026-05-21

**대상 파일:** `MainWindow.xaml`, `MainWindow.xaml.cs`

---

## 1. 사용법

다른 모듈(혼잡도 분석, 기상 제어 등)에서 UI 인스턴스에 접근하여 아래 메서드를 호출하면 차량 속도 및 기타 상황을 제어 가능합니다.

### 신호 및 속도 제어

```csharp
// 혼잡도 점수에 따라 차량 통과 속도 조정 (기본값: 1.0)
mainWindow.ApplyTrafficPolicy(double speedMultiplier);
```

### 긴급 상황 강제 발동

```csharp
// 호출 즉시 3초간 전 방향 빨간불 및 차량 강제 정지
mainWindow.TriggerEmergencyStop();
```

### 환경 모드 강제 설정

```csharp
// UI를 블랙아이스 모드로 전환
mainWindow.SetBlackIceMode(bool isActive);
```

---

## 2. 주요 작동 원리

- **배속 및 랙 방어 통합 (PassTime 구조):** UI(`MainWindow`)가 `DateTime.Now`를 활용하여 정밀한 현실 경과 시간(`realDeltaTime`)을 계산한 뒤, 설정된 배속을 곱해 가상 경과 시간(`simulatedDeltaTime`)을 도출한다. 이를 `TrafficController.PassTime(deltaTime)`으로 전달함으로써, `DateTime`를 사용한 구조와 함께 무한 대기 버그를 차단한다.
- **참조 공유 및 데이터 오염 방지 스냅샷:** UI 초시계가 30FPS로 실시간 차감되도록 `TrafficController`로부터 신호등 객체의 원본 참조를 받아서 렌더링한다. 반면, 과거 데이터 무결성을 지키기 위해 `DataManager.RecordData` 시점에서 독립된 복사본(`.Clone()`)을 생성하여 적재함해서, UI 최적화와 CSV 저장 데이터 오염 문제를 동시에 해결한다.
- **예측 기반 정지 (nextX, nextY):** 차를 무작정 이동시키는게 아니라, 다음 프레임의 위치를 미리 계산함. 그 위치에 정지선이나 앞차가 있으면 이동을 취소한다.
- **오브젝트 풀링:** `MAX_CARS(40대)` 제한이 있다. 화면에 차가 너무 많아지면 소환이 무시된다.
- **렌더링 최적화 및 GC 부하 개선:** 매 프레임마다 리스트를 생성하지 않고, 각 방향별 고정된 크기의 리스트를 `.Clear()`시켜서 재활용한다.

---

## 3. 시뮬레이션 변수 제어 기준

변수를 직접 수정해야 할 경우 참고사항

| 변수명                | 기본값 | 설명                                                                                        |
| --------------------- | ------ | ------------------------------------------------------------------------------------------- |
| `STOP_LINE_N/W`       | 170    | 북/서쪽 정지선 X/Y 좌표                                                                     |
| `STOP_LINE_S/E`       | 310    | 남/동쪽 정지선 X/Y 좌표                                                                     |
| `CAR_FOLLOW_DISTANCE` | 25     | 차량 간 최소 유지 안전 거리 (픽셀)                                                          |
| `_baseMoveSpeed`      | 2.0    | 이 값을 너무 높이면(5.0 이상) 차량이 정지선을 뚫고 지나가는 '터널링' 현상이 발생할 수 있음. |
| `visualTarget`        | 8      | 화면 가독성을 위해 한 차선당 최대 8대까지만 그림. (내부 로직은 모든 차량 연산 중)           |

---

## 4. 개선사항

## 수동 강제 이벤트(BlackIce, WrongWay) 발동이 계산 로직과 동기화되지 않음

**어떤 문제인지**
UI에서 블랙아이스 강제실행 버튼 또는 역주행 시뮬레이션 버튼을 눌러도, UI상에서만 시각적으로 표시될 뿐 실제 안전위험도 및 신호 계산에 아무런 영향이 없음

**왜 문제인가?**
사용자에 의해 강제로 상황발생을 시킬 때, UI에는 강제적인 상황이 적용되지만 `TrafficController`는 이 사실을 모르고 있다. 컨트롤러는 블랙아이스가 강제로 발생되더라도 계속 맑은 날씨라고 생각해서 100점을 부여하는 문제가 있다.

**어디 부분인지**

- 파일: `MainWindow.xaml.cs` 라인 89-94 (강제 이벤트 메서드), 라인 369-378 (초기화 메서드)

**개선을 어떻게 하면 좋을지**
`TrafficController` 객체에게 강제상태를 표현하는 코드를 추가했다. 강제상황에 의한 타이머가 끝나거나, 시뮬레이션 하루가 끝나면 다시 강제상태를 해제하도록 설정했다.

```csharp
public void TriggerEmergencyStop()
{
    if (_emergencyTimer == 0)
    {
        _emergencyTimer = 90;
        if (_trafficController != null) _trafficController.ForceWrongWay = true; // 컨트롤러에 통보
        UpdateUI();
    }
}

public void SetBlackIceMode(bool isActive)
{
    _forceBlackIce = isActive;
    if (_trafficController != null) _trafficController.ForceBlackIce = isActive; // 컨트롤러에 통보
    UpdateUI();
}

private void ResetRoadState()
{
    // ... 기존 코드 ...

    // Controller에 있는 강제상황 flag를 다시 초기화
    if (_trafficController != null)
    {
        _trafficController.ForceBlackIce = false;
        _trafficController.ForceWrongWay = false;
    }
    // ... 이하 생략 ...
}
```

## 5. 주의사항

### UI 스레드 접근

`DataGenerator` 등 외부 스레드에서 UI 메서드(API)를 호출할 때는 반드시 **Dispatcher**를 통해야 함. 내부처리를 해놨지만 추가 구현사항이 있을 때 주의

### 긴급 정지 버튼 쿨다운

`TriggerEmergencyStop` 버튼을 누르면 3초(90프레임) 동안 시스템이 잠김. 시간이 다 지날 때까지 재입력이 불가능하니까 주의

### 차량 겹침 생성 방지

진입로에 이미 차가 있으면 센서 데이터에 대기 차량이 있어도 새로 소환하지 않음. 차들이 겹쳐서 유령처럼 달리는 것을 방지하기 위한 제약 사항임.

---

## 6. 버전 이력

- **v2.1 (2026-05-21)**
  - : `MainWindow`와 `TrafficController` 간의 `DateTime` 연산 충돌(시간 이중 차감) 및 무한 대기 무한루프 현상을 해결하기 위해 `PassTime` 동기화 구조 추가.
  - 신호등 참조 공유로 인해 과거 24시간 저장 데이터가 전부 최종값으로 덮어씌워지던 데이터 무결성 문제를 해결하기 위해, `DataManager.RecordData` 시점에서의 독립된 `.Clone()` 적재 로직 제안.
  - '블랙아이스 강제 실행' 및 '강제 역주행 시뮬레이션' 버튼 클릭 시, `TrafficController`에 즉각적으로 플래그를 전달하도록 연동해서, 점수 및 신호 연장 제어가 정상적으로 작동하도록 개선.
  - 시뮬레이션 극초반 데이터가 부족할 때(데이터 개수 1개 이하) 발생할 수 있는 그래프 연산 에러 방어 코드 추가. (0/0 상황 방지)

- **v2.0 (2026-05-20):**
  - UI가 스스로 신호를 계산하는 문제를 개선. `TrafficController`의 신호 이벤트를 수신하여 동기화시킴.
  - O(N²) 시간으로 동작하는 충돌 감지 로직을 방향별 리스트 그룹화를 통해 O(N)으로 성능 개선함.
  - 프레임마다 불필요한 List 객체 생성 없이, 리스트를 재사용하도록 수정해 GC(가비지 컬렉터) 부하를 줄임.
  - 다양한 예외 처리 상황을 추가함. 시뮬레이션 완료 및 데이터 저장(`DataManager`) 프로세스에 `try-catch` 및 사용자 알림 로직 추가함.
  - `Window_Closing` 시 타이머 종료 및 이벤트 핸들러 해제를 통해 메모리 누수를 방지함.

- **v1.0 (2026-05-15):**
  - 신호등 체계 구현
  - 긴급 상황 카운트다운 게이지 UI 추가
  - 배속 상태 버튼 시각화(선택 버튼 초록색 강조) 추가
