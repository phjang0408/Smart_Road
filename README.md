프로그램 설명 문서

스마트 교차로 신호 제어 시스템
도시형 교차로의 교통 흐름을 최적화하는 환경 센서 기반 신호 제어 시뮬레이션


프로그램 개요
============

이 프로그램은 스마트 시티 기술을 활용하여 교차로의 신호등을 자동으로 제어하는 시스템을 시뮬레이션합니다.
기상 데이터(기온, 강수량), 도로 상태, 교통량, 안전 요소(역주행, 보행자)를 종합적으로 분석하여
교통 효율성과 도로 안전성을 동시에 고려한 최적의 신호 제어를 구현합니다.


핵심 기능
========

1. 센서 데이터 시뮬레이션
   - 기상 센서: 기온, 강수량, 풍속
   - 지자기 센서: 방향별 대기 차량 수 (4개 방향)
   - GPS 센서: 평균 주행 속도
   - 레이더 센서: 역주행 감지, 보행자 감지

2. 도로 상태 판단
   - 기온 + 강수량 기반 노면 상태 결정
   - 건조, 습윤, 결빙, 적설 4가지 상태 자동 판단

3. 신호 제어 로직
   - 교통 효율성 점수 계산 (대기 차량, 평균 속도 기반)
   - 도로 안전성 점수 계산 (노면상태, 보행자, 역주행 기반)
   - 4가지 상태에 따른 신호 제어
     * 정상: 표준 신호 주기
     * 혼잡: 신호 시간 단축
     * 환경위험: 신호 시간 연장 (안전 확보)
     * 긴급: 모든 신호 정지 (역주행 감지)

4. 실시간 시각화
   - 교차로 다이어그램 및 신호등 표시
   - 차량 흐름 애니메이션
   - 기상 데이터 그래프 (기온, 강수량)
   - 실시간 점수 및 상태 표시

5. 데이터 저장
   - CSV 형식으로 시뮬레이션 결과 저장
   - 모든 신호 변화 이력 기록
   - 센서 데이터와 신호 상태를 시간순 저장


시스템 아키텍처
==============

프로그램은 4개의 핵심 모듈로 구성되며, 데이터는 단방향으로 흐릅니다.

SensorData (데이터 구조)
- Temperature: 기온 (-10 ~ 40도)
- Rainfall: 강수량 (0 ~ 50mm/h)
- WindSpeed: 풍속 (0 ~ 15m/s)
- RoadCondition: 노면 상태
- WaitingCars_N/S/E/W: 방향별 대기 차량
- VehicleSpeed: 차량 진입 속도
- IsWrongWay: 역주행 여부
- IsPedestrianRemaining: 보행자 유무
- AvgSpeed_GPS: 평균 주행 속도


모듈 1: DataGenerator (센서 데이터 생성)
========================================

역할
센서 데이터를 시뮬레이션하여 생성하는 모듈입니다.
24시간의 일일 시뮬레이션을 5분 단위로 진행하며, 각 센서 간의 현실적인 종속 관계를 구현합니다.

주요 특징

배속 지원
- 1배: 정상 속도
- 2배: 2배 빠른 속도
- 4배: 4배 빠른 속도
타이머 기반 주기적 갱신으로 부드러운 진행 가능

센서 데이터 종속성 구현

기상 → 노면상태
기온과 강수량에 따라 노면 상태가 자동으로 결정됩니다.
- 영하 + 다량강수: 적설
- 영하: 결빙
- 영상 + 강수: 습윤
- 그 외: 건조

지자기 → GPS 속도
방향별 대기 차량 수를 기반으로 평균 주행 속도를 계산합니다.
- 대기 차량 > 10대: 정체 상태 (5-20 km/h)
- 대기 차량 <= 10대: 원활 상태 (30-60 km/h)

혼잡 시간대 가중치
8-9시, 18-19시에 높은 차량 대기 수를 생성하여 현실적인 교통 패턴 구현

일일 기온 변화
코사인 곡선을 사용하여 일교차를 자연스럽게 표현합니다.
최저기온: 새벽 2-4시경
최고기온: 오후 14시경

강수 패턴
하루 날씨를 처음에 결정 (30% 확률로 비 오는 날)
비 오는 날은 70% 확률로 강수를 생성

독립 센서 (레이더)
- 역주행: 2% 확률로 감지
- 보행자: 15% 확률로 감지
- 차량 진입 속도: 0-90 km/h 범위에서 랜덤 생성


모듈 2: TrafficController (신호 제어)
======================================

역할
센서 데이터를 받아 교통 효율성과 도로 안전성을 평가하고,
이에 따라 교차로의 신호등 색상과 시간을 제어합니다.

점수 계산

교통 효율성 점수 (0-100)
- 기본값: 100
- 대기 차량당 2점 감점
- 평균 속도가 시속 50km 이하면 추가 감점
- 점수가 높을수록 교통 상태가 좋음

도로 안전성 점수 (0-170)
- 기본값: 0
- 습윤 도로: 10점
- 결빙/적설: 40점
- 보행자 감지: 30점
- 역주행 감지: 100점
- 점수가 높을수록 위험 상태

상태 판단

정상 상태
- 효율성 점수 >= 70 AND 안전성 점수 < 50
- 표준 신호 주기 (녹색 30초)

혼잡 상태
- 효율성 점수 < 70
- 신호 주기 단축 (녹색 20초)
- 차량 흐름 빨리하기 위함

환경위험 상태
- 안전성 점수 >= 50 AND 안전성 점수 < 100
- 신호 주기 연장 (녹색 40초)
- 보행자 안전 확보, 운전자 대응 시간 제공

긴급 상태
- 역주행 감지 OR 안전성 점수 >= 100
- 모든 신호등 빨간색 (차량 전체 정지)
- 긴급 상황 해제될 때까지 유지

