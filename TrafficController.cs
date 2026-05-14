using System;

namespace Smart_Road
{
    /// <summary>
    /// 교차로의 4단계 상태 정의
    /// </summary>
    public enum TrafficState
    {
        Normal,             // 정상
        Congested,          // 혼잡
        EnvironmentalHazard,// 환경위험
        Emergency           // 긴급
    }

    /// <summary>
    /// 스마트 교차로의 핵심 두뇌 역할을 하는 컨트롤러 클래스
    /// </summary>
    public partial class TrafficController
    {
        // --- 가중치 및 임계값 상수 (제안서 및 설계 기준) ---
        private const int EFFICIENCY_BASE_SCORE = 100;
        private const int PENALTY_PER_WAITING_CAR = 2;
        private const int SPEED_LIMIT = 50; // 도심 제한 속도 기준

        private const int SAFETY_BASE_SCORE = 0;
        private const int WET_PENALTY = 10;
        private const int FREEZING_SNOW_PENALTY = 40;
        private const int PEDESTRIAN_PENALTY = 30;
        private const int WRONG_WAY_PENALTY = 100;

        private const int BASE_GREEN_TIME = 30;  // 기본 녹색 신호 시간
        private const int BASE_YELLOW_TIME = 3;  // 기본 황색 신호 시간

        // --- 내부 상태 변수 ---
        public TrafficLight Light_N { get; private set; } = new TrafficLight();
        public TrafficLight Light_S { get; private set; } = new TrafficLight();
        public TrafficLight Light_E { get; private set; } = new TrafficLight();
        public TrafficLight Light_W { get; private set; } = new TrafficLight();

        private bool isNorthSouthPhase = true; // 현재 녹색 신호 축 (true: 남북, false: 동서)
        
        // 외부(UI, 저장 모듈)에서 구독할 이벤트
        public event EventHandler<TrafficUpdateEventArgs> TrafficUpdated;

        /// <summary>
        /// 생성자: 데이터 생성기를 연결하고 초기 신호를 설정합니다.
        /// </summary>
        public TrafficController(DataGenerator generator)
        {
            // 데이터 생성기의 이벤트 구독
            generator.SensorDataUpdated += OnSensorDataUpdated;

            // 초기 상태: 남북 방향 녹색 신호로 시작
            isNorthSouthPhase = true;
            InitializeDefaultLights();
        }

        private void InitializeDefaultLights()
        {
            Light_N.Color = LightColor.Green; Light_N.RemainingTime = BASE_GREEN_TIME;
            Light_S.Color = LightColor.Green; Light_S.RemainingTime = BASE_GREEN_TIME;
            Light_E.Color = LightColor.Red;   Light_E.RemainingTime = 0;
            Light_W.Color = LightColor.Red;   Light_W.RemainingTime = 0;
        }

