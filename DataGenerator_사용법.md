# DataGenerator 사용 가이드

**담당:** 장지욱
**상태:** WPF 변환 완료 (2026-04-10)
**파일:** `DataGenerator.cs`, `SensorData.cs`

---

## 📌 개요

가상 센서 데이터를 주기적으로 생성하여 교통 상황을 시뮬레이션하는 모듈입니다.
WPF `DispatcherTimer` 기반으로 1초마다 데이터를 갱신하며, 배속(1x/2x/4x) 기능을 지원합니다.

---

## 🚀 빠른 시작

```csharp
// 1. 인스턴스 생성
var generator = new DataGenerator();
generator.Initialize();  // Timer 시작

// 2. 데이터 업데이트 이벤트 구독
generator.SensorDataUpdated += (sensorData) =>
{
    Console.WriteLine($"기온: {sensorData.Temperature}°C");
    Console.WriteLine($"노면상태: {sensorData.RoadCondition}");
    Console.WriteLine($"대기차량(북): {sensorData.WaitingCars_N}대");
};

// 3. 배속 변경
generator.SetSpeed(2);  // 2배속
generator.SetSpeed(4);  // 4배속

// 4. 정지 및 리소스 해제
generator.Stop();
```

---

## SensorData 구조

```csharp
public class SensorData
{
    // 기상 센서
    public double Temperature { get; set; }         // 기온 (°C, -10~35)
    public double Rainfall { get; set; }            // 강수량 (mm/h, 0~20)
    public double WindSpeed { get; set; }           // 풍속 (m/s, 0~20)

    // 노면 센서 (기상에 종속)
    public string RoadCondition { get; set; }       // 건조/습윤/결빙/적설

    // 지자기 센서
    public int WaitingCars_N { get; set; }          // 북쪽 대기차량 (0~15)
    public int WaitingCars_S { get; set; }          // 남쪽 대기차량
    public int WaitingCars_E { get; set; }          // 동쪽 대기차량
    public int WaitingCars_W { get; set; }          // 서쪽 대기차량

    // GPS (지자기에 종속)
    public double AvgSpeed_GPS { get; set; }        // 구간 평균속도 (km/h)

    // 레이더 (독립 생성)
    public double VehicleSpeed { get; set; }        // 차량속도 (0~90 km/h, 연속값)
    public bool IsWrongWay { get; set; }            // 역주행 여부 (2% 확률)
    public bool IsPedestrianRemaining { get; set; } // 잔류보행자 (15% 확률)
}
```

---

## 🔗 센서 종속 관계

### 1. 기상 → 노면 상태

```
기온 + 강수량 → 노면상태
  ├─ 기온 < 0°C + 강수 > 5mm/h → "적설"
  ├─ 기온 < 0°C + 강수 > 0mm/h → "결빙"
  ├─ 강수량 > 0 → "습윤"
  └─ 그 외 → "건조"
```

**예시:**
- 기온 -5°C, 강수 8mm/h → **적설** 🌨️
- 기온 -5°C, 강수 2mm/h → **결빙** ❄️
- 기온 15°C, 강수 2mm/h → **습윤** 💧
- 기온 25°C, 강수 0mm/h → **건조** ☀️

### 2. 지자기 → GPS 속도

```
대기차량수(평균) → GPS 구간 평균속도
  ├─ 대기차량 > 10대 → 5~20 km/h (정체)
  └─ 대기차량 ≤ 10대 → 30~60 km/h (원활)
```

**예시:**
- 평균 대기차량 13대 → GPS 평균속도 **12 km/h** (정체 🔴)
- 평균 대기차량 5대 → GPS 평균속도 **45 km/h** (원활 🟢)

### 3. 레이더 (독립 생성)

```
역주행: 2% 확률로 발생
잔류보행자: 15% 확률로 발생
차량속도: 0~90 km/h 연속값 무작위
```

---

## ⚙️ 주요 메서드

### `Initialize()`
Timer를 초기화하고 데이터 생성을 시작합니다.
재호출 시 무시됩니다(안전한 재진입 가드).

