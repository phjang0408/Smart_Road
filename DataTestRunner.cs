using System;
using System.IO;
using System.Threading.Tasks;

namespace Smart_Road
{
    public class DataTestRunner
    {
        public void RunTest()
        {
            var manager = new DataManager();

            // 1. 정상적으로 데이터가 입력되는지 확인하는 테스트
            Console.WriteLine("테스트 시작: 데이터를 기록 중...");
            for (int i = 0; i < 10; i++)
            {
                manager.RecordData(CreateMockData(i));
            }

            // 2. 여러 스레드에서 동시에 접근해도 에러가 안 나는지 검증 (동기화 테스트)
            Parallel.For(0, 100, i =>
            {
                manager.RecordData(CreateMockData(i + 100));
            });

            // 3. 누적된 데이터를 실제 바탕화면에 파일로 생성해 보는 테스트
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string csvFile = Path.Combine(desktopPath, "SmartRoad_Test_Result.csv");

            SaveResult saveResult = manager.SaveToCsv(csvFile);

            if (saveResult.Success)
            {
                Console.WriteLine($"성공: {saveResult.FilePath}에 데이터가 저장되었습니다.");
            }
            else
            {
                Console.WriteLine($"실패: {saveResult.Message}");
            }
        }

        // 실제 팀원들이 넘겨주는 것과 똑같은 형태의 가짜(Mock) 데이터를 만드는 기능
        private TrafficUpdateEventArgs CreateMockData(int index)
        {
            return new TrafficUpdateEventArgs
            {
                Timestamp = DateTime.Now,
                CurrentState = (index % 2 == 0) ? TrafficState.Normal : TrafficState.Congested,
                EfficiencyScore = 85 + (index % 15),
                SafetyScore = 90 - (index % 20),

                // 기존 테스트 코드에서 누락되어서 에러를 내던 신호등 객체 강제 초기화
                Light_N = new TrafficLight { Color = LightColor.Green, RemainingTime = 30 },
                Light_S = new TrafficLight { Color = LightColor.Green, RemainingTime = 30 },
                Light_E = new TrafficLight { Color = LightColor.Red, RemainingTime = 0 },
                Light_W = new TrafficLight { Color = LightColor.Red, RemainingTime = 0 },

                // 센서 데이터 묶음 초기화
                RawData = new SensorData
                {
                    Temperature = 25.0,
                    Rainfall = 0.0,
                    WindSpeed = 2.5,
                    RoadCondition = RoadConditionType.Dry,
                    WaitingCars_N = 5,
                    WaitingCars_S = 3,
                    WaitingCars_E = 8,
                    WaitingCars_W = 2,
                    VehicleSpeed = 45.5,
                    IsWrongWay = false,
                    IsPedestrianRemaining = false,
                    AvgSpeed_GPS = 60.0 + index
                }
            };
        }
    }
}