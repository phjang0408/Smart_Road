using System;

namespace Smart_Road
{
    public enum TrafficState
    {
        Normal,
        Congested,
        EnvironmentalHazard,
        Emergency
    }

    // 교통 신호를 제어하는 모듈
    // 센서 데이터를 받아서 교통 효율성과 안전성을 평가
    // 그에 따라 신호를 제어하는 로직 구현
    public partial class TrafficController : IDisposable
    {
        // 교통 효율성 점수 계산 상수들
        // 기본값 100에서 대기 차량 수와 평균 속도에 따라 감점
        private const int EFFICIENCY_BASE_SCORE = 100;
        private const int PENALTY_PER_WAITING_CAR = 2;
        private const int SPEED_LIMIT = 50;

        // 도로 안전성 점수 계산 상수들
        // 기본값 0에서 위험 요소에 따라 가산
        private const int SAFETY_BASE_SCORE = 0;
        private const int WET_PENALTY = 10;
        private const int FREEZING_SNOW_PENALTY = 40;
        private const int PEDESTRIAN_PENALTY = 30;
        private const int WRONG_WAY_PENALTY = 100;

        // 신호 시간 설정 (초 단위)
        private const int BASE_GREEN_TIME = 30;
        private const int BASE_YELLOW_TIME = 3;

        // 시뮬레이션 배속 (1배, 2배, 4배)
        private int _simulationSpeed = 1;

        public TrafficLight Light_N { get; private set; } = new TrafficLight();
        public TrafficLight Light_S { get; private set; } = new TrafficLight();
        public TrafficLight Light_E { get; private set; } = new TrafficLight();
        public TrafficLight Light_W { get; private set; } = new TrafficLight();

        private bool isNorthSouthPhase = true;
        
        // 타이머 동기화용 변수
        private DateTime _lastStateChangeTime;
        private double _currentPhaseTotalTime = BASE_GREEN_TIME;   // int->double
        private int _currentSpeed = 1;

        private bool _isExtendedThisPhase = false; 
        private bool _isEmergencyActive = false;

        public bool ForceBlackIce { get; set; } = false;
        public bool ForceWrongWay { get; set; } = false;

        private DataGenerator _generator;
        public event EventHandler<TrafficUpdateEventArgs> TrafficUpdated;

        // 생성자
        // DataGenerator로부터 센서 데이터를 받아 신호를 제어하도록 설정
        public TrafficController(DataGenerator generator)
        {
            _generator = generator;
            // 센서 데이터 업데이트 이벤트 구독
            _generator.SensorDataUpdated += OnSensorDataUpdated;

            // 초기 신호 방향 설정 (남북 방향부터 시작)
            isNorthSouthPhase = true;
            _lastStateChangeTime = DateTime.Now;
            InitializeDefaultLights();
        }

        // 시뮬레이션 배속 설정
        // 배속 변경 시 진행 중인 신호의 남은 시간도 함께 조정
        // 배속을 높이면 남은 시간도 단축되어 현실적인 신호 진행 구현
        public void SetSimulationSpeed(int speed)
        {
            // 현재 남은 시간을 1배 기준으로 정규화
            double normalizedTime = _currentPhaseTotalTime * _simulationSpeed;

            // 새로운 배속에 맞게 다시 계산
            _currentPhaseTotalTime = normalizedTime / (double)speed;

            // 배속 업데이트
            _simulationSpeed = speed;
        }

        // 신호 상태 완전히 초기화
        // 새로운 하루 시뮬레이션 시작 시 호출하여 신호를 초기 상태로 리셋
        public void Reset()
        {
            // 남북 방향 신호부터 시작
            isNorthSouthPhase = true;
            // 신호 변경 시각 기록
            _lastStateChangeTime = DateTime.Now;
            // 신호 시간을 배속에 맞게 설정
            _currentPhaseTotalTime = BASE_GREEN_TIME / (double)_simulationSpeed;
            // 신호 연장 플래그 초기화
            _isExtendedThisPhase = false;
            // 응급 모드 해제
            _isEmergencyActive = false;
            // 신호 색상 초기화
            InitializeDefaultLights();
        }

        // 리소스 해제
        // IDisposable 구현으로 메모리 누수 방지
        public void Dispose()
        {
            if (_generator != null)
            {
                // 이벤트 핸들러 제거
                _generator.SensorDataUpdated -= OnSensorDataUpdated;
            }
        }

        private void InitializeDefaultLights()
        {
            int adjustedGreenTime = BASE_GREEN_TIME / _simulationSpeed;
            Light_N.Color = LightColor.Green; Light_N.RemainingTime = adjustedGreenTime;
            Light_S.Color = LightColor.Green; Light_S.RemainingTime = adjustedGreenTime;
            Light_E.Color = LightColor.Red;   Light_E.RemainingTime = 0;
            Light_W.Color = LightColor.Red;   Light_W.RemainingTime = 0;
            _currentPhaseTotalTime = adjustedGreenTime;
        }

