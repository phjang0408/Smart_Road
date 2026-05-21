using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Smart_Road
{
    public class DataManager
    {
        // 스레드 동기화를 위한 락 객체 (여러 곳에서 동시에 리스트에 접근하는 것을 방지)
        private readonly object _lock = new object();

        // 메모리에 실시간 데이터를 쌓아두는 리스트
        private readonly List<TrafficUpdateEventArgs> _history = new List<TrafficUpdateEventArgs>();

        // 1초마다 발생하는 교통 데이터를 리스트에 안전하게 기록하는 기능
        public void RecordData(TrafficUpdateEventArgs data)
        {
            if (data == null) return;

            lock (_lock)
            {
                _history.Add(data);
            }
        }

        // 누적된 데이터를 CSV 파일로 저장하는 기능
        public bool SaveToCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            try
            {
                // 저장할 폴더가 없으면 자동으로 생성
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 저장하는 동안 리스트가 변경되지 않도록 복사본 생성
                List<TrafficUpdateEventArgs> snapshot;
                lock (_lock)
                {
                    snapshot = _history.ToList();
                }

                if (snapshot.Count == 0) return false;

                using (var writer = new StreamWriter(filePath))
                {
                    // CSV 헤더 (엑셀 첫 줄 항목 이름)
                    writer.WriteLine("Timestamp,CurrentState,EfficiencyScore,SafetyScore,Temperature,Rainfall,WindSpeed,RoadCondition,VehicleSpeed,IsWrongWay,IsPedestrianRemaining,AvgSpeed_GPS,Signal_N,Signal_S,Signal_E,Signal_W");

                    foreach (var item in snapshot)
                    {
                        // 널(Null) 방지 처리: 팀원 코드가 값을 안 넘겨줘도 프로그램이 안 뻗게 기본값 세팅
                        string signalN = item.Light_N?.Color.ToString() ?? "None";
                        string signalS = item.Light_S?.Color.ToString() ?? "None";
                        string signalE = item.Light_E?.Color.ToString() ?? "None";
                        string signalW = item.Light_W?.Color.ToString() ?? "None";

                        double temp = item.RawData?.Temperature ?? 0;
                        double rain = item.RawData?.Rainfall ?? 0;
                        double wind = item.RawData?.WindSpeed ?? 0;
                        string road = item.RawData?.RoadCondition ?? "Unknown";
                        double vSpeed = item.RawData?.VehicleSpeed ?? 0;
                        bool wrongWay = item.RawData?.IsWrongWay ?? false;
                        bool ped = item.RawData?.IsPedestrianRemaining ?? false;
                        double gpsSpeed = item.RawData?.AvgSpeed_GPS ?? 0;

                        // 시간 포맷을 맞춰서 콤마로 구분해 한 줄씩 작성
                        writer.WriteLine($"{item.Timestamp:yyyy-MM-dd HH:mm:ss},{item.CurrentState},{item.EfficiencyScore},{item.SafetyScore}," +
                            $"{temp},{rain},{wind},{road},{vSpeed},{wrongWay},{ped},{gpsSpeed}," +
                            $"{signalN},{signalS},{signalE},{signalW}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] CSV 저장 실패: {ex.Message}");
                return false;
            }
        }

        // 시뮬레이션 하루 치(24틱)가 끝나면 바탕화면에 날짜별 폴더를 만들고 자동 저장하는 기능
        public bool AutoSaveDay()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string dataRootPath = Path.Combine(desktopPath, "SmartRoad_Data");
            string dateFolderPath = Path.Combine(dataRootPath, DateTime.Now.ToString("yyyy-MM-dd"));

            if (!Directory.Exists(dateFolderPath))
            {
                Directory.CreateDirectory(dateFolderPath);
            }

            string fileName = $"SmartRoad_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            string filePath = Path.Combine(dateFolderPath, fileName);

            return SaveToCsv(filePath);
        }

        // 데이터 저장이 끝난 후, 다음 저장을 위해 메모리를 비우는 기능
        public void ResetForNewDay()
        {
            ClearHistory();
        }

        // 리스트를 완전히 비우는 내부 기능
        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }
    }
}