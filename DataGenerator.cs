using System;
using System.Windows.Threading;

namespace Smart_Road
{
    // 가상 센서 데이터를 생성하는 모듈
    // 기상 센서, 지자기 센서, 레이더 센서 등을 시뮬레이션하고
    // 이들 사이의 종속성을 구현하여 현실적인 데이터 생성
    public class DataGenerator : IDisposable
    {
        // 난수 생성기
        private readonly Random _rng;
        // 시뮬레이션 타이머 (배속에 따라 interval 조정)
        private DispatcherTimer _timer;
        // 시뮬레이션 배속 (1x, 2x, 4x)
        private int _speed = 1;
        // 혼잡 시간 모드 활성화 여부
        private bool _rushHourEnabled = true;
        // 역주행 강제 조건
        private bool _forceWrongWay = false;

        // 시뮬레이션 현재 시각 (분 단위, 0~1440분 = 0~24시간)
        private double _simMinutes = 0;
        // 오늘 비 오는 날 여부
        private bool _isDayRainy = false;
        // 오늘 최저기온 (새벽 4시경)
        private double _dayMinTemp;
        // 오늘 최고기온 (오후 2시경)
        private double _dayMaxTemp;

        private const int MIN_TEMPERATURE = -10;
        private const int MAX_TEMPERATURE = 35;
        private const int TEMP_BASE_LOW = -5;
        private const int TEMP_BASE_HIGH = 10;
        private const int RUSH_HOUR_START_1 = 8;
        private const int RUSH_HOUR_END_1 = 9;
        private const int RUSH_HOUR_START_2 = 18;
        private const int RUSH_HOUR_END_2 = 19;
        private const int MAX_WAITING_CARS_RUSH = 15;
        private const int MIN_WAITING_CARS_RUSH = 5;
        private const int MAX_WAITING_CARS = 8;
        private const int WAITING_CARS_THRESHOLD = 10;
        private const int MAX_VEHICLE_SPEED = 90;
        private const int WRONG_WAY_PROBABILITY = 2;
        private const int PEDESTRIAN_PROBABILITY = 15;
        private const double RAINFALL_PROBABILITY = 0.3;
        private const double MAX_WIND_SPEED = 20.0;
        private const double FREEZING_TEMP = 0.0;
        private const double SNOW_TEMP = 2.0;
        private const double SNOW_RAINFALL = 5.0;

        public DataGenerator(Random rng = null)
        {
            _rng = rng ?? new Random();
        }

        public event Action<SensorData> SensorDataUpdated;
        public event Action DayCompleted;

        /// <summary>
        /// DataGenerator 초기화 — 날씨 상태 결정만 수행 (타이머 시작 없음)
        /// </summary>
        public void Initialize()
        {
            _simMinutes = 0;
            StartNewDay();
        }

        // 하루 시뮬레이션 시작
        // 타이머를 설정하고 센서 데이터 업데이트를 시작함
        // 배속에 따라 타이머 주기가 조정됨
        public void StartDay()
        {
            // 이미 타이머가 실행 중이면 중복 시작 방지
            if (_timer != null) return;

            // 시뮬레이션 시간 초기화
            _simMinutes = 0;
            // 새 날씨 조건 설정
            StartNewDay();

            // 타이머 생성 및 설정
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(GetTimerInterval(_speed));
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        // 타이머 틱 이벤트 핸들러
        // 주기적으로 호출되어 센서 데이터를 생성하고 발행
        private void OnTimerTick(object sender, EventArgs e)
        {
            UpdateSensorData();
        }

        // 타이머 정지
        // 메모리 누수 방지를 위해 이벤트 핸들러를 명시적으로 해제
        public void Stop()
        {
            if (_timer != null)
            {
                // 이벤트 핸들러 제거 (메모리 누수 방지)
                _timer.Tick -= OnTimerTick;
                // 타이머 중지
                _timer.Stop();
                // null 초기화로 dispose 상태 명시
                _timer = null;
            }
        }

        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 현재 시뮬레이션 시각 반환 (0~23시)
        /// </summary>
        public int GetCurrentHour()
        {
            return (int)(_simMinutes / 60.0);
        }

        /// <summary>
        /// 타이머 실행 여부
        /// </summary>
        public bool IsRunning
        {
            get { return _timer != null; }
        }

        // 배속 설정 (1배, 2배, 4배만 가능)
        // 배속을 변경하면 타이머의 주기도 함께 조정됨
        public void SetSpeed(int speed)
        {
            // 유효한 배속 값 확인
            if (speed != 1 && speed != 2 && speed != 4)
                throw new ArgumentException("배속은 1, 2, 4만 가능합니다.");

            _speed = speed;
            // 타이머가 실행 중이면 interval 조정
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(GetTimerInterval(_speed));
            }
        }

