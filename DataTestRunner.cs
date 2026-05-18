using System;
using System.Threading.Tasks;

namespace Smart_Road
{
	public class DataTestRunner
	{
		public void RunTest()
		{
			var manager = new DataManager();

			// 1. 정상 데이터 입력 테스트
			Console.WriteLine("테스트 시작: 데이터 기록 중...");
			for (int i = 0; i < 10; i++)
			{
				manager.RecordData(CreateMockData(i));
			}

			// 2. 동시성 테스트 (동시에 100개가 들어와도 버티는지)
			Parallel.For(0, 100, i =>
			{
				manager.RecordData(CreateMockData(i + 100));
			});

			// 3. 파일 저장 테스트 (경로 권한 및 예외 처리 검증)
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string csvFile = Path.Combine(desktopPath, "SmartRoad_Test_Result.csv");

			bool isSaved = manager.SaveToCsv(csvFile);

			if (isSaved)
			{
				Console.WriteLine($"성공: {csvFile}에 데이터가 저장되었습니다.");
			}
			else
			{
				Console.WriteLine("실패: 저장 로직에 오류가 발생했습니다.");
			}
		}

		// 팀원들의 클래스 구조를 흉내 낸 가짜 데이터 생성기
		private TrafficUpdateEventArgs CreateMockData(int index)
		{
			return new TrafficUpdateEventArgs
			{
				CurrentState = (index % 2 == 0) ? "Green" : "Red",
				EfficiencyScore = 85.5 + (index * 0.1),
				SafetyScore = 90.0 - (index * 0.05),
				RawData = new SensorData // SensorData.cs 기반
				{
					Temperature = 25.0,
					Rainfall = 0.0,
					RoadCondition = "Dry",
					AvgSpeed_GPS = 60.0 + index
				}
			};
		}
	}
}