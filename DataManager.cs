using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Smart_Road
{
    // 교통 신호 제어 시뮬레이션의 모든 데이터를 기록하고 저장하는 매니저 클래스
    // 시뮬레이션 중 발생하는 신호 상태 변화와 센서 데이터를 누적 기록하고
    // CSV 파일로 내보내거나 자동 저장하는 기능을 제공
    public class DataManager
    {
        // 여러 스레드에서 동시에 접근해도 안전하도록 스레드 동기화를 위한 잠금 객체
        private readonly object _lock = new object();
        // 시뮬레이션 전체 기간 동안 발생한 모든 신호 제어 이벤트를 기록하는 리스트
        private readonly List<TrafficUpdateEventArgs> _history = new List<TrafficUpdateEventArgs>();

        // 신호 제어 이벤트 발생 시 호출되어 현재 상태를 메모리에 기록
        // 같은 TrafficUpdateEventArgs 객체를 여러 곳에서 참조할 수 있으므로
        // Clone()을 사용하여 스냅샷을 생성하고 저장 (나중에 원본이 변경되는 것 방지)
        public void RecordData(TrafficUpdateEventArgs data)
        {
            if (data == null) return;

            // 현재 이벤트의 스냅샷 생성 (신호등 객체는 Clone으로 복사)
            var snapshot = new TrafficUpdateEventArgs
            {
                Timestamp = data.Timestamp,
                CurrentState = data.CurrentState,
                EfficiencyScore = data.EfficiencyScore,
                SafetyScore = data.SafetyScore,
                RawData = data.RawData,
                Light_N = data.Light_N?.Clone(),
                Light_S = data.Light_S?.Clone(),
                Light_E = data.Light_E?.Clone(),
                Light_W = data.Light_W?.Clone()
            };

            // 스레드 안전성을 위해 잠금을 걸고 히스토리에 추가
            lock (_lock)
            {
                _history.Add(snapshot);
            }
        }

        // 메모리에 저장된 모든 신호 제어 데이터를 CSV 파일로 내보내기
        // 지정된 경로에 파일을 생성하고, 각 시간의 신호 상태와 센서 데이터를 기록
        // CSV 형식: 시간, 신호 상태, 효율점수, 안전점수, 기온, 강수량, 풍속, 노면상태, 등등
        public SaveResult SaveToCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return new SaveResult(false, "저장 경로가 지정되지 않았습니다.");

            try
            {
                // 필요시 경로의 디렉토리 자동 생성
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 스레드 안전하게 현재까지 기록된 데이터의 스냅샷 획득
                List<TrafficUpdateEventArgs> snapshot;
                lock (_lock)
                {
                    snapshot = _history.ToList();
                }

                if (snapshot.Count == 0)
                    return new SaveResult(false, "저장할 데이터가 없습니다.");

                // CSV 파일 작성
                using (var writer = new StreamWriter(filePath))
                {
                    // CSV 헤더 작성 (시간, 신호상태, 점수, 센서 데이터, 신호등 상태)
                    writer.WriteLine("Timestamp,CurrentState,EfficiencyScore,SafetyScore,Temperature,Rainfall,WindSpeed,RoadCondition,VehicleSpeed,IsWrongWay,IsPedestrianRemaining,AvgSpeed_GPS,Signal_N,Signal_S,Signal_E,Signal_W");

                    // 기록된 각 이벤트를 행으로 변환하여 작성
                    foreach (var item in snapshot)
                    {
                        // 4개 방향 신호등의 색상 상태를 문자열로 변환
                        string signalN = item.Light_N?.Color.ToString() ?? "None";
                        string signalS = item.Light_S?.Color.ToString() ?? "None";
                        string signalE = item.Light_E?.Color.ToString() ?? "None";
                        string signalW = item.Light_W?.Color.ToString() ?? "None";

                        // 센서 데이터 추출 (null 처리)
                        double temp = item.RawData?.Temperature ?? 0;
                        double rain = item.RawData?.Rainfall ?? 0;
                        double wind = item.RawData?.WindSpeed ?? 0;
                        string road = item.RawData?.RoadCondition.ToString() ?? "Unknown";
                        double vSpeed = item.RawData?.VehicleSpeed ?? 0;
                        bool wrongWay = item.RawData?.IsWrongWay ?? false;
                        bool ped = item.RawData?.IsPedestrianRemaining ?? false;
                        double gpsSpeed = item.RawData?.AvgSpeed_GPS ?? 0;

                        // CSV 행 작성
                        writer.WriteLine($"\"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\",{item.CurrentState},{item.EfficiencyScore},{item.SafetyScore}," +
                            $"{temp},{rain},{wind},{road},{vSpeed},{wrongWay},{ped},{gpsSpeed}," +
                            $"{signalN},{signalS},{signalE},{signalW}");
                    }
                }
                return new SaveResult(true, $"CSV 저장 성공 ({snapshot.Count}건)", filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[Error] CSV 저장 실패 (권한 없음): {ex.Message}");
                return new SaveResult(false, $"저장 권한이 없습니다. 폴더 접근 권한을 확인하세요.\n상세: {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Error] CSV 저장 실패 (IO 오류): {ex.Message}");
                return new SaveResult(false, $"파일 저장 중 오류가 발생했습니다. 디스크 용량 또는 경로를 확인하세요.\n상세: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] CSV 저장 실패: {ex.Message}");
                return new SaveResult(false, $"예기치 않은 오류가 발생했습니다.\n상세: {ex.Message}");
            }
        }

        // 시뮬레이션이 끝난 후 자동으로 데이터를 바탕화면 SmartRoad_Data 폴더에 저장
        // 오늘 날짜 폴더 내에 현재 시간을 파일명으로 하는 CSV 파일 생성
        // 하루 여러 번 시뮬레이션을 돌려도 모두 같은 날짜 폴더에 저장됨
        public SaveResult AutoSaveDay()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string dataRootPath = Path.Combine(desktopPath, "SmartRoad_Data");
            // 오늘 날짜를 기준으로 폴더 생성 (예: 2026-05-24)
            string dateFolderPath = Path.Combine(dataRootPath, DateTime.Now.ToString("yyyy-MM-dd"));

            try
            {
                // 필요시 날짜별 폴더 자동 생성
                if (!Directory.Exists(dateFolderPath))
                {
                    Directory.CreateDirectory(dateFolderPath);
                }
            }
            catch (IOException ex)
            {
                return new SaveResult(false, $"폴더 생성 실패: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return new SaveResult(false, $"폴더 접근 권한 없음: {ex.Message}");
            }

            // 파일명은 년-월-일_시-분-초 형식으로 생성 (같은 날짜에 여러 파일 구분 가능)
            string fileName = $"SmartRoad_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            string filePath = Path.Combine(dateFolderPath, fileName);

            return SaveToCsv(filePath);
        }

        // 메모리에 기록된 모든 데이터를 삭제 (새로운 시뮬레이션 시작 시 호출)
        // 스레드 안전성을 위해 잠금을 걸고 리스트를 초기화
        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }
    }
}