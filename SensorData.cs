public class SensorData
{
    public double Temperature { get; set; }     // 기온 (°C)
    public double Rainfall { get; set; }        // 강수량 (mm/h)
    public double WindSpeed { get; set; }       // 풍속 (m/s)
    public string RoadCondition { get; set; }   // 건조/습윤/결빙/적설
    public int WaitingCars_N { get; set; }      // 북쪽 대기 차량 수
    public int WaitingCars_S { get; set; }
    public int WaitingCars_E { get; set; }
    public int WaitingCars_W { get; set; }
    public double VehicleSpeed { get; set; }    // 차량 진입 속도 (km/h)
    public bool IsWrongWay { get; set; }        // 역주행 여부
    public bool IsPedestrianRemaining { get; set; } // 잔류 보행자 여부
    public double AvgSpeed_GPS { get; set; }    // GPS 구간 평균 속도
}