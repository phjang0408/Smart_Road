# DataGenerator 사용 가이드

**담당:** 장지욱  
**상태:** 완료 (2026-04-03)  
**파일:** `DataGenerator.cs`, `SensorData.cs`

---

## 📌 개요

가상 센서 데이터를 주기적으로 생성하여 교통 상황을 시뮬레이션하는 모듈입니다.  
Timer 기반으로 1초마다 데이터를 갱신하며, 배속(1x/2x/4x) 기능을 지원합니다.

---

## 🚀 빠른 시작

```csharp
// 1. 인스턴스 생성 및 시작
var generator = new DataGenerator(speed: 1);  // 1x 배속
generator.Start();

// 2. 데이터 업데이트 이벤트 구독
generator.OnSensorDataUpdated += (sensorData) => 
{
    Console.WriteLine($"기온: {sensorData.Temperature}°C");
    Console.WriteLine($"노면상태: {sensorData.RoadCondition}");
    Console.WriteLine($"대기차량(북): {sensorData.WaitingCars_N}대");
};

// 3. 배속 변경
generator.SetSpeed(2);  // 2배속
generator.SetSpeed(4);  // 4배속

// 4. 정지
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
    public double VehicleSpeed { get; set; }        // 차량속도 (0~90 km/h)
    public bool IsWrongWay { get; set; }            // 역주행 여부 (2% 확률)
    public bool IsPedestrianRemaining { get; set; } // 잔류보행자 (15% 확률)
}
```

---

## 🔗 센서 종속 관계

### 1. 기상 → 노면 상태

```
기온 + 강수량 → 노면상태
  ├─ 기온 < 0°C + 강수 있음 → "결빙"
  ├─ 기온 < 2°C + 강수 > 5mm/h → "적설"
  ├─ 강수량 > 0 → "습윤"
  └─ 그 외 → "건조"
```

**예시:**
- 기온 -5°C, 강수 3mm/h → **결빙** ❄️
- 기온 1°C, 강수 8mm/h → **적설** 🌨️
- 기온 15°C, 강수 2mm/h → **습윤** 💧
- 기온 25°C, 강수 0mm/h → **건조** ☀️

### 2. 지자기 → GPS 속도

```
대기차량수 → GPS 구간 평균속도
  ├─ 대기차량 > 10대 → 5~20 km/h (정체)
  └─ 대기차량 ≤ 10대 → 30~60 km/h (원활)
```

**예시:**
- 북쪽 대기차량 13대 → GPS 평균속도 **12 km/h** (정체 🔴)
- 북쪽 대기차량 5대 → GPS 평균속도 **45 km/h** (원활 🟢)

### 3. 레이더 (독립 생성)

```
역주행: 2% 확률로 발생
잔류보행자: 15% 확률로 발생
차량속도: 0~90 km/h 무작위 (제한속도 50km/h 기준)
```

---

## ⚙️ 주요 메서드

### `Start()`
Timer를 시작하고 데이터 갱신을 시작합니다.

```csharp
generator.Start();
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

### `OnSensorDataUpdated` (이벤트)
데이터가 갱신될 때마다 호출됩니다.

```csharp
generator.OnSensorDataUpdated += (data) => 
{
    // data: SensorData 인스턴스
};
```

---

## 🔌 다른 모듈과 연결

### ScoreLogic과 통합 (최민석 모듈)

```csharp
var dataGen = new DataGenerator();
var scoreLogic = new ScoreLogic();  // 최민석 모듈

dataGen.OnSensorDataUpdated += (sensorData) => 
{
    // 1. SensorData를 ScoreLogic에 전달
    var systemState = scoreLogic.Calculate(sensorData);
    
    // 2. 결과 출력
    Console.WriteLine($"교통 점수: {systemState.TrafficScore}");
    Console.WriteLine($"안전 점수: {systemState.SafetyScore}");
    Console.WriteLine($"상태: {systemState.Status}");
    Console.WriteLine($"북쪽 신호: {systemState.SignalColor_N}");
};

dataGen.Start();
```

### UI와 통합 (송성조 모듈)

```csharp
var dataGen = new DataGenerator();

dataGen.OnSensorDataUpdated += (sensorData) => 
{
    // UI 업데이트
    labelTemperature.Text = $"{sensorData.Temperature:F1}°C";
    labelRoadCondition.Text = sensorData.RoadCondition;
    labelWaitingCars.Text = $"{sensorData.WaitingCars_N}대";
    
    // 노면 상태에 따라 색상 변경
    if (sensorData.RoadCondition == "결빙")
        panelRoad.BackColor = Color.LightBlue;
    else if (sensorData.RoadCondition == "건조")
        panelRoad.BackColor = Color.Gray;
};

dataGen.Start();
```

---

## 🧪 테스트 시나리오

### 1. 결빙 상황 테스트
```
조건: 기온 -3°C, 강수 5mm/h
기대값: RoadCondition = "결빙"
```

### 2. 혼잡 시간대 테스트
```
조건: 08:30 (출근시간)
기대값: WaitingCars_N 5~15대, AvgSpeed_GPS 5~20 km/h
```

### 3. 역주행 발생 테스트
```
조건: 100번 실행
기대값: IsWrongWay = true가 약 2회 발생
```

---

## 📝 주의사항

1. **Timer 리소스 관리**: 사용 후 반드시 `Stop()` 호출
2. **배속 값**: 1, 2, 4만 허용 (다른 값 입력 시 예외 가능)
3. **이벤트 구독**: `Start()` 전에 `OnSensorDataUpdated` 구독 권장
4. **종속 관계**: 노면상태와 GPS 속도는 다른 센서 값에 의존함

---

## 🐛 트러블슈팅

### Q. 데이터가 갱신되지 않아요
**A.** `Start()` 메서드를 호출했는지 확인하세요.

### Q. 배속 변경이 안 돼요
**A.** `SetSpeed(1)`, `SetSpeed(2)`, `SetSpeed(4)`만 사용 가능합니다.

### Q. 기온 35°C인데 결빙이 나와요
**A.** 센서 종속 관계가 적용되었다면 발생하지 않습니다. DataGenerator.cs의 `GetRoadCondition()` 메서드를 확인하세요.

---

## 📅 버전 이력

- **v1.0** (2026-04-03)
  - 초기 구현 완료
  - Timer 기반 주기적 갱신
  - 센서 종속 관계 구현
  - 배속 기능 (1x/2x/4x)
