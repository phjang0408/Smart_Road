namespace Smart_Road
{
    // 신호등 색상
    public enum LightColor
    {
        Red,
        Yellow,
        Green
    }

    // 개별 방향의 신호등 상태를 담는 클래스
    public class TrafficLight
    {
        public LightColor Color { get; set; } = LightColor.Red;
        public int RemainingTime { get; set; } = 0;

        // 참조 오염 방지를 위한 깊은 복사(Deep Copy) 메서드
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