신호 제어 알고리즘

위상 관리
- 남북(N-S) 녹색 및 동서(E-W) 빨간색
- 시간 경과 후 위상 전환
- 노란색 신호 3초 자동 삽입

보행자 신호 연장
- 보행자 감지 시 현재 녹색을 추가 10초 연장
- 보행자 안전 확보

배속 연동
시뮬레이션 배속이 변경되면 진행 중인 신호 시간도 함께 조정


모듈 3: MainWindow (사용자 인터페이스)
========================================

역할
시뮬레이션을 시각화하고 사용자 입력을 처리하는 UI 모듈입니다.
교차로 상황을 실시간으로 표시하고 제어 기능을 제공합니다.

화면 구성

교차로 다이어그램
- 4개 방향(북, 남, 동, 서) 표시
- 신호등 색상 연동 (빨강, 노랑, 초록)
- 차량 흐름 애니메이션

정보 패널
- 현재 상태 (정상, 혼잡, 환경위험, 긴급)
- 효율성 점수와 안전성 점수
- 현재 기온, 강수량, 노면 상태
- 각 방향의 대기 차량 수

그래프 표시
- 기온 변화 그래프 (실시간 갱신)
- 강수량 변화 그래프

제어 버튼

시작/정지
- 시뮬레이션 시작 및 중지
- 대기 중인 경우에만 활성화

배속 설정
- 1배, 2배, 4배 선택 가능
- 모듈 간 자동 연동

혼잡 시간 토글
- 8-9시, 18-19시 교통량 변화 유무 설정

강제 조건 설정
- 역주행 강제 활성화
- 블랙아이스 강제 활성화 (결빙 도로)

데이터 저장
- CSV 파일로 시뮬레이션 결과 저장

차량 렌더링

객체 풀링 기법
- 미리 생성한 차량 객체 40개를 재활용
- GC(가비지 컬렉션) 부담 최소화

차량 생성 규칙
- 방향별 대기 차량 수에 따라 생성
- 정지선에서 일정 거리(25px)에서 대기
- 신호 변화에 따라 주행 또는 정지

애니메이션
- 배속에 따라 차량 속도 자동 조정
- 매 프레임 위치 업데이트


모듈 4: DataManager (데이터 저장)
==================================

역할
시뮬레이션 중 발생하는 모든 신호 제어 이벤트와 센서 데이터를 기록하고
CSV 파일로 내보내는 기능을 제공합니다.

주요 기능

메모리 기록
- 모든 신호 변화와 센서 데이터를 메모리에 누적
- 스레드 안전성 확보 (Lock 사용)
- 각 이벤트 스냅샷 저장 (원본 데이터 변경 방지)

CSV 내보내기
- 지정된 경로에 CSV 파일 생성
- 다음 항목 저장:
  * 타임스탬프
  * 신호 상태 (정상, 혼잡, 환경위험, 긴급)
  * 효율성 점수, 안전성 점수
  * 기온, 강수량, 풍속, 노면 상태
  * 대기 차량 수, 차량 속도, GPS 속도
  * 역주행, 보행자, 신호등 상태

이용 사례
- 시뮬레이션 결과 분석
- 신호 제어 알고리즘 성능 평가
- 교통 패턴 통계 분석


데이터 흐름
===========

1. DataGenerator
   - 센서 데이터 생성 및 SensorDataUpdated 이벤트 발행

2. TrafficController
   - SensorDataUpdated 이벤트 수신
   - 효율성 점수, 안전성 점수 계산
   - 상태 판단 및 신호 제어
   - TrafficUpdated 이벤트 발행

3. MainWindow
   - TrafficUpdated 이벤트 수신
   - UI 업데이트 (신호등 색상, 점수, 그래프 등)
   - 차량 애니메이션 렌더링

4. DataManager
   - TrafficUpdated 이벤트 구독
   - 데이터 기록 및 CSV 저장


사용 흐름
========

1. 프로그램 시작
   - 초기 화면 표시 (신호등 초기화)

2. 설정 구성
   - 배속 선택 (1배, 2배, 4배)
   - 혼잡 시간 토글 설정
   - 강제 조건 활성화 여부 결정

3. 시뮬레이션 시작
   - 시작 버튼 클릭
   - DataGenerator 타이머 시작
   - 센서 데이터 5분마다 생성 시작
   - 신호 제어 및 UI 업데이트 시작

4. 실시간 모니터링
   - 교차로 상황 시각화
   - 현재 상태와 점수 확인
   - 그래프로 기온, 강수량 추이 확인

5. 시뮬레이션 종료
   - 24시간 완료 시 자동 정지
   - 또는 정지 버튼으로 수동 정지

6. 결과 저장
   - CSV 저장 버튼 클릭
   - 파일 경로 지정
   - 시뮬레이션 결과 저장 완료


주요 설정값
===========

신호 시간 (초)
- 기본 녹색 시간: 30초
- 기본 노란색 시간: 3초
- 혼잡 상태 녹색: 20초
- 환경위험 상태 녹색: 40초
- 보행자 신호 연장: 10초

점수 계산
- 효율성 기본값: 100
- 대기 차량당 감점: 2점
- 안전성 기본값: 0
- 습윤 도로 가산: 10점
- 결빙/적설 가산: 40점
- 보행자 가산: 30점
- 역주행 가산: 100점

센서 범위
- 기온: -10 ~ 40도
- 강수량: 0 ~ 50mm/h
- 풍속: 0 ~ 20m/s
- 차량 속도: 0 ~ 90km/h
- 대기 차량 (통상): 0 ~ 8대
- 대기 차량 (혼잡): 5 ~ 15대