        // 혼잡 시간 모드 설정
        // true이면 8~9시, 18~19시에 높은 차량 대기 수 생성
        // false이면 혼잡 시간 구분 없이 일정한 차량 대기 수 생성
        public void SetRushHourMode(bool enabled)
        {
            _rushHourEnabled = enabled;
        }

        /// <summary>
        /// 새로운 하루 시작 — 날씨 상태 결정 (1회/하루)
        /// </summary>
        private void StartNewDay()
        {
            // 비 오는 날 여부: 30% 확률
            _isDayRainy = _rng.Next(0, 100) < 30;

            // 최저기온: -5 ~ 15°C
            _dayMinTemp = -5 + _rng.NextDouble() * 20;

            // 최고기온: 최저 + 5~15도 (일교차 보장)
            double tempDelta = 5 + _rng.NextDouble() * 10;
            _dayMaxTemp = _dayMinTemp + tempDelta;

            // 범위 클램핑
            _dayMinTemp = Math.Max(-10, Math.Min(35, _dayMinTemp));
            _dayMaxTemp = Math.Max(-10, Math.Min(35, _dayMaxTemp));

            // 클램핑 후 최소 일교차 보장 (최고 >= 최저 + 2도)
            if (_dayMaxTemp <= _dayMinTemp)
                _dayMaxTemp = _dayMinTemp + 2.0;
        }

        /// <summary>
        /// 배속에 따른 Timer Interval 계산 (ms)
        /// 1x=2000ms (데이터 생성 속도를 느리게), 2x=1000ms, 4x=500ms
        /// </summary>
        private int GetTimerInterval(int speed)
        {
            return 2000 / speed; // 1x=2000ms, 2x=1000ms, 4x=500ms
        }

