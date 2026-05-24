using System;

namespace Smart_Road
{
    // TrafficController에서 신호 상태가 변경될 때마다 전달하는 이벤트 인자 클래스
    // MainWindow의 UI 업데이트와 DataManager의 데이터 기록에 필요한 모든 정보를 포함
    // 이 객체는 TrafficUpdated 이벤트를 통해 구독자들에게 전파됨
    public class TrafficUpdateEventArgs : EventArgs
    {
        // 이 신호 상태 변경이 발생한 시뮬레이션 시간 (실제 DateTime이 아닌 시뮬레이션 내부 시간)
        public DateTime Timestamp { get; set; }

        // 현재 교통 상태 (정상/혼잡/환경위험/긴급)
        public TrafficState CurrentState { get; set; }

        // 교통 효율성 점수 (0~100, 높을수록 좋음)
        public int EfficiencyScore { get; set; }

        // 도로 안전성 점수 (높을수록 위험)
        public int SafetyScore { get; set; }

        // 북쪽 신호등의 색상 및 남은 시간
        public TrafficLight Light_N { get; set; }
        // 남쪽 신호등의 색상 및 남은 시간
        public TrafficLight Light_S { get; set; }
        // 동쪽 신호등의 색상 및 남은 시간
        public TrafficLight Light_E { get; set; }
        // 서쪽 신호등의 색상 및 남은 시간
        public TrafficLight Light_W { get; set; }

        // 현재 시점의 센서 데이터 (기온, 강수량, 노면상태, 대기 차량 수 등)
        public SensorData RawData { get; set; }
    }
}