타이머 간격 (배속별, ms)
- 1배: 2000ms
- 2배: 1000ms
- 4배: 500ms

혼잡 시간
- 오전: 8시 ~ 9시
- 저녁: 18시 ~ 19시


기술 특징
========

신센서 종속성 구현
각 센서가 독립적이지 않으며 현실적인 관계를 표현합니다.
예: 기온이 낮으면 자동으로 도로 상태가 위험하게 변함

배속 동기화
시뮬레이션의 모든 모듈이 배속 변경에 자동으로 반응합니다.
진행 중인 신호 시간도 함께 조정됩니다.

메모리 최적화
차량 객체 풀링으로 GC 부담을 최소화하여 프레임 안정성 확보

스레드 안전성
DataManager는 Lock을 사용하여 멀티스레드 환경에서도 안전합니다.

예외 처리
모든 이벤트 핸들러에 try-catch를 적용하여 한 모듈의 오류가
다른 모듈에 영향을 주지 않도록 격리합니다.


신호 제어의 상세 단계별 로직
============================

센서 데이터 수신
- DataGenerator가 SensorDataUpdated 이벤트 발행
- TrafficController가 OnSensorDataUpdated 핸들러에서 이를 수신

점수 계산 단계 1: 교통 효율성 점수

기본 공식:
EfficiencyScore = 100 - (대기차량합계 x 2) - (시속50km 미만시 감점)

상세 계산:
1) 4개 방향의 대기 차량을 모두 합산
   totalWaitingCars = N + S + E + W

2) 대기 차량당 2점 감점
   score -= totalWaitingCars x 2

3) 평균 주행 속도(AvgSpeed_GPS)가 50km/h 미만이면 추가 감점
   if (AvgSpeed_GPS < 50)
      score -= (50 - AvgSpeed_GPS)

4) 최종 점수는 0 이상으로 클램프
   score = max(0, score)

예시:
- 모든 방향 대기 차량 0대, GPS 속도 60km/h: 점수 100 (최상)
- 총 대기 차량 6대, GPS 속도 30km/h: 점수 = 100 - 12 - 20 = 68
- 총 대기 차량 15대, GPS 속도 10km/h: 점수 = 100 - 30 - 40 = 30

점수 계산 단계 2: 도로 안전성 점수

기본 공식:
SafetyScore = 0 + (위험요소별 가산점)

상세 계산:
1) 기본값은 0 (위험 요소가 없으면 0)

2) 노면 상태 평가
   if (노면 = 습윤)
      score += 10
   else if (노면 = 결빙 또는 적설)
      score += 40

3) 보행자 감지
   if (보행자 있음)
      score += 30

4) 역주행 감지
   if (역주행 감지)
      score += 100

점수의 의미:
- 0-49: 안전 (정상/혼잡 상태 가능)
- 50-99: 주의 (환경 위험 상태, 신호 연장)
- 100 이상: 긴급 (모든 신호 정지)

예시:
- 건조 도로, 보행자 없음, 역주행 없음: 점수 0 (안전)
- 습윤 도로, 보행자 있음, 역주행 없음: 점수 40 (주의)
- 결빙 도로, 보행자 있음: 점수 70 (환경위험 상태)
- 역주행 감지: 점수 100 이상 (긴급 상태)

상태 판단 단계 3: 현재 상태 결정

판단 우선순위 (높은 순서부터 검사):
1) 역주행 감지되었거나 안전성 >= 100
   상태 = 긴급

2) 안전성 >= 40 (결빙/적설 또는 보행자 있음)
   상태 = 환경위험

3) 효율성 < 60 (교통 정체)
   상태 = 혼잡

4) 그 외
   상태 = 정상

신호 제어 단계 4: 상태별 신호 조정

정상 상태 (Normal)
- 신호 변경: 없음
- 녹색 시간: 30초 (배속에 따라 조정)
- 노란색 시간: 3초
- 위상 전환: 남북 30초 -> 노란색 3초 -> 동서 30초 반복

혼잡 상태 (Congested)
- 신호 변경: 녹색 시간 추가 15초 연장
- 녹색 시간: 30 + 15 = 45초
- 목적: 대기 차량을 더 많이 통과시키기 위함

환경위험 상태 (EnvironmentalHazard)
- 신호 변경: 녹색 시간 추가 10초 연장 (보행자 감지 시)
- 신호 변경: 노란색 시간 추가 5초 연장 (결빙/적설 시)
- 녹색 시간: 최대 40초
- 목적: 운전자에게 충분한 반응 시간 제공

긴급 상태 (Emergency)
- 신호 변경: 모든 신호등 빨강
- 북, 남, 동, 서 모두 정지
- 복구 시점: 역주행 신호 사라질 때까지 유지

위상 전환 로직
==============

기본 개념
- 교차로는 두 가지 위상만 존재: 남북(N-S) 위상과 동서(E-W) 위상
- 한 방향만 녹색일 때 다른 방향은 빨강
- 위상 전환 시에만 신호 색상이 변함

남북(N-S) 위상 예시 (30초 주기):
1) 시간 0초: 북 녹색, 남 녹색, 동 빨강, 서 빨강
2) 시간 30초: 위상 전환 시작
3) 시간 30초: 북 노랑, 남 노랑, 동 빨강, 서 빨강 (3초 유지)
4) 시간 33초: 북 빨강, 남 빨강, 동 빨강, 서 빨강
5) 시간 33초: 동서 위상 시작

동서(E-W) 위상 예시:
1) 시간 33초: 북 빨강, 남 빨강, 동 녹색, 서 녹색
2) 시간 63초: 위상 전환 시작
3) 시간 63초: 동 노랑, 서 노랑 (3초 유지)
4) 시간 66초: 동 빨강, 서 빨강
5) 시간 66초: 남북 위상 시작

