using System;

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
    }
}
