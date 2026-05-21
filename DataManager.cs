using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Smart_Road
{
    public class DataManager
    {
        // 1. ������ ����ȭ�� ���� �� ��ü (���� ������ ���ÿ� ������ �� ������ �� ����)
        private readonly object _lock = new object();

        // 2. �޸� ���Ѽ� ���� (��: 10,000�� �̻� ���̸� ���� ���� ���� ������ Ȯ�� ����)
        private readonly List<TrafficUpdateEventArgs> _history = new List<TrafficUpdateEventArgs>();

        /// <summary>
        /// �����͸� ����� �� ������ ������(Thread-Safety) ����
        /// </summary>
        public void RecordData(TrafficUpdateEventArgs data)
        {
            if (data == null) return;

            var snapshot = new TrafficUpdateEventArgs
            {
                Timestamp = data.Timestamp,
                CurrentState = data.CurrentState,
                EfficiencyScore = data.EfficiencyScore,
                SafetyScore = data.SafetyScore,
                RawData = data.RawData,

                // Clone() 사용
                Light_N = data.Light_N?.Clone(),
                Light_S = data.Light_S?.Clone(),
                Light_E = data.Light_E?.Clone(),
                Light_W = data.Light_W?.Clone()
            };

            lock (_lock)
            {
                _history.Add(snapshot);
            }
        }

        /// <summary>
        /// ���� ���� ���п� ����� ���� ó�� �� ���丮 �ڵ� ���� ����
        /// </summary>
        public bool SaveToCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            try
            {
                // ��ΰ� ������ ����
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // �����ϴ� ���� �����Ͱ� �߰��Ǿ� ����Ʈ�� ���ϴ� �� ���� (���纻 ����)
                List<TrafficUpdateEventArgs> snapshot;
                lock (_lock)
                {
                    snapshot = _history.ToList();
                }

                if (snapshot.Count == 0) return false;

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("Timestamp,CurrentState,EfficiencyScore,SafetyScore,Temperature,Rainfall,WindSpeed,RoadCondition,VehicleSpeed,IsWrongWay,IsPedestrianRemaining,AvgSpeed_GPS,Signal_N,Signal_S,Signal_E,Signal_W");
                    foreach (var item in snapshot)
                    {
                        string signalN = item.Light_N?.Color.ToString() ?? "Red";
                        string signalS = item.Light_S?.Color.ToString() ?? "Red";
                        string signalE = item.Light_E?.Color.ToString() ?? "Red";
                        string signalW = item.Light_W?.Color.ToString() ?? "Red";

                        writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{item.CurrentState},{item.EfficiencyScore},{item.SafetyScore}," +
                            $"{item.RawData.Temperature},{item.RawData.Rainfall},{item.RawData.WindSpeed},{item.RawData.RoadCondition}," +
                            $"{item.RawData.VehicleSpeed},{item.RawData.IsWrongWay},{item.RawData.IsPedestrianRemaining},{item.RawData.AvgSpeed_GPS}," +
                            $"{signalN},{signalS},{signalE},{signalW}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // ���� ������ �α׷� ����ų� �������� �˸� �� �ֵ��� ó��
                Console.WriteLine($"[Error] CSV ���� ����: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 하루 시뮬레이션 완료 시 자동 저장 (날짜별 폴더 생성)
        /// </summary>
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

        /// <summary>
        /// 하루 시뮬레이션 후 history 초기화
        /// </summary>
        public void ResetForNewDay()
        {
            ClearHistory();
        }

        /// <summary>
        /// ���� �� �޸𸮸� ����ִ� ����� �и��Ͽ� �޸� ���� ����
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