namespace Smart_Road
{
    public class SensorData
    {
        public double Temperature { get; init; }     // 기온 (°C)
        public double Rainfall { get; init; }        // 강수량 (mm/h)
        public double WindSpeed { get; init; }       // 풍속 (m/s)
        public string RoadCondition { get; init; }   // 건조/습윤/결빙/적설
        public int WaitingCars_N { get; init; }      // 북쪽 대기 차량 수
        public int WaitingCars_S { get; init; }
        public int WaitingCars_E { get; init; }
        public int WaitingCars_W { get; init; }
        public double VehicleSpeed { get; init; }    // 차량 진입 속도 (km/h)
        public bool IsWrongWay { get; init; }        // 역주행 여부
        public bool IsPedestrianRemaining { get; init; } // 잔류 보행자 여부
        public double AvgSpeed_GPS { get; init; }    // GPS 구간 평균 속도
    }
}