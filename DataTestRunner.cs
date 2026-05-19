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

			// 1. 기본 데이터 입력 테스트
			Console.WriteLine("테스트 시작: 데이터를 기록 중...");
			for (int i = 0; i < 10; i++)
			{
				manager.RecordData(CreateMockData(i));
			}

			// 2. 멀티스레드 테스트 (동시에 100개의 데이터 병렬기록)
			Parallel.For(0, 100, i =>
			{
				manager.RecordData(CreateMockData(i + 100));
			});

			// 3. 파일 저장 테스트 (로컬 디스크 저장 확인)
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string csvFile = Path.Combine(desktopPath, "SmartRoad_Test_Result.csv");

			bool isSaved = manager.SaveToCsv(csvFile);

			if (isSaved)
			{
				Console.WriteLine($"성공: {csvFile}에 데이터가 저장되었습니다.");
			}
			else
			{
				Console.WriteLine("실패: 파일 저장 중에 오류가 발생했습니다.");
			}
		}

		// 목데이터 클래스 샘플을 생성 및 검사 타입 데이터 설정
		private TrafficUpdateEventArgs CreateMockData(int index)
		{
			return new TrafficUpdateEventArgs
			{
				CurrentState = (index % 2 == 0) ? TrafficState.Normal : TrafficState.Congested,
				EfficiencyScore = 85 + (index % 15),
				SafetyScore = 90 - (index % 20),
				RawData = new SensorData
				{
					Temperature = 25.0,
					Rainfall = 0.0,
					RoadCondition = "건조",
					AvgSpeed_GPS = 60.0 + index
				}
			};
		}
	}
}