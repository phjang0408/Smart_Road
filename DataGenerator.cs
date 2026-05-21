using System;
using System.Windows.Threading;

namespace Smart_Road
{
    public class DataGenerator : IDisposable
    {
        private readonly Random _rng;
        private DispatcherTimer _timer;
        private int _speed = 1; // 배속 (1x, 2x, 4x)
        private bool _rushHourEnabled = true; // 혼잡 시간 모드

        // 시뮬레이션 시간 및 날씨 상태 (구조적 날씨)
        private int _simHour = 0;           // 시뮬레이션 현재 시각 (0~23)
        private bool _isDayRainy = false;   // 오늘 비 오는 날 여부
        private double _dayMinTemp;         // 오늘 최저기온 (새벽 4시)
        private double _dayMaxTemp;         // 오늘 최고기온 (오후 2시)

        // 상수 (매직 넘버 상수화)
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

        // SensorData 갱신 시 발행하는 이벤트
        public event Action<SensorData> SensorDataUpdated;

        // 하루(24시간) 시뮬레이션 완료 시 발행하는 이벤트
        public event Action DayCompleted;

        /// <summary>
        /// DataGenerator 초기화 — 날씨 상태 결정만 수행 (타이머 시작 없음)
        /// </summary>
        public void Initialize()
        {
            _simHour = 0;
            StartNewDay();
        }

        /// <summary>
        /// 하루 시뮬레이션 시작 — 타이머를 시작하고 24시간 데이터 생성
        /// </summary>
        public void StartDay()
        {
            if (_timer != null) return;  // 이미 실행 중이면 무시

            _simHour = 0;
            StartNewDay();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(GetTimerInterval(_speed));
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        /// <summary>
        /// Timer Tick 핸들러
        /// </summary>
        private void OnTimerTick(object sender, EventArgs e)
        {
            UpdateSensorData();
        }

        /// <summary>
        /// Timer 정지
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;  // 이벤트 핸들러 해제 (메모리 누수 방지)
                _timer.Stop();
                _timer = null;  // H-1: null 초기화로 dispose 상태 명시
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
        /// 현재 시뮬레이션 시각 반환 (0~23)
        /// </summary>
        public int GetCurrentHour()
        {
            return _simHour;
        }

        /// <summary>
        /// 타이머 실행 여부
        /// </summary>
        public bool IsRunning
        {
            get { return _timer != null; }
        }

        /// <summary>
        /// 배속 설정 (1, 2, 4)
        /// </summary>
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

        /// <summary>
        /// 혼잡 시간 모드 설정
        /// </summary>
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

        /// <summary>
        /// SensorData 생성 및 이벤트 발행
        /// </summary>
        private void UpdateSensorData()
        {
            try
            {
                // 하루 완료 시 더 이상 처리하지 않음
                if (_simHour >= 24) return;

                // 먼저 현재 시간의 데이터 생성
                var sensorData = GenerateSensorData();
                SensorDataUpdated?.Invoke(sensorData);

                // 그 다음 시뮬레이션 시간 진행 (매 tick마다 1시간 증가)
                _simHour++;
                if (_simHour >= 24)
                {
                    Stop();
                    DayCompleted?.Invoke();
                }
            }
            catch (Exception ex)
            {
                // H-4: 구독자 예외로 Timer 중단 방지
                System.Diagnostics.Debug.WriteLine($"[DataGenerator] Error in UpdateSensorData: {ex.Message}");
            }
        }

        /// <summary>
        /// 센서 데이터 생성 (메인 로직)
        /// </summary>
        private SensorData GenerateSensorData()
        {
            // Step 1: 기상 센서 생성 (사인 곡선 기반 일교차)
            double temperature = GenerateTemperature();
            double rainfall = GenerateRainfall();
            double windSpeed = GenerateWindSpeed();

            // Step 2: 노면 상태 종속 생성
            string roadCondition = GetRoadCondition(temperature, rainfall);

            // Step 3: 지자기 대기 차량 수 (_simHour 사용)
            int waitingCars_N = GetWaitingCars(_simHour);
            int waitingCars_S = GetWaitingCars(_simHour);
            int waitingCars_E = GetWaitingCars(_simHour);
            int waitingCars_W = GetWaitingCars(_simHour);

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
            // 24시간 주기 코사인: 최고점 14시 (cos(0)=1), 최저점 약 2시 (cos(π)=-1)
            // angle = (simHour - 14) / 24 * 2π
            // 14시 → angle=0 → cos(0)=1 → 최고
            // 2시 → angle≈π → cos(π)=-1 → 최저

            double angle = (_simHour - 14.0) / 24.0 * 2.0 * Math.PI;
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
        private string GetRoadCondition(double temperature, double rainfall)
        {
            // 적설: 저온 + 다량강수(눈)
            if (temperature < FREEZING_TEMP && rainfall > SNOW_RAINFALL)
                return "적설";

            // 결빙: 영하면 강수 조건 없이 자동 결빙 (맑은 날도 포함)
            if (temperature < FREEZING_TEMP)
                return "결빙";

            // 습윤: 영상 + 강수
            if (rainfall > 0)
                return "습윤";

            // 건조
            return "건조";
        }

        #endregion

        #region 지자기 센서 생성

        /// <summary>
        /// 방향별 대기 차량 수 (혼잡 시간대 가중치)
        /// </summary>
        private int GetWaitingCars(int hour)
        {
            // 혼잡 시간 모드가 비활성화되면 일반 트래픽만 반환
            if (!_rushHourEnabled)
                return _rng.Next(0, MAX_WAITING_CARS);

            bool isRushHour = (hour >= RUSH_HOUR_START_1 && hour <= RUSH_HOUR_END_1) ||
                              (hour >= RUSH_HOUR_START_2 && hour <= RUSH_HOUR_END_2);
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
        /// 역주행 여부 (2% 확률)
        /// </summary>
        private bool GenerateWrongWay()
        {
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