이벤트 기반 아키텍처
===================

이벤트 흐름:

1. DataGenerator.StartDay() 호출
   -> DispatcherTimer 시작

2. 타이머 Tick 발생 (5분 주기)
   -> OnTimerTick() 실행
   -> UpdateSensorData() 호출
   -> 센서 데이터 생성

3. SensorDataUpdated 이벤트 발행
   -> TrafficController 구독 (OnSensorDataUpdated)
   -> MainWindow 구독

4. TrafficController.OnSensorDataUpdated 실행
   -> CalculateTrafficEfficiencyScore()
   -> CalculateRoadSafetyScore()
   -> DetermineState()
   -> ExecuteSignalControl()
   -> TrafficUpdated 이벤트 발행

5. MainWindow와 DataManager가 TrafficUpdated 구독
   -> MainWindow: UI 업데이트
   -> DataManager: 데이터 기록

6. DataManager.RecordData()
   -> 메모리에 저장
   -> 나중에 CSV로 내보내기 가능

이벤트 체인의 이점:
- 모듈 간 느슨한 결합 (Loose Coupling)
- 각 모듈이 독립적으로 동작 가능
- 새로운 구독자 추가 용이
- 한 모듈의 오류가 다른 모듈 영향 최소화


시나리오별 동작 예시
===================

시나리오 1: 정상 교통 상황
시간 8:30, 날씨 맑음, 대기 차량 2대

1. 센서 생성
   Temperature: 15도, Rainfall: 0mm, RoadCondition: Dry
   WaitingCars_N/S/E/W: 2/1/1/2, AvgSpeed_GPS: 55km/h

2. 점수 계산
   EfficiencyScore = 100 - (6 x 2) = 88
   SafetyScore = 0 (위험 요소 없음)

3. 상태 판단
   88 >= 60 AND 0 < 50 -> 정상 상태

4. 신호 제어
   남북 녹색 30초 유지
   동서 빨강 0초

시나리오 2: 혼잡 교통 상황
시간 18:30, 날씨 맑음, 대기 차량 많음

1. 센서 생성
   Temperature: 20도, Rainfall: 0mm, RoadCondition: Dry
   WaitingCars_N/S/E/W: 8/7/8/9, AvgSpeed_GPS: 25km/h

2. 점수 계산
   EfficiencyScore = 100 - (32 x 2) - (50-25) = 17
   SafetyScore = 0

3. 상태 판단
   17 < 60 AND 0 < 50 -> 혼잡 상태

4. 신호 제어
   남북 녹색 30 + 15 = 45초로 연장
   동서 빨강

시나리오 3: 결빙 도로 상황
시간 2:00, 기온 -5도, 강수 3mm, 보행자 감지

1. 센서 생성
   Temperature: -5도, Rainfall: 3mm, RoadCondition: Icy
   IsPedestrianRemaining: true
   WaitingCars_N/S/E/W: 3/2/2/3, AvgSpeed_GPS: 40km/h

2. 점수 계산
   EfficiencyScore = 100 - (10 x 2) - (50-40) = 70
   SafetyScore = 40 (결빙) + 30 (보행자) = 70

3. 상태 판단
   SafetyScore 70 >= 50 and < 100 -> 환경위험 상태

4. 신호 제어
   남북 녹색 30 + 10 (보행자) + 5 (결빙) = 45초
   실제 녹색 시간 최대 40초로 제한
   동서 빨강

시나리오 4: 역주행 감지
언제든 역주행 감지

1. 센서 생성
   IsWrongWay: true

2. 점수 계산
   SafetyScore += 100

3. 상태 판단
   SafetyScore >= 100 -> 긴급 상태

4. 신호 제어
   모든 신호 빨강으로 변경
   북, 남, 동, 서 모두 정지
   역주행 신호 사라질 때까지 유지


배속과 신호 시간의 상호작용
==========================

배속이란?
시뮬레이션을 빠르게 진행하기 위한 기능
24시간을 더 짧은 시간에 시뮬레이션

배속 1배 (정상 속도)
- 센서 데이터 생성: 2000ms 주기
- 녹색 신호: 30초 (실제 30초)
- 5분 시뮬레이션: 5분 소요

배속 2배
- 센서 데이터 생성: 1000ms 주기
- 녹색 신호: 15초 (실제 시간상 15초이지만 시뮬레이션상 30초)
- 5분 시뮬레이션: 2.5분 소요

배속 4배
- 센서 데이터 생성: 500ms 주기
- 녹색 신호: 7.5초
- 5분 시뮬레이션: 1.25분 소요

배속 변경 시 신호 조정
배속을 1배에서 2배로 변경 시:
1) 현재 남은 시간을 1배 기준으로 정규화
   normalizedTime = 현재남은시간 x 현배속(1)

2) 새로운 배속에 맞게 다시 계산
   새로운남은시간 = normalizedTime / 새배속(2)

3) 결과적으로 실제 시간은 절반으로 단축


데이터 저장 구조
==============

메모리 저장
- DataManager의 _history 리스트에 누적 저장
- 각 요소는 TrafficUpdateEventArgs
  * Timestamp: 저장 시각
  * CurrentState: 현재 신호 상태
  * EfficiencyScore: 효율 점수
  * SafetyScore: 안전 점수
  * Light_N/S/E/W: 4개 방향 신호등 상태
  * RawData: 원본 센서 데이터 (SensorData)

CSV 저장 포맷
헤더: Timestamp,CurrentState,EfficiencyScore,SafetyScore,Temperature,Rainfall,WindSpeed,RoadCondition,VehicleSpeed,IsWrongWay,IsPedestrianRemaining,AvgSpeed_GPS,Signal_N,Signal_S,Signal_E,Signal_W

