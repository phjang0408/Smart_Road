namespace Smart_Road
{
    // DataGenerator에서 생성하는 센서 데이터를 담는 클래스
    // init 속성을 사용하여 불변성 확보 (생성 후 변경 불가)
    // TrafficController와 UI에서 신호 제어 및 표시에 필요한 모든 센서 정보 포함
    public class SensorData
    {
        // 현재 시뮬레이션 시각의 기온 (섭씨 온도, 범위: -10 ~ 40도)
        // 노면 상태 결정에 영향 (낮을수록 결빙/적설 가능성 높음)
        public double Temperature { get; init; }

        // 시간당 강수량 (mm/h, 범위: 0 ~ 50)
        // 기온과 함께 노면 상태 결정 (강수량이 많으면 습윤 또는 결빙)
        public double Rainfall { get; init; }

        // 풍속 (초속 미터, 범위: 0 ~ 15)
        // 현재는 노면 상태 계산에 직접 사용되지 않으나 기상 데이터 기록 목적
        public double WindSpeed { get; init; }

        // 현재 도로의 노면 상태 (Dry/Wet/Icy/Snow)
        // 기온과 강수량에 따라 자동 결정됨 (TrafficController에서 안전성 점수 계산에 사용)
        public RoadConditionType RoadCondition { get; init; }

        // 북쪽 교차로에서 신호 대기 중인 차량 수
        public int WaitingCars_N { get; init; }
        // 남쪽 교차로에서 신호 대기 중인 차량 수
        public int WaitingCars_S { get; init; }
        // 동쪽 교차로에서 신호 대기 중인 차량 수
        public int WaitingCars_E { get; init; }
        // 서쪽 교차로에서 신호 대기 중인 차량 수
        public int WaitingCars_W { get; init; }

        // 교차로 진입 차량의 속도 (km/h)
        // 실제로는 지정된 확률 범위 내의 임의 값으로 생성됨
        public double VehicleSpeed { get; init; }

        // 역주행 감지 여부 (true: 역주행 감지, false: 정상)
        // 감지 시 즉시 긴급 상태 활성화됨 (모든 신호등 RED)
        public bool IsWrongWay { get; init; }

        // 교차로 내 잔류 보행자 감지 여부 (true: 보행자 있음, false: 없음)
        // 감지 시 Green 신호를 10초 추가 연장하여 보행자 안전 확보
        public bool IsPedestrianRemaining { get; init; }

        // GPS 기반 구간 평균 속도 (km/h)
        // 지자기 센서의 대기 차량 수 데이터로부터 역함수 계산 (차량 많음 = 속도 낮음)
        // 시뮬레이션 시간과 무관하게 대기 차량 수에만 의존
        public double AvgSpeed_GPS { get; init; }
    }
}