using System;

namespace Smart_Road
{
    // 외부로 전달할 데이터 보따리
    public class TrafficUpdateEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; } // 기록 시점의 실제 시간 저장
        public TrafficState CurrentState { get; set; }
        public int EfficiencyScore { get; set; }
        public int SafetyScore { get; set; }
        public TrafficLight Light_N { get; set; }
        public TrafficLight Light_S { get; set; }
        public TrafficLight Light_E { get; set; }
        public TrafficLight Light_W { get; set; }
        public SensorData RawData { get; set; } 
    }
}