        public void PassTime(double deltaTime)
        {
            if (_isEmergencyActive) return;

            _currentPhaseTotalTime -= deltaTime;
            UpdateNormalPhase();
        }

        private void OnSensorDataUpdated(SensorData data)
        {
            // 예외 처리 강화
            try
            {
                int efficiencyScore = CalculateTrafficEfficiencyScore(data);
                int safetyScore = CalculateRoadSafetyScore(data);
                TrafficState currentState = DetermineState(efficiencyScore, safetyScore);

                ExecuteSignalControl(currentState, data);

                TrafficUpdated?.Invoke(this, new TrafficUpdateEventArgs
                {
                    Timestamp = DateTime.Now, 
                    CurrentState = currentState,
                    EfficiencyScore = efficiencyScore,
                    SafetyScore = safetyScore,
                    /*
                    // Clone()을 사용하여 원본 데이터가 이후에 덮어씌워지는 것 방지
                    Light_N = this.Light_N.Clone(), 
                    Light_S = this.Light_S.Clone(),
                    Light_E = this.Light_E.Clone(),
                    Light_W = this.Light_W.Clone(),
                    */

                    // Clone()을 사용하면 UI에 복사본을 주는데, Controller가 신호등 시간을 바꾸더라도 복사본을 줬기 때문에 반영을 못함
                    // 따라서 Clone을 쓰면 안된다.
                    Light_N = this.Light_N,
                    Light_S = this.Light_S,
                    Light_E = this.Light_E,
                    Light_W = this.Light_W,

                    RawData = data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TrafficController] 에러 발생: {ex.Message}");
            }
        }

        private int CalculateTrafficEfficiencyScore(SensorData data)
        {
            int score = EFFICIENCY_BASE_SCORE;
            int totalWaitingCars = data.WaitingCars_N + data.WaitingCars_S + data.WaitingCars_E + data.WaitingCars_W;
            score -= totalWaitingCars * PENALTY_PER_WAITING_CAR;

            if (data.AvgSpeed_GPS < SPEED_LIMIT)
                score -= (int)(SPEED_LIMIT - data.AvgSpeed_GPS);

            return Math.Max(0, score);
        }

        private int CalculateRoadSafetyScore(SensorData data)
        {
            int score = SAFETY_BASE_SCORE;

            if (ForceBlackIce || data.RoadCondition == RoadConditionType.Icy || data.RoadCondition == RoadConditionType.Snow) score += FREEZING_SNOW_PENALTY;
            else if (data.RoadCondition == RoadConditionType.Wet) score += WET_PENALTY;

            if (data.IsPedestrianRemaining) score += PEDESTRIAN_PENALTY;
            if (ForceWrongWay || data.IsWrongWay) score += WRONG_WAY_PENALTY;

            return score;
        }

        private TrafficState DetermineState(int efficiencyScore, int safetyScore)
        {
            if (safetyScore >= WRONG_WAY_PENALTY) return TrafficState.Emergency;
            if (safetyScore >= FREEZING_SNOW_PENALTY) return TrafficState.EnvironmentalHazard;
            if (efficiencyScore < 60) return TrafficState.Congested;
            return TrafficState.Normal;
        }

        private void ExecuteSignalControl(TrafficState state, SensorData data)
        {
            switch (state)
            {
                case TrafficState.Emergency:
                    SetAllRed();
                    return;

                case TrafficState.EnvironmentalHazard:
                    if (ForceBlackIce || data.RoadCondition == RoadConditionType.Icy || data.RoadCondition == RoadConditionType.Snow)
                        ExtendYellowLight(5);
                    if (data.IsPedestrianRemaining)
                        ExtendGreenLight(10);
                    break;

                case TrafficState.Congested:
                    ExtendGreenLight(15);
                    break;
            }

            UpdateNormalPhase();
        }

        // DateTime 기반으로 배속 동작 시에도 완벽하게 동기화되는 타이머 로직
        private void UpdateNormalPhase()
        {
            if (_isEmergencyActive)
            {
                _lastStateChangeTime = DateTime.Now;
                _isEmergencyActive = false;
            }

            // active, wait에 따로 저장하면서 참조가 다 끊어짐
            // 그래서 객체에 직접 대입해줘야 내부시간이랑 UI랑 동기화 가능
            /*
            TrafficLight active1 = isNorthSouthPhase ? Light_N : Light_E;
            TrafficLight active2 = isNorthSouthPhase ? Light_S : Light_W;
            TrafficLight wait1 = isNorthSouthPhase ? Light_E : Light_N;
            TrafficLight wait2 = isNorthSouthPhase ? Light_W : Light_S;

            wait1.Color = LightColor.Red; wait1.RemainingTime = 0;
            wait2.Color = LightColor.Red; wait2.RemainingTime = 0;
            
            TimeSpan elapsed = DateTime.Now - _lastStateChangeTime;
            */

            //int remainingSeconds = Math.Max(0, _currentPhaseTotalTime - (int)elapsed.TotalSeconds);
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling(_currentPhaseTotalTime));

            // 객체에 직접 대입
            if (isNorthSouthPhase)
            {
                Light_N.RemainingTime = remainingSeconds; Light_S.RemainingTime = remainingSeconds;
                Light_E.Color = LightColor.Red; Light_W.Color = LightColor.Red;
                Light_E.RemainingTime = 0; Light_W.RemainingTime = 0;
            }
            else
            {
                Light_E.RemainingTime = remainingSeconds; Light_W.RemainingTime = remainingSeconds;
                Light_N.Color = LightColor.Red; Light_S.Color = LightColor.Red;
                Light_N.RemainingTime = 0; Light_S.RemainingTime = 0;
            }

            //active1.RemainingTime = remainingSeconds;
            //active2.RemainingTime = remainingSeconds;

            if (remainingSeconds <= 0)
            {
                //SwitchPhase(active1, active2, wait1, wait2);
                SwitchPhase();
            }
        }

