namespace Smart_Road
{
    // 신호등의 색상 상태를 나타내는 열거형
    // Red: 진행 불가, Yellow: 진행 주의, Green: 진행 가능
    public enum LightColor
    {
        // 정지 신호
        Red,
        // 주의 신호 (남은 시간 표시됨)
        Yellow,
        // 진행 신호
        Green
    }

    // 교차로의 개별 신호등(북/남/동/서) 상태를 나타내는 클래스
    // 색상과 남은 시간을 함께 관리하여 UI 표시 및 신호 제어 로직에 사용
    public class TrafficLight
    {
        // 현재 신호등의 색상 (기본값: Red)
        public LightColor Color { get; set; } = LightColor.Red;

        // 현재 신호의 남은 시간 (초 단위, 배속이 적용됨)
        // Yellow 상태에서는 3초, Green 상태에서는 30초 등 시간이 표시됨
        public int RemainingTime { get; set; } = 0;

        // 이벤트 핸들러나 데이터 저장 시 원본 신호등 객체를 복사하여 전달
        // 이를 통해 이후에 원본 객체의 상태가 변경되어도 복사본은 영향받지 않음
        // TrafficController와 DataManager 간 데이터 무결성 유지에 필수
        public TrafficLight Clone()
        {
            return new TrafficLight
            {
                Color = this.Color,
                RemainingTime = this.RemainingTime
            };
        }
    }
}