데이터 행 예시:
"2026-05-24 08:30:00",Normal,85,0,15.2,0,5.1,Dry,45.3,false,false,55.0,Green,Green,Red,Red

스레드 안전성
Lock을 사용하여 멀티스레드 환경에서도 안전
- RecordData() 호출 시 Lock
- SaveToCsv() 호출 시 Lock


센서 데이터 생성 알고리즘 상세
=============================

기온 생성 (코사인 기반 일교차)

목표: 자연스러운 일교차 표현
최저: 새벽 2-4시, 최고: 오후 14시

알고리즘:
1) 하루 시작 시 최저/최고 기온 결정
   dayMinTemp = -5 + random(0~20) = -5 ~ 15도
   dayMaxTemp = dayMinTemp + 5~15 = 0 ~ 30도

2) 각 5분마다 코사인 함수로 계산
   currentHour = simMinutes / 60
   angle = (currentHour - 14) / 24 * 2π
   cosValue = cos(angle)

3) 코사인 값 (-1~1)을 기온 범위로 변환
   baseTemp = dayMinTemp + (dayMaxTemp - dayMinTemp) * (cosValue + 1) / 2

4) 랜덤 노이즈 추가
   noise = random(-0.5 ~ 0.5) 도
   finalTemp = baseTemp + noise

5) 범위 제한 (-10 ~ 40도)

예시:
- 14시 (오후 2시): angle=0, cos=1, temp=최고
- 2시 (새벽 2시): angle≈π, cos≈-1, temp=최저
- 8시: 오전 중간값
- 20시: 저녁 중간값

강수 생성 (일일 기반)

목표: 하루 날씨 일관성 표현
30% 확률로 비 오는 날 결정

알고리즘:
1) 시뮬레이션 시작 시 하루 날씨 결정 (1회만)
   isDayRainy = random < 30%

2) 비 오는 날: 매 5분마다
   70% 확률로 강수 생성 (3~20mm)
   30% 확률로 강수 없음

3) 맑은 날: 항상 강수 0

노면 상태 결정 (종속성)

규칙:
- 적설: 영하 + 강수 > 5mm
- 결빙: 영하 (강수 무관)
- 습윤: 영상 + 강수 > 0
- 건조: 그 외

대기 차량 생성 (혼잡 시간 가중치)

통상적 상황:
- 임의의 차량 0~8대

혼잡 시간 (8-9시, 18-19시):
- 임의의 차량 5~15대

GPS 속도 (대기 차량 종속)

알고리즘:
1) 모든 방향의 평균 대기 차량 계산
   avgWaitingCars = (N+S+E+W) / 4

2) 정체 판단
   if (avgWaitingCars > 10)
      정체 상태: 속도 5~20 km/h
   else
      원활 상태: 속도 30~60 km/h


성능 고려사항
============

메모리 최적화
- 차량 객체 풀: 40개 미리 할당, 재활용하여 GC 부담 감소
- 그래프: 최근 데이터만 유지 (모든 데이터 보관 X)
- Brush 캐싱: 신호등 색상용 Brush 미리 생성 (매 프레임 재생성 X)
- 신호등 객체: 복사 대신 참조 사용으로 UI 동기화

이벤트 처리
- try-catch로 구독자 예외 격리
- 한 모듈의 오류가 다른 모듈 영향 없음

타이머 동기화
- DateTime.Now 기반 경과 시간 계산
- 배속 변경 시에도 완벽 동기화
- 신호 시간 정확도 유지

멀티스레드 안전성
- DataManager의 Lock으로 동시 접근 방지
- 이벤트 핸들러에서도 thread-safe 구현


핵심 코드 예시
==============

1. 데이터 구조: SensorData 클래스

public class SensorData
{
    // 기상 센서 데이터
    public double Temperature { get; init; }      // -10 ~ 40도
    public double Rainfall { get; init; }          // 0 ~ 50mm/h
    public double WindSpeed { get; init; }         // 0 ~ 15m/s

    // 도로 상태
    public RoadConditionType RoadCondition { get; init; }  // Dry/Wet/Icy/Snow

    // 지자기 센서 - 방향별 대기 차량
    public int WaitingCars_N { get; init; }
    public int WaitingCars_S { get; init; }
    public int WaitingCars_E { get; init; }
    public int WaitingCars_W { get; init; }

    // 레이더 센서
    public double VehicleSpeed { get; init; }     // 0 ~ 90km/h
    public bool IsWrongWay { get; init; }         // 역주행 감지
    public bool IsPedestrianRemaining { get; init; }  // 보행자 감지

    // GPS 센서
    public double AvgSpeed_GPS { get; init; }     // 평균 주행 속도
}

2. 신호등 클래스

public enum LightColor
{
    Red,     // 정지
    Yellow,  // 주의
    Green    // 진행
}

public class TrafficLight
{
    public LightColor Color { get; set; } = LightColor.Red;
    public int RemainingTime { get; set; } = 0;  // 초 단위

    // 복사본 생성 (데이터 무결성 유지)
    public TrafficLight Clone()
    {
        return new TrafficLight
        {
            Color = this.Color,
            RemainingTime = this.RemainingTime
        };
    }
}

3. 신호 상태 열거형

public enum TrafficState
{
    Normal,                  // 정상 교통
    Congested,              // 교통 혼잡
    EnvironmentalHazard,    // 환경 위험
    Emergency               // 긴급
}

4. DataGenerator: 센서 데이터 생성

public class DataGenerator : IDisposable
{
    private DispatcherTimer _timer;
    private int _speed = 1;  // 배속 (1, 2, 4)
    private double _simMinutes = 0;

    // 이벤트 정의
    public event Action<SensorData> SensorDataUpdated;
    public event Action DayCompleted;