        /// <summary>
        /// 매초 데이터가 들어올 때마다 실행되는 메인 파이프라인
        /// </summary>
        private void OnSensorDataUpdated(SensorData data)
        {
            // 1. 점수 계산 (Efficiency & Safety)
            int efficiencyScore = CalculateTrafficEfficiencyScore(data);
            int safetyScore = CalculateRoadSafetyScore(data);

            // 2. 4단계 상태 판단
            TrafficState currentState = DetermineState(efficiencyScore, safetyScore);

            // 3. 규칙 기반 신호 제어 로직 실행
            ExecuteSignalControl(currentState, data);

            // 4. 외부로 결과 전달 (이벤트 발행)
            TrafficUpdated?.Invoke(this, new TrafficUpdateEventArgs
            {
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

        #region 점수 계산 및 상태 판단 로직

        private int CalculateTrafficEfficiencyScore(SensorData data)
        {
            int score = EFFICIENCY_BASE_SCORE;
            int totalWaitingCars = data.WaitingCars_N + data.WaitingCars_S + data.WaitingCars_E + data.WaitingCars_W;
            
            score -= totalWaitingCars * PENALTY_PER_WAITING_CAR;

            if (data.AvgSpeed_GPS < SPEED_LIMIT)
            {
                score -= (int)(SPEED_LIMIT - data.AvgSpeed_GPS);
            }
            return Math.Max(0, score);
        }

        private int CalculateRoadSafetyScore(SensorData data)
        {
            int score = SAFETY_BASE_SCORE;

            // 노면 상태 가중치
            if (data.RoadCondition == "습윤") score += WET_PENALTY;
            else if (data.RoadCondition == "결빙" || data.RoadCondition == "적설") score += FREEZING_SNOW_PENALTY;

            // 돌발 상황 가중치
            if (data.IsPedestrianRemaining) score += PEDESTRIAN_PENALTY;
            if (data.IsWrongWay) score += WRONG_WAY_PENALTY;

            return score;
        }

        private TrafficState DetermineState(int efficiencyScore, int safetyScore)
        {
            if (safetyScore >= WRONG_WAY_PENALTY) return TrafficState.Emergency;
            if (safetyScore >= FREEZING_SNOW_PENALTY) return TrafficState.EnvironmentalHazard;
            if (efficiencyScore < 60) return TrafficState.Congested;
            
            return TrafficState.Normal;
        }

        #endregion

        #region 신호 제어 및 타이머 로직

        private void ExecuteSignalControl(TrafficState state, SensorData data)
        {
            // [상태별 특수 제어]
            switch (state)
            {
                case TrafficState.Emergency:
                    SetAllRed(); // 역주행 시 전 방향 적색
                    return;

                case TrafficState.EnvironmentalHazard:
                    if (data.RoadCondition == "결빙" || data.RoadCondition == "적설")
                        ExtendYellowLight(5); // 블랙아이스 시 황색 연장
                    if (data.IsPedestrianRemaining)
                        ExtendGreenLight(10); // 보행자 잔류 시 녹색 연장
                    break;

                case TrafficState.Congested:
                    ExtendGreenLight(15); // 혼잡 시 녹색 연장
                    break;
            }

            // [일반적인 신호 주기 업데이트]
            UpdateNormalPhase();
        }

        private void UpdateNormalPhase()
        {
            TrafficLight active1 = isNorthSouthPhase ? Light_N : Light_E;
            TrafficLight active2 = isNorthSouthPhase ? Light_S : Light_W;
            TrafficLight wait1 = isNorthSouthPhase ? Light_E : Light_N;
            TrafficLight wait2 = isNorthSouthPhase ? Light_W : Light_S;

            // 대기 축은 적색 유지
            wait1.Color = LightColor.Red; wait1.RemainingTime = 0;
            wait2.Color = LightColor.Red; wait2.RemainingTime = 0;

            if (active1.RemainingTime > 0)
            {
                active1.RemainingTime--;
                active2.RemainingTime--;
            }
            else
            {
                SwitchPhase(active1, active2, wait1, wait2);
            }
        }

        private void SwitchPhase(TrafficLight active1, TrafficLight active2, TrafficLight wait1, TrafficLight wait2)
        {
            if (active1.Color == LightColor.Green)
            {
                active1.Color = LightColor.Yellow; active1.RemainingTime = BASE_YELLOW_TIME;
                active2.Color = LightColor.Yellow; active2.RemainingTime = BASE_YELLOW_TIME;
            }
            else
            {
                active1.Color = LightColor.Red; active1.RemainingTime = 0;
                active2.Color = LightColor.Red; active2.RemainingTime = 0;

                isNorthSouthPhase = !isNorthSouthPhase; // 축 교체

                wait1.Color = LightColor.Green; wait1.RemainingTime = BASE_GREEN_TIME;
                wait2.Color = LightColor.Green; wait2.RemainingTime = BASE_GREEN_TIME;
            }
        }

        #endregion

        #region 세부 제어 헬퍼 메서드

        private void SetAllRed()
        {
            Light_N.Color = LightColor.Red; Light_N.RemainingTime = 99;
            Light_S.Color = LightColor.Red; Light_S.RemainingTime = 99;
            Light_E.Color = LightColor.Red; Light_E.RemainingTime = 99;
            Light_W.Color = LightColor.Red; Light_W.RemainingTime = 99;
        }

        private void ExtendYellowLight(int extra)
        {
            if (Light_N.Color == LightColor.Yellow) Light_N.RemainingTime += extra;
            if (Light_S.Color == LightColor.Yellow) Light_S.RemainingTime += extra;
            if (Light_E.Color == LightColor.Yellow) Light_E.RemainingTime += extra;
            if (Light_W.Color == LightColor.Yellow) Light_W.RemainingTime += extra;
        }

        private void ExtendGreenLight(int extra)
        {
            if (Light_N.Color == LightColor.Green) Light_N.RemainingTime += extra;
            if (Light_S.Color == LightColor.Green) Light_S.RemainingTime += extra;
            if (Light_E.Color == LightColor.Green) Light_E.RemainingTime += extra;
            if (Light_W.Color == LightColor.Green) Light_W.RemainingTime += extra;
        }

        #endregion
    }
}