        // 센서 데이터 생성 및 이벤트 발행
        // 매 타이머 틱마다 호출되어 현재 시간의 센서 데이터를 생성하고 구독자에게 전달
        private void UpdateSensorData()
        {
            try
            {
                // 하루 완료 조건 확인 (1440분 = 24시간)
                if (_simMinutes >= 1440) return;

                // 현재 시간의 센서 데이터 생성
                var sensorData = GenerateSensorData();
                // SensorDataUpdated 이벤트 발행 (UI와 TrafficController가 구독)
                SensorDataUpdated?.Invoke(sensorData);

                // 시뮬레이션 시간 진행 (매 tick마다 5분씩 증가)
                // 24시간 = 1440분, 5분씩 증가하면 총 288번의 업데이트
                _simMinutes += 5;

                // 하루 완료 확인
                if (_simMinutes >= 1440)
                {
                    // 타이머 중지
                    Stop();
                    // DayCompleted 이벤트 발행 (UI가 구독하여 저장 처리)
                    DayCompleted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                // 구독자의 예외로 인해 타이머가 중단되지 않도록 예외 처리
                System.Diagnostics.Debug.WriteLine($"[DataGenerator] Error in UpdateSensorData: {ex.Message}");
            }
        }

        // 센서 데이터 생성 (메인 로직)
        // 기상 센서, 노면 상태, 대기 차량, GPS 속도, 레이더 데이터를 모두 생성
        // 이들 사이의 종속성을 구현하여 현실적인 데이터 반환
        private SensorData GenerateSensorData()
        {
            // 현재 시간(0~24) 계산
            double currentHour = _simMinutes / 60.0;

            // Step 1: 기상 센서 생성 (사인 곡선 기반 일교차)
            double temperature = GenerateTemperature();
            double rainfall = GenerateRainfall();
            double windSpeed = GenerateWindSpeed();

            // Step 2: 노면 상태 종속 생성
            RoadConditionType roadCondition = GetRoadCondition(temperature, rainfall);

            // Step 3: 지자기 대기 차량 수 (현재 시간 사용)
            int waitingCars_N = GetWaitingCars(currentHour);
            int waitingCars_S = GetWaitingCars(currentHour);
            int waitingCars_E = GetWaitingCars(currentHour);
            int waitingCars_W = GetWaitingCars(currentHour);

            // Step 4: GPS 구간 속도 (지자기에 종속)
            // 평균 대기 차량 수 기반
            int avgWaitingCars = (waitingCars_N + waitingCars_S + waitingCars_E + waitingCars_W) / 4;
            double avgSpeed_GPS = GetAvgSpeed(avgWaitingCars);

            // Step 5: 레이더 데이터 (독립)
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

        #region 기상 센서 생성

        /// <summary>
        /// 기온 생성 (코사인 곡선 기반 일교차)
        /// 최저: 새벽 2~4시경, 최고: 오후 14시
        /// </summary>
        private double GenerateTemperature()
        {
            // 현재 시간(0~24) 계산
            double currentHour = _simMinutes / 60.0;

            // 24시간 주기 코사인: 최고점 14시 (cos(0)=1), 최저점 약 2시 (cos(π)=-1)
            // angle = (currentHour - 14) / 24 * 2π
            // 14시 → angle=0 → cos(0)=1 → 최고
            // 2시 → angle≈π → cos(π)=-1 → 최저

            double angle = (currentHour - 14.0) / 24.0 * 2.0 * Math.PI;
            double cosValue = Math.Cos(angle);

            // cos(-1~1)를 (0~1) 범위로 정규화
            double baseTemp = _dayMinTemp + (_dayMaxTemp - _dayMinTemp) * ((cosValue + 1.0) / 2.0);

            // 작은 랜덤 노이즈 ±0.5°C
            double noise = (_rng.NextDouble() - 0.5) * 1.0;
            double temperature = baseTemp + noise;

            // 범위 제한
            return Math.Max(MIN_TEMPERATURE, Math.Min(MAX_TEMPERATURE, temperature));
        }

        /// <summary>
        /// 강수량 생성 (하루 단위 날씨에 종속)
        /// 맑은 날: 0 반환, 비 오는 날: 70% 확률로 강수 (3~20mm)
        /// </summary>
        private double GenerateRainfall()
        {
            // 맑은 날이면 강수 없음
            if (!_isDayRainy) return 0.0;

            // 비 오는 날: 70% 확률로 강수, 30% 확률로 잠시 그침
            if (_rng.Next(0, 100) < 70)
                return 3.0 + _rng.NextDouble() * 17.0;  // 3~20mm
            else
                return 0.0;  // 잠시 그침
        }

        /// <summary>
        /// 풍속 생성
        /// </summary>
        private double GenerateWindSpeed()
        {
            return _rng.NextDouble() * MAX_WIND_SPEED;
        }

        #endregion

        #region 노면 상태 종속 생성

        /// <summary>
        /// 노면 상태 결정 (기온과 강수량에 종속)
        /// 영하이면 강수 없어도 자연적으로 결빙됨
        /// </summary>
        private RoadConditionType GetRoadCondition(double temperature, double rainfall)
        {
            // 적설: 저온 + 다량강수(눈)
            if (temperature < FREEZING_TEMP && rainfall > SNOW_RAINFALL)
                return RoadConditionType.Snow;

            // 결빙: 영하면 강수 조건 없이 자동 결빙 (맑은 날도 포함)
            if (temperature < FREEZING_TEMP)
                return RoadConditionType.Icy;

            // 습윤: 영상 + 강수
            if (rainfall > 0)
                return RoadConditionType.Wet;

            // 건조
            return RoadConditionType.Dry;
        }

        #endregion

        #region 지자기 센서 생성

        /// <summary>
        /// 방향별 대기 차량 수 (혼잡 시간대 가중치)
        /// </summary>
        private int GetWaitingCars(double currentHour)
        {
            // 혼잡 시간 모드가 비활성화되면 일반 트래픽만 반환
            if (!_rushHourEnabled)
                return _rng.Next(0, MAX_WAITING_CARS);

            bool isRushHour = (currentHour >= RUSH_HOUR_START_1 && currentHour <= RUSH_HOUR_END_1) ||
                              (currentHour >= RUSH_HOUR_START_2 && currentHour <= RUSH_HOUR_END_2);
            return isRushHour ? _rng.Next(MIN_WAITING_CARS_RUSH, MAX_WAITING_CARS_RUSH) : _rng.Next(0, MAX_WAITING_CARS);
        }

        #endregion

        #region GPS 프로브 생성

        /// <summary>
        /// GPS 구간 평균 속도 (대기 차량 수에 종속)
        /// </summary>
        private double GetAvgSpeed(int waitingCars)
        {
            if (waitingCars > WAITING_CARS_THRESHOLD)
                return _rng.Next(5, 20);   // 정체 (5 ~ 20 km/h)
            else
                return _rng.Next(30, 60);  // 원활 (30 ~ 60 km/h)
        }

        #endregion

        #region 레이더 센서 생성

        /// <summary>
        /// 차량 진입 속도 (0 ~ 90 km/h)
        /// </summary>
        private double GenerateVehicleSpeed()
        {
            // M-5: 연속값으로 수정 (반환타입 double과 일관성)
            return _rng.NextDouble() * MAX_VEHICLE_SPEED;
        }

        /// <summary>
        /// 역주행 강제 조건 설정
        /// </summary>
        public void ForceWrongWay(bool force)
        {
            _forceWrongWay = force;
        }

        /// <summary>
        /// 역주행 여부 (강제 조건 우선, 아니면 2% 확률)
        /// </summary>
        private bool GenerateWrongWay()
        {
            if (_forceWrongWay) return true;
            return _rng.Next(0, 100) < WRONG_WAY_PROBABILITY;
        }

        /// <summary>
        /// 잔류 보행자 여부 (15% 확률)
        /// </summary>
        private bool GeneratePedestrianRemaining()
        {
            return _rng.Next(0, 100) < PEDESTRIAN_PROBABILITY;
        }

        #endregion
    }
}