    // 하루 시뮬레이션 시작
    public void StartDay()
    {
        if (_timer != null) return;  // 중복 시작 방지

        _simMinutes = 0;
        StartNewDay();  // 날씨 상태 결정

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(GetTimerInterval(_speed));
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    // 타이머 틱 이벤트
    private void OnTimerTick(object sender, EventArgs e)
    {
        UpdateSensorData();
    }

    // 센서 데이터 업데이트
    private void UpdateSensorData()
    {
        try
        {
            if (_simMinutes >= 1440) return;  // 24시간 = 1440분

            var sensorData = GenerateSensorData();
            SensorDataUpdated?.Invoke(sensorData);  // 이벤트 발행

            _simMinutes += 5;  // 5분씩 진행

            if (_simMinutes >= 1440)
            {
                Stop();
                DayCompleted?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DataGenerator] Error: {ex.Message}");
        }
    }

    // 배속 설정
    public void SetSpeed(int speed)
    {
        if (speed != 1 && speed != 2 && speed != 4)
            throw new ArgumentException("배속은 1, 2, 4만 가능합니다.");

        _speed = speed;
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(GetTimerInterval(_speed));
        }
    }

    // 타이머 간격 계산
    private int GetTimerInterval(int speed)
    {
        return 2000 / speed;  // 1x=2000ms, 2x=1000ms, 4x=500ms
    }
}

5. TrafficController: 신호 제어 핵심 로직

public partial class TrafficController : IDisposable
{
    private const int EFFICIENCY_BASE_SCORE = 100;
    private const int PENALTY_PER_WAITING_CAR = 2;
    private const int SPEED_LIMIT = 50;

    private const int SAFETY_BASE_SCORE = 0;
    private const int WET_PENALTY = 10;
    private const int FREEZING_SNOW_PENALTY = 40;
    private const int PEDESTRIAN_PENALTY = 30;
    private const int WRONG_WAY_PENALTY = 100;

    private const int BASE_GREEN_TIME = 30;  // 초
    private const int BASE_YELLOW_TIME = 3;  // 초

    public TrafficLight Light_N { get; private set; }
    public TrafficLight Light_S { get; private set; }
    public TrafficLight Light_E { get; private set; }
    public TrafficLight Light_W { get; private set; }

    public event EventHandler<TrafficUpdateEventArgs> TrafficUpdated;

    // 센서 데이터 수신 및 신호 제어
    private void OnSensorDataUpdated(SensorData data)
    {
        try
        {
            // 1단계: 점수 계산
            int efficiencyScore = CalculateTrafficEfficiencyScore(data);
            int safetyScore = CalculateRoadSafetyScore(data);

            // 2단계: 상태 판단
            TrafficState currentState = DetermineState(efficiencyScore, safetyScore);

            // 3단계: 신호 제어
            ExecuteSignalControl(currentState, data);

            // 4단계: 이벤트 발행 (UI와 DataManager에 알림)
            TrafficUpdated?.Invoke(this, new TrafficUpdateEventArgs
            {
                Timestamp = DateTime.Now,
                CurrentState = currentState,
                EfficiencyScore = efficiencyScore,
                SafetyScore = safetyScore,
                Light_N = this.Light_N,
                Light_S = this.Light_S,
                Light_E = this.Light_E,
                Light_W = this.Light_W,
                RawData = data
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrafficController] Error: {ex.Message}");
        }
    }

    // 교통 효율성 점수 계산
    private int CalculateTrafficEfficiencyScore(SensorData data)
    {
        int score = EFFICIENCY_BASE_SCORE;

        // 1) 대기 차량 합계
        int totalWaitingCars = data.WaitingCars_N + data.WaitingCars_S
                             + data.WaitingCars_E + data.WaitingCars_W;

        // 2) 대기 차량당 2점 감점
        score -= totalWaitingCars * PENALTY_PER_WAITING_CAR;

        // 3) 평균 속도가 50km/h 미만이면 추가 감점
        if (data.AvgSpeed_GPS < SPEED_LIMIT)
            score -= (int)(SPEED_LIMIT - data.AvgSpeed_GPS);

        // 4) 0 이상으로 클램프
        return Math.Max(0, score);
    }

    // 도로 안전성 점수 계산
    private int CalculateRoadSafetyScore(SensorData data)
    {
        int score = SAFETY_BASE_SCORE;

        // 노면 상태 평가
        if (data.RoadCondition == RoadConditionType.Icy
         || data.RoadCondition == RoadConditionType.Snow)
            score += FREEZING_SNOW_PENALTY;  // 40점
        else if (data.RoadCondition == RoadConditionType.Wet)
            score += WET_PENALTY;  // 10점

        // 보행자 감지
        if (data.IsPedestrianRemaining)
            score += PEDESTRIAN_PENALTY;  // 30점

        // 역주행 감지
        if (data.IsWrongWay)
            score += WRONG_WAY_PENALTY;  // 100점

        return score;
    }

    // 현재 상태 판단
    private TrafficState DetermineState(int efficiencyScore, int safetyScore)
    {
        // 1순위: 긴급 상태
        if (safetyScore >= WRONG_WAY_PENALTY)
            return TrafficState.Emergency;

        // 2순위: 환경 위험
        if (safetyScore >= FREEZING_SNOW_PENALTY)
            return TrafficState.EnvironmentalHazard;

        // 3순위: 혼잡
        if (efficiencyScore < 60)
            return TrafficState.Congested;

        // 기본: 정상
        return TrafficState.Normal;
    }

    // 상태별 신호 제어
    private void ExecuteSignalControl(TrafficState state, SensorData data)
    {
        switch (state)
        {
            case TrafficState.Emergency:
                SetAllRed();  // 모든 신호 정지
                return;

            case TrafficState.EnvironmentalHazard:
                // 결빙/적설: 노란색 5초 연장
                if (data.RoadCondition == RoadConditionType.Icy
                 || data.RoadCondition == RoadConditionType.Snow)
                    ExtendYellowLight(5);

                // 보행자: 녹색 10초 연장
                if (data.IsPedestrianRemaining)
                    ExtendGreenLight(10);
                break;

            case TrafficState.Congested:
                // 혼잡: 녹색 15초 연장
                ExtendGreenLight(15);
                break;
        }

        UpdateNormalPhase();
    }

    // 모든 신호 빨강으로 변경
    private void SetAllRed()
    {
        Light_N.Color = LightColor.Red;
        Light_S.Color = LightColor.Red;
        Light_E.Color = LightColor.Red;
        Light_W.Color = LightColor.Red;
        _isEmergencyActive = true;
    }

    // 배속 설정
    public void SetSimulationSpeed(int speed)
    {
        // 현재 남은 시간을 1배 기준으로 정규화
        double normalizedTime = _currentPhaseTotalTime * _simulationSpeed;

        // 새로운 배속에 맞게 다시 계산
        _currentPhaseTotalTime = normalizedTime / (double)speed;

        _simulationSpeed = speed;
    }
}

6. DataManager: 데이터 저장

public class DataManager
{
    private readonly object _lock = new object();
    private readonly List<TrafficUpdateEventArgs> _history = new List<TrafficUpdateEventArgs>();

