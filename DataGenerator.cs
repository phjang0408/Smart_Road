using System;
using System.Windows.Forms;

namespace Smart_Road
{
    public class DataGenerator
    {
        private Random _rng = new Random();
        private Timer _timer;
        private int _speed = 1; // 배속 (1x, 2x, 4x)

        // SensorData 갱신 시 발행하는 이벤트
        public event Action<SensorData> OnSensorDataUpdated;

        /// <summary>
        /// DataGenerator 초기화 및 Timer 시작
        /// </summary>
        public void Initialize()
        {
            _timer = new Timer();
            _timer.Interval = GetTimerInterval(_speed);
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
                _timer.Dispose();
            }
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
                _timer.Interval = GetTimerInterval(_speed);
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
            var sensorData = GenerateSensorData();
            OnSensorDataUpdated?.Invoke(sensorData);
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
            int tempBase = (hour >= 2 && hour <= 6) ? -5 : 10;
            double temperature = tempBase + (_rng.NextDouble() * 10 - 5);

            // 범위 제한: -10 ~ 35°C
            return Math.Max(-10, Math.Min(35, temperature));
        }

        /// <summary>
        /// 강수량 생성 (30% 확률로 발생)
        /// </summary>
        private double GenerateRainfall()
        {
            return (_rng.Next(0, 10) < 3) ? _rng.NextDouble() * 20 : 0.0;
        }

        /// <summary>
        /// 풍속 생성 (0 ~ 20 m/s)
        /// </summary>
        private double GenerateWindSpeed()
        {
            return _rng.NextDouble() * 20;
        }

        #endregion

        #region 노면 상태 종속 생성

        /// <summary>
        /// 노면 상태 결정 (기온과 강수량에 종속)
        /// </summary>
        private string GetRoadCondition(double temperature, double rainfall)
        {
            if (temperature < 0 && rainfall > 0)
                return "결빙";
            if (temperature < 2 && rainfall > 5)
                return "적설";
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
            bool isRushHour = (hour >= 8 && hour <= 9) || (hour >= 18 && hour <= 19);
            return isRushHour ? _rng.Next(5, 15) : _rng.Next(0, 8);
        }

        #endregion

        #region GPS 프로브 생성

        /// <summary>
        /// GPS 구간 평균 속도 (대기 차량 수에 종속)
        /// </summary>
        private double GetAvgSpeed(int waitingCars)
        {
            if (waitingCars > 10)
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
            return _rng.Next(0, 90);
        }

        /// <summary>
        /// 역주행 여부 (2% 확률)
        /// </summary>
        private bool GenerateWrongWay()
        {
            return _rng.Next(0, 100) < 2;
        }

        /// <summary>
        /// 잔류 보행자 여부 (15% 확률)
        /// </summary>
        private bool GeneratePedestrianRemaining()
        {
            return _rng.Next(0, 100) < 15;
        }

        #endregion
    }
}
