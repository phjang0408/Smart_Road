using System;
using System.Windows.Threading;

namespace Smart_Road
{
    public class DataGenerator : IDisposable
    {
        private readonly Random _rng;
        private DispatcherTimer _timer;
        private int _speed = 1; // 배속 (1x, 2x, 4x)

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

        /// <summary>
        /// DataGenerator 초기화 및 Timer 시작
        /// </summary>
        public void Initialize()
        {
            if (_timer != null) return;  // H-3: 재진입 가드

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(GetTimerInterval(_speed));
            _timer.Tick += (s, e) => UpdateSensorData();
            _timer.Start();
        }

        /// <summary>
        /// Timer 정지
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
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
        /// 배속에 따른 Timer Interval 계산 (ms)
        /// </summary>
        private int GetTimerInterval(int speed)
        {
            return 1000 / speed; // 1x=1000ms, 2x=500ms, 4x=250ms
        }

        /// <summary>
        /// SensorData 생성 및 이벤트 발행
        /// </summary>
        private void UpdateSensorData()
        {
            try
            {
                var sensorData = GenerateSensorData();
                SensorDataUpdated?.Invoke(sensorData);
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
            int hour = DateTime.Now.Hour;

            // Step 1: 기상 센서 생성
            double temperature = GenerateTemperature(hour);
            double rainfall = GenerateRainfall();
            double windSpeed = GenerateWindSpeed();

            // Step 2: 노면 상태 종속 생성
            string roadCondition = GetRoadCondition(temperature, rainfall);

            // Step 3: 지자기 대기 차량 수
            int waitingCars_N = GetWaitingCars(hour);
            int waitingCars_S = GetWaitingCars(hour);
            int waitingCars_E = GetWaitingCars(hour);
            int waitingCars_W = GetWaitingCars(hour);

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
        /// 기온 생성 (시간대별 가중치)
        /// </summary>
        private double GenerateTemperature(int hour)
        {
            // 새벽 2~6시: 저온(-5 기준), 그 외: 고온(10 기준)
            int tempBase = (hour >= 2 && hour <= 6) ? TEMP_BASE_LOW : TEMP_BASE_HIGH;
            double temperature = tempBase + (_rng.NextDouble() * 10 - 5);

            // 범위 제한
            return Math.Max(MIN_TEMPERATURE, Math.Min(MAX_TEMPERATURE, temperature));
        }

        /// <summary>
        /// 강수량 생성 (30% 확률로 발생)
        /// </summary>
        private double GenerateRainfall()
        {
            return (_rng.Next(0, 10) < (int)(RAINFALL_PROBABILITY * 10)) ? _rng.NextDouble() * 20 : 0.0;
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
        /// </summary>
        private string GetRoadCondition(double temperature, double rainfall)
        {
            // H-2: 우선순위 수정 — 적설(저온+다량강수)을 먼저 평가
            if (temperature < FREEZING_TEMP && rainfall > SNOW_RAINFALL)
                return "적설";
            if (temperature < FREEZING_TEMP && rainfall > 0)
                return "결빙";
            if (rainfall > 0)
                return "습윤";
            return "건조";
        }

        #endregion

        #region 지자기 센서 생성

        /// <summary>
        /// 방향별 대기 차량 수 (혼잡 시간대 가중치)
        /// </summary>
        private int GetWaitingCars(int hour)
        {
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
