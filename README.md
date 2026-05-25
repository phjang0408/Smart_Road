# 🚦 도심형 스마트 교차로 교통 관제 시스템
> [cite_start]**부제:** 가상 IoT 데이터 기반 신호 제어 시뮬레이션 [cite: 128, 129]

[cite_start]본 프로젝트는 도로위험 기상정보 시스템(RWIS)의 환경 센서 기반 위험 감지 원리를 도심 교차로 신호 제어에 적용하여, 기상 악화 및 돌발 상황 발생 시 교통 신호의 동적 제어를 검증하는 C# WPF 기반 시뮬레이터입니다[cite: 172, 193]. [cite_start]Traffic Efficiency Score와 Road Safety Score를 복합 산출하는 이중 점수 체계를 구축하고, 안전을 최우선으로 삼는 'Safety-First' 설계 원칙을 구현하였습니다[cite: 174, 179, 223].

---

## 🛠️ 개발 환경 및 기술 스택
- [cite_start]**개발 언어:** C# (.NET 9.0) [cite: 193]
- [cite_start]**개발 환경:** Visual Studio 2022 [cite: 193]
- [cite_start]**UI 프레임워크:** WPF (Windows Presentation Foundation) [cite: 193]
- [cite_start]**렌더링 엔진:** WPF Canvas 기반 실시간 렌더링 (30 FPS) [cite: 193]
- [cite_start]**데이터 저장:** Thread-Safe 구조 기반 CSV 자동 저장 [cite: 193, 312]

---

## 🌟 주요 기능 (Core Features)

### [cite_start]1. 현실적인 가상 IoT 데이터 생성 (`DataGenerator.cs`) [cite: 202]
- [cite_start]**기상 모사:** 코사인 함수를 기반으로 실시간 기온 변동을 계산하여 새벽의 최저기온과 오후의 최고기온 등 현실적인 일주기 변화를 구현합니다[cite: 206].
- [cite_start]**센서 간 종속 관계:** 기온과 강수량 데이터를 조합하여 노면 상태(건조, 습윤, 결빙, 적설)를 자동으로 결정합니다[cite: 209].
- [cite_start]**교통량 제어:** 출퇴근 혼잡 시간대(08~09시, 18~19시) 가중치를 반영하여 대기 차량 수와 GPS 구간 평균 속도를 유기적으로 변동시킵니다[cite: 215].

### [cite_start]2. 이중 점수 산출 및 4단계 상태 판단 (`TrafficController.cs`) [cite: 179, 180, 202]
- [cite_start]**Traffic Efficiency Score:** 방향별 대기 차량 수와 GPS 구간 속도를 종합하여 교차로 흐름의 원활함을 100점 만점으로 평가합니다[cite: 219, 220].
- [cite_start]**Road Safety Score:** 결빙·적설(+40점), 잔류 보행자(+30점), 역주행 감지(+100점) 등 도로 위험 요소를 수치화합니다[cite: 221, 222].
- [cite_start]**4단계 상태 전이:** 계산된 복합 점수를 기준으로 교차로를 **정상(Normal) / 혼잡(Congested) / 환경위험(EnvironmentalHazard) / 긴급(Emergency)** 상태로 분류합니다[cite: 180, 226].

### [cite_start]3. 규칙 기반(Rule-based) 동적 신호 제어 [cite: 188]
- [cite_start]**상태별 변동 주기:** 혼잡 상태 시 녹색 신호 연장(+15초), 블랙아이스 감지 시 황색 신호 연장(+5초), 보행자 잔류 시 보행 신호 연장(+10초)을 수행합니다[cite: 227].
- [cite_start]**최우선 긴급 제어:** 역주행 감지 시 즉시 전 방향 적색 신호(`SetAllRed`)를 발동하여 인명 사고를 예방하는 Safety-First 아키텍처를 보장합니다[cite: 224, 225, 229].
- [cite_start]**타이머 동기화:** 시스템 시계(`DateTime.Now`) 기반의 절대 경과 시간 추적 방식을 도입하여 시뮬레이션 배속(1x/2x/4x) 변경 시에도 신호 주기가 완벽하게 동기화됩니다[cite: 308, 326, 327].

### [cite_start]4. 실시간 관제 대시보드 시각화 (`MainWindow.xaml`) [cite: 147]
- [cite_start]500×500 논리 좌표 기반 Canvas 내에서 차량 레이더 애니메이션 및 4방향 신호등 상태를 실시간 시각화합니다[cite: 241, 253, 255].
- [cite_start]기온 및 강수량 추이를 밝은 파란색과 초록색 선으로 표현하는 반응형 그래프 섹션을 포함합니다[cite: 238, 239].
- [cite_start]블랙아이스 오버레이 이펙트 및 역주행 경고 인디케이터를 통해 가시적인 경고 시스템을 제공합니다[cite: 261, 264, 265].

### [cite_start]5. 스레드 안전 데이터 로깅 시스템 (`DataManager.cs`) [cite: 202]
- [cite_start]`lock` 패턴을 적용하여 UI 및 타이머 스레드의 동시 접근 시 발생할 수 있는 경쟁 조건을 차단하고 무결성을 확보합니다[cite: 326, 333].
- [cite_start]신호등 객체의 `Clone()` 스냅샷 복사 패턴을 활용해 과거 기록 데이터가 현재 상태로 오염되는 문제를 근본적으로 해결하였습니다[cite: 317, 326].

---

## 🏗️ 시스템 아키텍처 (Architecture)
[cite_start]본 시스템은 컴포넌트 간 결합도를 낮추기 위해 **이벤트 기반 아키텍처(Event-Driven Architecture)**로 설계되었습니다[cite: 199].

- [cite_start]`DataGenerator` (데이터 생성 엔진) ➔ `SensorDataUpdated` 이벤트 발행 [cite: 199]
- [cite_start]`TrafficController` (제어/판단) ➔ 데이터 수신 후 복합 점수 산출 및 신호 위상 제어 [cite: 199, 200]
- [cite_start]`MainWindow` / `DataManager` ➔ `TrafficUpdated` 이벤트를 구독하여 각각 실시간 렌더링 및 CSV 기록 수행 [cite: 200]

---

## 👥 팀 구성 및 역할 분담

| 학번 | 이름 | 담당 모듈 | 세부 업무 내용 |
|------|------|------|------|
| 20011669 | **장지욱** | 데이터 생성 모듈 | [cite_start]기상·노면·레이더·지자기 가상 데이터 시뮬레이터 개발 및 센서 종속성 구현 [cite: 191] |
| 21011220 | **최민석** | 점수 계산 및 신호 제어 | [cite_start]복합 점수 산출 알고리즘 설계, 4단계 상태 전이 및 동적 신호 제어 핵심 로직 구현 [cite: 191] |
| 22010542 | **송성조** | UI / 시각화 | [cite_start]교차로 다이어그램, 차량 애니메이션 루프, 실시간 추이 그래프 및 대시보드 UI 개발 [cite: 191] |
| 23013349 | **손강표** | [cite_start]테스트 / 데이터 저장 | lock 기반 스레드 안전 데이터 적재 구조 설계, 모드 검증 테스트 및 CSV 내보내기 구현 [cite: 191] |

---

## 🚀 실행 및 테스트 방법
1. 본 레포지토리를 클론합니다.
   ```bash
   git clone [https://github.com/phjang0408/Smart_Road.git](https://github.com/phjang0408/Smart_Road.git)
