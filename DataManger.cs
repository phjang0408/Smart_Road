using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Smart_Road
{
    public class DataManager
    {
        // 1. 스레드 동기화를 위한 락 객체 (여러 곳에서 동시에 접근할 때 터지는 것 방지)
        private readonly object _lock = new object();

        // 2. 메모리 상한선 설정 (예: 10,000개 이상 쌓이면 강제 저장 유도 등으로 확장 가능)
        private readonly List<TrafficUpdateEventArgs> _history = new List<TrafficUpdateEventArgs>();

        /// <summary>
        /// 데이터를 기록할 때 스레드 안전성(Thread-Safety) 보장
        /// </summary>
        public void RecordData(TrafficUpdateEventArgs data)
        {
            if (data == null) return;

            lock (_lock)
            {
                _history.Add(data);
            }
        }

        /// <summary>
        /// 파일 쓰기 실패에 대비한 예외 처리 및 디렉토리 자동 생성 포함
        /// </summary>
        public bool SaveToCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            try
            {
                // 경로가 없으면 생성
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 저장하는 동안 데이터가 추가되어 리스트가 변하는 것 방지 (복사본 생성)
                List<TrafficUpdateEventArgs> snapshot;
                lock (_lock)
                {
                    snapshot = _history.ToList();
                }

                if (snapshot.Count == 0) return false;

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("Timestamp,CurrentState,EfficiencyScore,SafetyScore,Temp,Rainfall,RoadCondition,AvgSpeed");
                    foreach (var item in snapshot)
                    {
                        // 날짜/시간 정보를 추가하여 데이터 식별력 강화
                        writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{item.CurrentState},{item.EfficiencyScore},{item.SafetyScore},{item.RawData.Temperature},{item.RawData.Rainfall},{item.RawData.RoadCondition},{item.RawData.AvgSpeed_GPS}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // 실패 원인을 로그로 남기거나 팀원에게 알릴 수 있도록 처리
                Console.WriteLine($"[Error] CSV 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 저장 후 메모리를 비워주는 기능을 분리하여 메모리 누수 방지
        /// </summary>
        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }
    }
}