    // 데이터 기록 (스레드 안전)
    public void RecordData(TrafficUpdateEventArgs data)
    {
        if (data == null) return;

        // 스냅샷 생성 (원본 변경 방지)
        var snapshot = new TrafficUpdateEventArgs
        {
            Timestamp = data.Timestamp,
            CurrentState = data.CurrentState,
            EfficiencyScore = data.EfficiencyScore,
            SafetyScore = data.SafetyScore,
            RawData = data.RawData,
            Light_N = data.Light_N?.Clone(),
            Light_S = data.Light_S?.Clone(),
            Light_E = data.Light_E?.Clone(),
            Light_W = data.Light_W?.Clone()
        };

        lock (_lock)
        {
            _history.Add(snapshot);
        }
    }

    // CSV로 저장
    public SaveResult SaveToCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return new SaveResult(false, "경로가 지정되지 않았습니다.");

        try
        {
            // 디렉토리 자동 생성
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // 스레드 안전하게 데이터 복사
            List<TrafficUpdateEventArgs> snapshot;
            lock (_lock)
            {
                snapshot = _history.ToList();
            }

            if (snapshot.Count == 0)
                return new SaveResult(false, "저장할 데이터가 없습니다.");

            // CSV 작성
            using (var writer = new StreamWriter(filePath))
            {
                // 헤더
                writer.WriteLine("Timestamp,CurrentState,EfficiencyScore,SafetyScore," +
                    "Temperature,Rainfall,WindSpeed,RoadCondition,VehicleSpeed," +
                    "IsWrongWay,IsPedestrianRemaining,AvgSpeed_GPS," +
                    "Signal_N,Signal_S,Signal_E,Signal_W");

                // 데이터 행
                foreach (var item in snapshot)
                {
                    string signalN = item.Light_N?.Color.ToString() ?? "None";
                    string signalS = item.Light_S?.Color.ToString() ?? "None";
                    string signalE = item.Light_E?.Color.ToString() ?? "None";
                    string signalW = item.Light_W?.Color.ToString() ?? "None";

                    double temp = item.RawData?.Temperature ?? 0;
                    double rain = item.RawData?.Rainfall ?? 0;
                    double wind = item.RawData?.WindSpeed ?? 0;
                    string road = item.RawData?.RoadCondition.ToString() ?? "Unknown";
                    double vSpeed = item.RawData?.VehicleSpeed ?? 0;
                    bool wrongWay = item.RawData?.IsWrongWay ?? false;
                    bool ped = item.RawData?.IsPedestrianRemaining ?? false;
                    double gpsSpeed = item.RawData?.AvgSpeed_GPS ?? 0;

                    writer.WriteLine($"\"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                        $"{item.CurrentState},{item.EfficiencyScore}," +
                        $"{item.SafetyScore},{temp},{rain},{wind},{road}," +
                        $"{vSpeed},{wrongWay},{ped},{gpsSpeed}," +
                        $"{signalN},{signalS},{signalE},{signalW}");
                }
            }

            return new SaveResult(true, $"CSV 저장 완료: {filePath}");
        }
        catch (Exception ex)
        {
            return new SaveResult(false, $"저장 실패: {ex.Message}");
        }
    }
}

7. 사용 예시 코드 (MainWindow)

public partial class MainWindow : Window
{
    private DataGenerator _dataGenerator;
    private TrafficController _trafficController;
    private DataManager _dataManager;

    public MainWindow()
    {
        InitializeComponent();

        // 모듈 초기화
        _dataGenerator = new DataGenerator();
        _trafficController = new TrafficController(_dataGenerator);
        _dataManager = new DataManager();

        // 이벤트 구독
        _trafficController.TrafficUpdated += OnTrafficUpdated;
        _dataGenerator.DayCompleted += OnDayCompleted;
    }