```csharp
generator.Initialize();
```

### `Stop()`
Timer를 정지하고 리소스를 해제합니다.

```csharp
generator.Stop();
```

### `SetSpeed(int speed)`
배속을 변경합니다. (1, 2, 4만 허용)

```csharp
generator.SetSpeed(1);  // 1배속 (1초마다)
generator.SetSpeed(2);  // 2배속 (0.5초마다)
generator.SetSpeed(4);  // 4배속 (0.25초마다)
```

### `SensorDataUpdated` (이벤트)
데이터가 갱신될 때마다 호출됩니다.

```csharp
generator.SensorDataUpdated += (data) =>
{
    // data: SensorData 인스턴스
    Console.WriteLine(data.RoadCondition);
};
```

### `Dispose()`
(선택) IDisposable 구현으로 `using` 문과 호환됩니다.

```csharp
using (var generator = new DataGenerator())
{
    generator.Initialize();
    // ...
}  // 자동 Stop() 호출
```

---

## 🧬 Random 주입 (테스트용)

테스트에서 결정론적 데이터 생성이 필요할 때:

```csharp
// 프로덕션: 랜덤
var generator = new DataGenerator();

// 테스트: 시드 고정
var testRandom = new Random(12345);
var testGenerator = new DataGenerator(testRandom);
testGenerator.Initialize();
// 동일한 시드면 항상 같은 데이터 생성
```


---

## 🧪 테스트 시나리오

### 1. 적설 상황 테스트
```
조건: 기온 -5°C, 강수 8mm/h
기대값: RoadCondition = "적설"
```

### 2. 결빙 상황 테스트
```
조건: 기온 -3°C, 강수 2mm/h
기대값: RoadCondition = "결빙"
```

### 3. 혼잡 시간대 테스트
```
조건: 08:30 (출근시간)
기대값: WaitingCars_N 5~15대, AvgSpeed_GPS 5~20 km/h
```

### 4. 역주행 발생 테스트
```
조건: 100번 실행
기대값: IsWrongWay = true가 약 2회 발생
```

---

## 📝 주의사항

1. **Timer 리소스 관리**: 사용 후 반드시 `Stop()` 호출 또는 `using` 문 사용
2. **배속 값**: 1, 2, 4만 허용 (다른 값 입력 시 `ArgumentException` 발생)
3. **이벤트 구독**: `Initialize()` 전 또는 후 모두 가능
4. **종속 관계**: 노면상태와 GPS 속도는 다른 센서 값에 의존함
5. **WPF Dispatcher**: DispatcherTimer는 WPF UI 스레드에서 실행됨

---

## 🐛 트러블슈팅

### Q. 데이터가 갱신되지 않아요
**A.** `Initialize()` 메서드를 호출했는지 확인하세요.

### Q. 배속 변경이 안 돼요
**A.** `SetSpeed(1)`, `SetSpeed(2)`, `SetSpeed(4)`만 사용 가능합니다.

### Q. 기온 35°C인데 적설이 나와요
**A.** 적설 조건: 기온 < 0°C AND 강수 > 5mm/h입니다. 일반적으로 발생하지 않습니다.

### Q. 이벤트 핸들러 예외가 Timer를 멈춰요
**A.** v1.1부터는 내부 try-catch로 구독자 예외를 격리하므로 Timer가 중단되지 않습니다.

---

## 📅 버전 이력

- **v1.1** (2026-04-10)
  - WPF DispatcherTimer로 전환
  - IDisposable 구현 추가
  - Random 생성자 주입 지원 (테스트 용이성 개선)
  - 적설 판정 우선순위 버그 수정
  - 이벤트명 OnSensorDataUpdated → SensorDataUpdated 변경
  - 매직 넘버 상수화
  - 이벤트 핸들러 예외 격리 추가

- **v1.0** (2026-04-03)
  - 초기 구현 완료
  - Timer 기반 주기적 갱신
  - 센서 종속 관계 구현
  - 배속 기능 (1x/2x/4x)