        //private void SwitchPhase(TrafficLight active1, TrafficLight active2, TrafficLight wait1, TrafficLight wait2)
        private void SwitchPhase()
        {

            if (isNorthSouthPhase)
            {
                if (Light_N.Color == LightColor.Green)
                {
                    Light_N.Color = LightColor.Yellow; Light_S.Color = LightColor.Yellow;
                    _currentPhaseTotalTime = BASE_YELLOW_TIME / (double)_simulationSpeed;
                }
                else
                {
                    Light_N.Color = LightColor.Red; Light_S.Color = LightColor.Red;
                    isNorthSouthPhase = false; // 동서 방향으로 교체
                    Light_E.Color = LightColor.Green; Light_W.Color = LightColor.Green;
                    _currentPhaseTotalTime = BASE_GREEN_TIME / (double)_simulationSpeed;
                }
            }
            else
            {
                if (Light_E.Color == LightColor.Green)
                {
                    Light_E.Color = LightColor.Yellow; Light_W.Color = LightColor.Yellow;
                    _currentPhaseTotalTime = BASE_YELLOW_TIME / (double)_simulationSpeed;
                }
                else
                {
                    Light_E.Color = LightColor.Red; Light_W.Color = LightColor.Red;
                    isNorthSouthPhase = true; // 남북 방향으로 교체
                    Light_N.Color = LightColor.Green; Light_S.Color = LightColor.Green;
                    _currentPhaseTotalTime = BASE_GREEN_TIME / (double)_simulationSpeed;
                }
            }

            _lastStateChangeTime = DateTime.Now;
            _isExtendedThisPhase = false; // 새 페이즈 시작 시 연장 플래그 초기화

            //UpdateNormalPhase(); // 변경된 상태 즉시 반영

            // 신호가 바뀌자마자 바뀐 시간을 즉시 반영
            int updatedSeconds = (int)Math.Ceiling(_currentPhaseTotalTime);
            if (isNorthSouthPhase) {
                Light_N.RemainingTime = updatedSeconds;
                Light_S.RemainingTime = updatedSeconds;
            }
            else { 
                Light_E.RemainingTime = updatedSeconds;
                Light_W.RemainingTime = updatedSeconds;
            }
        }
    

        private void SetAllRed()
        {
            // 긴급 정지는 약 3초 유지 (배속 반영)
            int emergencyDuration = 3;
            int adjustedDuration = emergencyDuration / _simulationSpeed;

            Light_N.Color = LightColor.Red; Light_N.RemainingTime = adjustedDuration;
            Light_S.Color = LightColor.Red; Light_S.RemainingTime = adjustedDuration;
            Light_E.Color = LightColor.Red; Light_E.RemainingTime = adjustedDuration;
            Light_W.Color = LightColor.Red; Light_W.RemainingTime = adjustedDuration;

            _currentPhaseTotalTime = adjustedDuration;
            _isEmergencyActive = true;
        }

        private void ExtendYellowLight(int extra)
        {
            if (!_isExtendedThisPhase && (Light_N.Color == LightColor.Yellow || Light_E.Color == LightColor.Yellow))
            {
                _currentPhaseTotalTime += extra / (double)_simulationSpeed;
                _isExtendedThisPhase = true;
            }
        }

        private void ExtendGreenLight(int extra)
        {
            if (!_isExtendedThisPhase && (Light_N.Color == LightColor.Green || Light_E.Color == LightColor.Green))
            {
                _currentPhaseTotalTime += extra / (double)_simulationSpeed;
                _isExtendedThisPhase = true;
            }
        }
    }
}