    // 시작 버튼 클릭
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _dataGenerator.StartDay();
        StatusTextBlock.Text = "시뮬레이션 시작...";
    }

    // 신호 업데이트 이벤트 처리
    private void OnTrafficUpdated(object sender, TrafficUpdateEventArgs e)
    {
        // UI 업데이트
        UpdateTrafficLights(e);
        UpdateScores(e);

        // 데이터 기록
        _dataManager.RecordData(e);
    }

    // 하루 완료 이벤트 처리
    private void OnDayCompleted()
    {
        StatusTextBlock.Text = "시뮬레이션 완료!";
        SaveButton.IsEnabled = true;
    }

    // 신호등 UI 업데이트
    private void UpdateTrafficLights(TrafficUpdateEventArgs e)
    {
        UpdateLightColor(LightN_Ellipse, e.Light_N.Color);
        UpdateLightColor(LightS_Ellipse, e.Light_S.Color);
        UpdateLightColor(LightE_Ellipse, e.Light_E.Color);
        UpdateLightColor(LightW_Ellipse, e.Light_W.Color);
    }

    private void UpdateLightColor(Ellipse ellipse, LightColor color)
    {
        ellipse.Fill = color switch
        {
            LightColor.Red => _brushRed,
            LightColor.Yellow => _brushYellow,
            LightColor.Green => _brushGreen,
            _ => _brushInactive
        };
    }

    // 점수 UI 업데이트
    private void UpdateScores(TrafficUpdateEventArgs e)
    {
        EfficiencyScoreTextBlock.Text = $"효율 점수: {e.EfficiencyScore}";
        SafetyScoreTextBlock.Text = $"안전 점수: {e.SafetyScore}";
        StateTextBlock.Text = $"상태: {e.CurrentState}";
    }

    // 저장 버튼 클릭
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = "csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            var result = _dataManager.SaveToCsv(saveDialog.FileName);
            MessageBox.Show(result.Message);
        }
    }

    // 배속 설정
    private void SetSpeed(int speed)
    {
        _dataGenerator.SetSpeed(speed);
        _trafficController.SetSimulationSpeed(speed);
    }
}

8. 센서 데이터 생성 알고리즘 코드

private SensorData GenerateSensorData()
{
    double currentHour = _simMinutes / 60.0;

    // Step 1: 기상 센서
    double temperature = GenerateTemperature();
    double rainfall = GenerateRainfall();
    double windSpeed = GenerateWindSpeed();

    // Step 2: 노면 상태 (종속)
    RoadConditionType roadCondition = GetRoadCondition(temperature, rainfall);

    // Step 3: 대기 차량
    int waitingCars_N = GetWaitingCars(currentHour);
    int waitingCars_S = GetWaitingCars(currentHour);
    int waitingCars_E = GetWaitingCars(currentHour);
    int waitingCars_W = GetWaitingCars(currentHour);

    // Step 4: GPS 속도 (대기 차량 종속)
    int avgWaitingCars = (waitingCars_N + waitingCars_S + waitingCars_E + waitingCars_W) / 4;
    double avgSpeed_GPS = GetAvgSpeed(avgWaitingCars);

    // Step 5: 레이더 센서
    double vehicleSpeed = GenerateVehicleSpeed();
    bool isWrongWay = GenerateWrongWay();
    bool isPedestrianRemaining = GeneratePedestrianRemaining();

    return new SensorData
    {
        Temperature = temperature,
        Rainfall = rainfall,
        WindSpeed = windSpeed,
        RoadCondition = roadCondition,
        WaitingCars_N = waitingCars_N,
        WaitingCars_S = waitingCars_S,
        WaitingCars_E = waitingCars_E,
        WaitingCars_W = waitingCars_W,
        VehicleSpeed = vehicleSpeed,
        IsWrongWay = isWrongWay,
        IsPedestrianRemaining = isPedestrianRemaining,
        AvgSpeed_GPS = avgSpeed_GPS
    };
}

// 기온 생성 (코사인 곡선)
private double GenerateTemperature()
{
    double currentHour = _simMinutes / 60.0;

    // 14시를 최고점으로 하는 코사인 곡선
    double angle = (currentHour - 14.0) / 24.0 * 2.0 * Math.PI;
    double cosValue = Math.Cos(angle);

    // 코사인 값 (-1~1)을 기온으로 변환
    double baseTemp = _dayMinTemp + (_dayMaxTemp - _dayMinTemp) * ((cosValue + 1.0) / 2.0);

    // 랜덤 노이즈 추가
    double noise = (_rng.NextDouble() - 0.5) * 1.0;
    double temperature = baseTemp + noise;

    return Math.Max(-10, Math.Min(35, temperature));
}

// 강수량 생성
private double GenerateRainfall()
{
    if (!_isDayRainy) return 0.0;  // 맑은 날

    // 비 오는 날: 70% 확률로 강수
    if (_rng.Next(0, 100) < 70)
        return 3.0 + _rng.NextDouble() * 17.0;  // 3~20mm
    else
        return 0.0;  // 잠시 그침
}

// 노면 상태 결정
private RoadConditionType GetRoadCondition(double temperature, double rainfall)
{
    // 적설: 영하 + 강수 많음
    if (temperature < 0.0 && rainfall > 5.0)
        return RoadConditionType.Snow;

    // 결빙: 영하
    if (temperature < 0.0)
        return RoadConditionType.Icy;

    // 습윤: 영상 + 강수
    if (rainfall > 0)
        return RoadConditionType.Wet;

    // 건조
    return RoadConditionType.Dry;
}

// 대기 차량 생성
private int GetWaitingCars(double currentHour)
{
    if (!_rushHourEnabled)
        return _rng.Next(0, 8);  // 일반 트래픽

    bool isRushHour = (currentHour >= 8 && currentHour <= 9) ||
                      (currentHour >= 18 && currentHour <= 19);

    return isRushHour ? _rng.Next(5, 15) : _rng.Next(0, 8);
}

// GPS 속도 계산
private double GetAvgSpeed(int waitingCars)
{
    if (waitingCars > 10)
        return _rng.Next(5, 20);   // 정체 상태
    else
        return _rng.Next(30, 60);  // 원활 상태
}
