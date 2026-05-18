using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Smart_Road
{
    /// <summary>
    /// 센서 및 교통 데이터를 파일로 저장하는 관리 클래스
    /// </summary>
    public class DataManager
    {
        // 실시간으로 들어오는 데이터들을 차곡차곡 쌓아두는 저장소(리스트)
        private List<TrafficUpdateEventArgs> _history = new List<TrafficUpdateEventArgs>();

        /// <summary>
        /// 새로운 데이터를 리스트에 기록함 (1초마다 호출 예정)
        /// </summary>
        public void RecordData(TrafficUpdateEventArgs data)
        {
            _history.Add(data);
        }

        /// <summary>
        /// 쌓인 데이터를 JSON 파일 형식으로 저장
        /// </summary>
        public void SaveToJson(string filePath)
        {
            // 읽기 편하도록 들여쓰기 설정 추가
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(_history, options);

            // 파일 생성 및 쓰기
            File.WriteAllText(filePath, jsonString);
        }

        /// <summary>
        /// 쌓인 데이터를 CSV(엑셀 호환) 파일 형식으로 저장
        /// </summary>
        public void SaveToCsv(string filePath)
        {
            // 스트림라이터를 열어서 파일 쓰기 시작
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // 1행: 엑셀에서 제목이 될 헤더 부분 작성
                writer.WriteLine("CurrentState,EfficiencyScore,SafetyScore,Temp,Rainfall,RoadCondition,AvgSpeed");

                // 리스트를 돌면서 데이터 한 줄씩 뽑아내기
                foreach (var item in _history)
                {
                    // 각 항목을 쉼표(,)로 구분해서 한 줄로 합쳐서 작성
                    writer.WriteLine($"{item.CurrentState},{item.EfficiencyScore},{item.SafetyScore},{item.RawData.Temperature},{item.RawData.Rainfall},{item.RawData.RoadCondition},{item.RawData.AvgSpeed_GPS}");
                }
            }
        }
    }
}