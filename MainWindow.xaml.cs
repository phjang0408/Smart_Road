using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Smart_Road
{
    // 메인 UI 윈도우
    // 교차로 시뮬레이션을 시각화하고 사용자 입력을 처리
    // DataGenerator, TrafficController, DataManager와 연동
    public partial class MainWindow : Window
    {
        #region 핵심 모듈 및 변수

        // 센서 데이터를 생성하는 모듈
        private DataGenerator _dataGenerator;
        // 신호를 제어하는 모듈
        private TrafficController _trafficController;
        // 시뮬레이션 데이터를 저장하는 모듈
        private DataManager _dataManager;

        private TrafficUpdateEventArgs _lastTrafficArgs;
        private SensorData _currentData;

        private int _currentSafetyScore = 0;
        private int _currentEfficiencyScore = 100;

        private DateTime _lastTickTime = DateTime.Now;
        
        private bool _forceBlackIce = false;
        private int _emergencyTimer = 0;
        private bool _isRushHourEnabled = true;
        private bool _isDayRunning = false;

        #endregion

        #region 렌더링 및 오브젝트 풀링 변수

        private DispatcherTimer _renderTimer;
        private const int MAX_CARS = 40;
        private CarUI[] _carPool = new CarUI[MAX_CARS];

        // 매 프레임마다 리스트 재활용 (GC 부담 줄임)
        private readonly List<CarUI> _activeN = new List<CarUI>(MAX_CARS);
        private readonly List<CarUI> _activeS = new List<CarUI>(MAX_CARS);
        private readonly List<CarUI> _activeE = new List<CarUI>(MAX_CARS);
        private readonly List<CarUI> _activeW = new List<CarUI>(MAX_CARS);

        // 보행자 표시 텍스트 (파란색 보행자 신호)
        private TextBlock _pedestrianN, _pedestrianS, _pedestrianE, _pedestrianW;

        // 정지선 및 안전거리 상수화
        private const int STOP_LINE_N = 170;
        private const int STOP_LINE_S = 310;
        private const int STOP_LINE_E = 310;
        private const int STOP_LINE_W = 170;
        private const int CAR_FOLLOW_DISTANCE = 25;

        // 속도 및 쿨다운 제어
        private int _currentSpeed = 1;
        private double _baseMoveSpeed = 2.0;
        private int _baseSpawnCooldown = 20;
        private double _policySpeedMultiplier = 1.0;

        private int _spawnCooldownN = 0, _spawnCooldownS = 0;
        private int _spawnCooldownE = 0, _spawnCooldownW = 0;

        #endregion

        #region UI 리소스 캐싱

        private List<double> _temperatureHistory = new List<double>();
        private List<double> _rainfallHistory = new List<double>();

        // 그래프 관련 필드
        private Polyline _tempPolyline, _rainPolyline;
        private PointCollection _tempPoints, _rainPoints;
        // 그래프 고정 범위 (더 안정적인 스케일링)
        private const double GRAPH_MIN_TEMP = -10;
        private const double GRAPH_MAX_TEMP = 40;
        private const double GRAPH_MAX_RAINFALL = 50;
        // 그래프 패딩 (줄여서 더 큰 그래프 영역 확보)
        private const double GRAPH_PADDING = 30;

        // Brush 객체 반복 생성을 막기 위한 전역 캐싱
        private readonly Brush _brushGraphAxis = (Brush)new BrushConverter().ConvertFrom("#505070");
        private readonly Brush _brushGraphLabel = (Brush)new BrushConverter().ConvertFrom("#909090");
        private readonly Brush _brushGraphTemp = (Brush)new BrushConverter().ConvertFrom("#64B5F6");
        private readonly Brush _brushGraphRain = (Brush)new BrushConverter().ConvertFrom("#81C784");

        private readonly Brush _brushRed = (Brush)new BrushConverter().ConvertFrom("#FF3B30");
        private readonly Brush _brushGreen = (Brush)new BrushConverter().ConvertFrom("#4CAF50");
        private readonly Brush _brushYellow = (Brush)new BrushConverter().ConvertFrom("#FFCC00");
        private readonly Brush _brushWhite = Brushes.White;
        private readonly Brush _brushInactive = (Brush)new BrushConverter().ConvertFrom("#2D2D44");
        private readonly Brush _dimRed = (Brush)new BrushConverter().ConvertFrom("#33FF3B30");
        private readonly Brush _dimGreen = (Brush)new BrushConverter().ConvertFrom("#334CAF50");
        private readonly Brush _bgDry = (Brush)new BrushConverter().ConvertFrom("#1E1E2E");
        private readonly Brush _bgWet = (Brush)new BrushConverter().ConvertFrom("#1A3A5A");
        private readonly Brush _bgIce = (Brush)new BrushConverter().ConvertFrom("#1B404E");
        private readonly Brush _bgSnow = (Brush)new BrushConverter().ConvertFrom("#454555");
        private readonly Brush _textWet = (Brush)new BrushConverter().ConvertFrom("#82B1FF");
        private readonly Brush _textIce = (Brush)new BrushConverter().ConvertFrom("#84FFFF");

        // 제어 모드 배경 Brush 캐싱 (UpdateUI 인라인 생성 제거)
        private readonly Brush _bgControlIce = (Brush)new BrushConverter().ConvertFrom("#102020");
        private readonly Brush _bgControlEmergency = (Brush)new BrushConverter().ConvertFrom("#201010");
        private readonly Brush _bgControlCongested = (Brush)new BrushConverter().ConvertFrom("#202010");
        private readonly Brush _bgControlNormal = (Brush)new BrushConverter().ConvertFrom("#202030");
        private readonly Brush _borderControlNormal = (Brush)new BrushConverter().ConvertFrom("#505070");
        private readonly Brush _brushOrange = (Brush)new BrushConverter().ConvertFrom("#FF9500");

        // 차량 UI 클래스
        private class CarUI
        {
            public Rectangle Shape { get; set; }
            public TranslateTransform Transform { get; set; }
            public string Direction { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public bool IsActive { get; set; }
        }

        #endregion

        #region 초기화 및 API화

        public MainWindow()
        {
            InitializeComponent();
            InitializeCarPool();
            InitializeSystem();
            InitializeRenderLoop();
            UpdateSpeedButtonsUI();
            new DataTestRunner().RunTest(); //데이터 저장 잘되는지 가짜데이터로 테스트
        }

        // 교통 효율 점수에 따른 차량 속도 조정
        // 혼잡도에 따라 차량의 통과 속도를 동적으로 조정
        public void ApplyTrafficPolicy(double speedMultiplier) { _policySpeedMultiplier = speedMultiplier; }

        // 역주행 감지 시 긴급 정지 트리거
        // 역주행이 감지되면 3초간 모든 신호를 빨강으로 설정하고 차량 진입을 차단
        public void TriggerEmergencyStop() {
            // 중복 트리거 방지 (이미 긴급 상태가 아닐 때만 실행)
            if (_emergencyTimer == 0) {
                // 긴급 정지 시간 설정 (30FPS이므로 90 프레임 = 3초)
                _emergencyTimer = 90;
                // TrafficController에 강제 상태 전달
                if (_trafficController != null) _trafficController.ForceWrongWay = true;
                // UI 업데이트
                UpdateUI();
            }
        }

        // 블랙아이스 모드 수동 전환
        // 사용자가 수동으로 결빙 상태를 강제 활성화/해제
        public void SetBlackIceMode(bool isActive) {
            _forceBlackIce = isActive;
            // TrafficController에 상태 전달
            if (_trafficController != null) _trafficController.ForceBlackIce = isActive;
            // UI 업데이트
            UpdateUI();
        }

        /// <summary>
        /// 오브젝트 풀링 초기화
        /// </summary>
        private void InitializeCarPool()
        {
            for (int i = 0; i < MAX_CARS; i++)
            {
                var transform = new TranslateTransform();
                var rect = new Rectangle { Fill = _brushWhite, RadiusX = 3, RadiusY = 3, Visibility = Visibility.Hidden, RenderTransform = transform };
                canvasIntersection.Children.Add(rect);
                _carPool[i] = new CarUI { Shape = rect, Transform = transform, IsActive = false };
            }

            // 보행자 표시 아이콘 초기화 (4개 방향 횡단보도)
            InitializePedestrianIndicators();
        }

        /// <summary>
        /// 보행자 신호 표시 초기화 (4개 방향 횡단보도에 파란색 텍스트로 표시)
        /// </summary>
        private void InitializePedestrianIndicators()
        {
            // 파란색 보행자 신호용 브러시
            SolidColorBrush brushPedestrianBlue = new SolidColorBrush(Color.FromRgb(0, 150, 255));

            // 북쪽 횡단보도 (Y=140, X=240)
            _pedestrianN = new TextBlock { Text = "보행자", Foreground = brushPedestrianBlue, FontSize = 11, FontWeight = System.Windows.FontWeights.Bold, Visibility = Visibility.Hidden };
            Canvas.SetLeft(_pedestrianN, 210); Canvas.SetTop(_pedestrianN, 125);
            canvasIntersection.Children.Add(_pedestrianN);

            // 남쪽 횡단보도 (Y=360, X=240)
            _pedestrianS = new TextBlock { Text = "보행자", Foreground = brushPedestrianBlue, FontSize = 11, FontWeight = System.Windows.FontWeights.Bold, Visibility = Visibility.Hidden };
            Canvas.SetLeft(_pedestrianS, 210); Canvas.SetTop(_pedestrianS, 355);
            canvasIntersection.Children.Add(_pedestrianS);

            // 동쪽 횡단보도 (X=360, Y=240)
            _pedestrianE = new TextBlock { Text = "보행자", Foreground = brushPedestrianBlue, FontSize = 11, FontWeight = System.Windows.FontWeights.Bold, Visibility = Visibility.Hidden };
            Canvas.SetLeft(_pedestrianE, 355); Canvas.SetTop(_pedestrianE, 235);
            canvasIntersection.Children.Add(_pedestrianE);

            // 서쪽 횡단보도 (X=140, Y=240)
            _pedestrianW = new TextBlock { Text = "보행자", Foreground = brushPedestrianBlue, FontSize = 11, FontWeight = System.Windows.FontWeights.Bold, Visibility = Visibility.Hidden };
            Canvas.SetLeft(_pedestrianW, 105); Canvas.SetTop(_pedestrianW, 235);
            canvasIntersection.Children.Add(_pedestrianW);
        }

        // UI와 핵심 모듈들을 연동
        // DataGenerator, TrafficController, DataManager를 생성하고
        // 이들의 이벤트를 UI 이벤트 핸들러에 연결
        private void InitializeSystem()
        {
            // 센서 데이터 생성 모듈 생성
            _dataGenerator = new DataGenerator();
            // 신호 제어 모듈 생성 (DataGenerator와 연동)
            _trafficController = new TrafficController(_dataGenerator);
            // 데이터 저장 모듈 생성
            _dataManager = new DataManager();

            // TrafficController의 업데이트 이벤트 구독
            _trafficController.TrafficUpdated += HandleTrafficUpdate;

            // 센서 데이터 업데이트 이벤트 구독
            // DataGenerator가 새 센서 데이터를 생성할 때마다 호출되어 UI를 갱신
            _dataGenerator.SensorDataUpdated += (data) =>
            {
                // UI 스레드에서 실행 (WPF UI는 단일 스레드)
                this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 현재 센서 데이터 저장
                        _currentData = data;
                        // 그래프 데이터 누적
                        _temperatureHistory.Add(data.Temperature);
                        _rainfallHistory.Add(data.Rainfall);

                        // 역주행 감지 시 긴급 정지 트리거
                        if (data.IsWrongWay) TriggerEmergencyStop();

                        // UI 업데이트 (텍스트, 색상 등)
                        UpdateUI();
                        // 그래프에 새로운 데이터 포인트 추가
                        AddGraphPoint(data.Temperature, data.Rainfall);
                        // 그래프 통계 업데이트
                        UpdateGraphStats();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[UI] 센서 업데이트 오류: {ex.Message}"); }
                });
            };

            _dataGenerator.DayCompleted += OnDayCompleted;
            _dataGenerator.Initialize();
        }

        /// <summary>
        /// TrafficController의 결정 사항 수신
        /// </summary>
        private void HandleTrafficUpdate(object sender, TrafficUpdateEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    _lastTrafficArgs = e;
                    _currentSafetyScore = e.SafetyScore;
                    _currentEfficiencyScore = e.EfficiencyScore;

                    if (e.RawData != null)
                    {
                        _dataManager.RecordData(e);     // 데이터 저장
                    }

                    // 점수가 낮으면 통과 속도 감속 (혼잡 반영)
                    double speedMultiplier = e.EfficiencyScore < 60 ? 0.8 : 1.0;
                    ApplyTrafficPolicy(speedMultiplier);

                    UpdateTrafficLightsUI();
                    UpdateUI();
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[UI] 정책 업데이트 오류: {ex.Message}"); }
            });
        }

        /// <summary>
        /// 30FPS 타이머 적용
        /// </summary>
        private void InitializeRenderLoop()
        {
            _renderTimer = new DispatcherTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(33);
            _renderTimer.Tick += RenderTimer_Tick;
            _renderTimer.Start();

            InitializeGraph();
        }

        // 그래프 초기화
        // 0시부터 24시까지의 축을 미리 그리고 Polyline 객체를 생성
        // 이후 센서 데이터가 들어오면 포인트를 추가하는 방식
        private void InitializeGraph()
        {
            // 기존 그래프 요소 모두 제거
            canvasGraph.Children.Clear();

            // 캔버스 크기 계산
            double canvasWidth = canvasGraph.ActualWidth > 0 ? canvasGraph.ActualWidth : 350;
            double canvasHeight = canvasGraph.ActualHeight > 0 ? canvasGraph.ActualHeight : 200;
            // 그래프 실제 크기 (패딩 제외)
            double graphWidth = canvasWidth - 2 * GRAPH_PADDING;
            double graphHeight = canvasHeight - 2 * GRAPH_PADDING;

            // X축 (가로선), Y축 (세로선) 그리기
            canvasGraph.Children.Add(new Line { X1 = GRAPH_PADDING, Y1 = canvasHeight - GRAPH_PADDING, X2 = canvasWidth - GRAPH_PADDING, Y2 = canvasHeight - GRAPH_PADDING, Stroke = _brushGraphAxis, StrokeThickness = 2 });
            canvasGraph.Children.Add(new Line { X1 = GRAPH_PADDING, Y1 = GRAPH_PADDING, X2 = GRAPH_PADDING, Y2 = canvasHeight - GRAPH_PADDING, Stroke = _brushGraphAxis, StrokeThickness = 2 });

            // X축 라벨
            TextBlock xLabel = new TextBlock { Text = "시간 (시)", Foreground = _brushGraphLabel, FontSize = 10, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(xLabel, canvasWidth - 60); Canvas.SetTop(xLabel, canvasHeight - 18); canvasGraph.Children.Add(xLabel);

            // X축 눈금과 시간 레이블 (0시, 3시, 6시, ... 24시)
            for (int hour = 0; hour <= 24; hour += 3)
            {
                double x = GRAPH_PADDING + (hour / 24.0) * graphWidth;

                // 눈금선
                canvasGraph.Children.Add(new Line { X1 = x, Y1 = canvasHeight - GRAPH_PADDING, X2 = x, Y2 = canvasHeight - GRAPH_PADDING + 5, Stroke = _brushGraphAxis, StrokeThickness = 1 });

                // 시간 레이블
                TextBlock timeLabel = new TextBlock { Text = hour.ToString(), Foreground = _brushGraphLabel, FontSize = 8 };
                Canvas.SetLeft(timeLabel, x - 10); Canvas.SetTop(timeLabel, canvasHeight - GRAPH_PADDING + 8); canvasGraph.Children.Add(timeLabel);
            }

            // 범례
            TextBlock legendTemp = new TextBlock { Text = "●", Foreground = _brushGraphTemp, FontSize = 12, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(legendTemp, GRAPH_PADDING + 10); Canvas.SetTop(legendTemp, GRAPH_PADDING + 15); canvasGraph.Children.Add(legendTemp);

            TextBlock legendTempText = new TextBlock { Text = "기온", Foreground = _brushGraphLabel, FontSize = 10 };
            Canvas.SetLeft(legendTempText, GRAPH_PADDING + 25); Canvas.SetTop(legendTempText, GRAPH_PADDING + 15); canvasGraph.Children.Add(legendTempText);

            TextBlock legendRain = new TextBlock { Text = "●", Foreground = _brushGraphRain, FontSize = 12, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(legendRain, GRAPH_PADDING + 80); Canvas.SetTop(legendRain, GRAPH_PADDING + 15); canvasGraph.Children.Add(legendRain);

            TextBlock legendRainText = new TextBlock { Text = "강수량", Foreground = _brushGraphLabel, FontSize = 10 };
            Canvas.SetLeft(legendRainText, GRAPH_PADDING + 95); Canvas.SetTop(legendRainText, GRAPH_PADDING + 15); canvasGraph.Children.Add(legendRainText);

            // Polyline 객체 생성 (아직 포인트 없음)
            _tempPoints = new PointCollection();
            _rainPoints = new PointCollection();

            _tempPolyline = new Polyline { Points = _tempPoints, Stroke = _brushGraphTemp, StrokeThickness = 3 };
            _rainPolyline = new Polyline { Points = _rainPoints, Stroke = _brushGraphRain, StrokeThickness = 3 };

            canvasGraph.Children.Add(_tempPolyline);
            canvasGraph.Children.Add(_rainPolyline);
        }

        #endregion

        #region UI 시각화 및 렌더링

        /// <summary> 
        /// 대시보드 텍스트 및 색상 갱신
        /// </summary>
        private void UpdateUI()
        {
            RoadConditionType? currentCondition = _forceBlackIce ? RoadConditionType.Icy : _currentData?.RoadCondition;
            string currentRoadCondition = currentCondition switch
            {
                RoadConditionType.Dry => "건조",
                RoadConditionType.Wet => "습윤",
                RoadConditionType.Icy => "결빙",
                RoadConditionType.Snow => "적설",
                _ => "대기 중"
            };
            bool isEmergency = _emergencyTimer > 0 || _lastTrafficArgs?.CurrentState == TrafficState.Emergency;

            rectBlackIceOverlay.Opacity = _forceBlackIce ? 0.25 : 0;
            rectWrongWayCar.Visibility = ((_currentData?.IsWrongWay ?? false) || isEmergency) ? Visibility.Visible : Visibility.Hidden;

            // 센서 데이터 텍스트 바인딩
            if (_currentData != null)
            {
                txtTemperature.Text = $"{_currentData.Temperature:F1} °C";
                txtRainfall.Text = $"{_currentData.Rainfall:F1} mm/h";
                txtWindSpeed.Text = $"{_currentData.WindSpeed:F1} m/s";
                txtVehicleSpeed.Text = $"{_currentData.VehicleSpeed:F1} km/h";
                txtAvgSpeed.Text = $"GPS 평균속도: {_currentData.AvgSpeed_GPS:F1} km/h";

                txtWrongWay.Text = _currentData.IsWrongWay ? "역주행: 감지됨! ⚠" : "역주행: 없음";
                txtWrongWay.Foreground = _currentData.IsWrongWay ? _brushRed : _brushGreen;

                txtPedestrian.Text = _currentData.IsPedestrianRemaining ? "잔류 보행자: 있음 ⚠" : "잔류 보행자: 없음";
                txtPedestrian.Foreground = _currentData.IsPedestrianRemaining ? _brushYellow : _brushGreen;

                // 보행자 표시 아이콘 업데이트 (4개 방향 횡단보도)
                Visibility pedestrianVis = _currentData.IsPedestrianRemaining ? Visibility.Visible : Visibility.Hidden;
                _pedestrianN.Visibility = pedestrianVis;
                _pedestrianS.Visibility = pedestrianVis;
                _pedestrianE.Visibility = pedestrianVis;
                _pedestrianW.Visibility = pedestrianVis;
            }

            txtRoadCondition.Text = currentRoadCondition;
            txtEfficiencyScore.Text = $"{_currentEfficiencyScore}점";
            txtRoadSafetyScore.Text = $"{_currentSafetyScore}점";

            // 노면 상태에 따른 배경 테마
            switch (currentRoadCondition)
            {
                case "건조": bdRoadCondition.Background = _bgDry; txtRoadCondition.Foreground = _brushWhite; break;
                case "습윤": bdRoadCondition.Background = _bgWet; txtRoadCondition.Foreground = _textWet; break;
                case "결빙": bdRoadCondition.Background = _bgIce; txtRoadCondition.Foreground = _textIce; break;
                case "적설": bdRoadCondition.Background = _bgSnow; txtRoadCondition.Foreground = _brushWhite; break;
                default: bdRoadCondition.Background = _brushInactive; txtRoadCondition.Foreground = _brushWhite; break;
            }

            // 제어 모드 상태 표시
            if (_forceBlackIce) // 빙판모드 키면 강제
            {
                txtActiveControl.Text = "❄️ 환경 위험 (안전 제어)";
                txtActiveControl.Foreground = _textIce;
                bdActiveControl.BorderBrush = _textIce;
                bdActiveControl.Background = _bgControlIce;
            }
            else if (isEmergency)
            {
                txtActiveControl.Text = " 긴급 제어: 전 방향 정지!";
                txtActiveControl.Foreground = _brushRed;
                bdActiveControl.BorderBrush = _brushRed; bdActiveControl.Background = _bgControlEmergency;
                btnTriggerWrongWay.Content = "긴급 제어 가동 중..."; btnTriggerWrongWay.Background = _brushInactive;
            }
            else
            {
                if (_lastTrafficArgs?.CurrentState == TrafficState.Congested)
                {
                    txtActiveControl.Text = "⚠️ 교통 혼잡 (신호 연장)";
                    txtActiveControl.Foreground = _brushYellow;
                    bdActiveControl.BorderBrush = _brushYellow; bdActiveControl.Background = _bgControlCongested;
                }
                else if (_lastTrafficArgs?.CurrentState == TrafficState.EnvironmentalHazard || _forceBlackIce)
                {
                    txtActiveControl.Text = "❄️ 환경 위험 (안전 제어)";
                    txtActiveControl.Foreground = _textIce;
                    bdActiveControl.BorderBrush = _textIce; bdActiveControl.Background = _bgControlIce;
                }
                else
                {
                    txtActiveControl.Text = "🟢 정상 제어 중";
                    txtActiveControl.Foreground = _brushGreen;
                    bdActiveControl.BorderBrush = _borderControlNormal; bdActiveControl.Background = _bgControlNormal;
                }
                btnTriggerWrongWay.Content = "🚨 강제 역주행 시뮬레이션"; btnTriggerWrongWay.Background = _brushRed;
            }

            btnToggleBlackIce.Background = _forceBlackIce ? _brushRed : _brushOrange;
            btnToggleBlackIce.Content = _forceBlackIce ? "블랙아이스 강제 해제" : "블랙아이스 모드 강제 실행";
        }

        /// <summary>
        /// 신호등 색상 및 카운트다운 게이지 업데이트
        /// </summary>
        private void UpdateTrafficLightsUI()
        {
            if (_lastTrafficArgs == null) return;
            bool forceAllRed = _emergencyTimer > 0 || (_lastTrafficArgs?.CurrentState == TrafficState.Emergency);

            if (forceAllRed)
            {
                lightN_Red.Fill = lightS_Red.Fill = lightE_Red.Fill = lightW_Red.Fill = _brushRed;
                lightN_Green.Fill = lightS_Green.Fill = lightE_Green.Fill = lightW_Green.Fill = _dimGreen;
                txtSignalTimer.Text = "🚨 긴급 정지 상태"; pbSignalTimer.Maximum = 90; pbSignalTimer.Value = _emergencyTimer; pbSignalTimer.Foreground = _brushRed;
            }
            else
            {
                if (_lastTrafficArgs.Light_N.Color == LightColor.Green) { lightN_Red.Fill = lightS_Red.Fill = _dimRed; lightN_Green.Fill = lightS_Green.Fill = _brushGreen; }
                else { lightN_Red.Fill = lightS_Red.Fill = _brushRed; lightN_Green.Fill = lightS_Green.Fill = _dimGreen; }

                if (_lastTrafficArgs.Light_E.Color == LightColor.Green) { lightE_Red.Fill = lightW_Red.Fill = _dimRed; lightE_Green.Fill = lightW_Green.Fill = _brushGreen; }
                else { lightE_Red.Fill = lightW_Red.Fill = _brushRed; lightE_Green.Fill = lightW_Green.Fill = _dimGreen; }

                int activeTime = (_lastTrafficArgs.Light_N.Color != LightColor.Red) ? _lastTrafficArgs.Light_N.RemainingTime : _lastTrafficArgs.Light_E.RemainingTime;

                // UI 시간과 Controller 시간이 동기화 되기 전 간극 처리
                if (activeTime <= 0) { txtSignalTimer.Text = "🔄 신호 전환 대기 중..."; pbSignalTimer.Value = 0; }
                else { txtSignalTimer.Text = $"다음 신호까지: {activeTime}초"; pbSignalTimer.Maximum = 45; pbSignalTimer.Value = Math.Min(activeTime, 45); }
                pbSignalTimer.Foreground = (_lastTrafficArgs?.CurrentState == TrafficState.Normal) ? _brushGreen : _brushYellow;
            }
        }

        /// <summary>
        /// 배속 버튼 색상
        /// </summary>
        private void UpdateSpeedButtonsUI()
        {
            btnSpeed1x.Background = _currentSpeed == 1 ? _brushGreen : _brushInactive;
            btnSpeed2x.Background = _currentSpeed == 2 ? _brushGreen : _brushInactive;
            btnSpeed4x.Background = _currentSpeed == 4 ? _brushGreen : _brushInactive;
        }

        // 그래프에 새로운 데이터 포인트 추가
        // 센서 데이터가 들어올 때마다 호출되어 기온과 강수량 그래프를 그려나감
        // 부드러운 애니메이션 효과를 위해 전체 그래프를 다시 그리지 않고 포인트만 추가
        private void AddGraphPoint(double temperature, double rainfall)
        {
            // Polyline이 아직 생성되지 않았으면 종료
            if (_tempPolyline == null || _rainPolyline == null) return;

            // 캔버스 크기 계산
            double canvasWidth = canvasGraph.ActualWidth > 0 ? canvasGraph.ActualWidth : 350;
            double canvasHeight = canvasGraph.ActualHeight > 0 ? canvasGraph.ActualHeight : 200;
            // 그래프 실제 크기 (패딩 제외)
            double graphWidth = canvasWidth - 2 * GRAPH_PADDING;
            double graphHeight = canvasHeight - 2 * GRAPH_PADDING;

            // 첫 번째 데이터일 때 Y축 라벨 설정 (고정 범위 사용)
            if (_temperatureHistory.Count == 1)
            {
                // Y축 기온 라벨 (왼쪽)
                TextBlock yLabelTemp = new TextBlock { Text = $"기온°C\n({GRAPH_MIN_TEMP:F0}~{GRAPH_MAX_TEMP:F0})", Foreground = _brushGraphTemp, FontSize = 9, FontWeight = System.Windows.FontWeights.Bold };
                Canvas.SetLeft(yLabelTemp, 2); Canvas.SetTop(yLabelTemp, 5); canvasGraph.Children.Add(yLabelTemp);

                // Y축 강수량 라벨 (오른쪽)
                TextBlock yLabelRain = new TextBlock { Text = $"강수mm\n(0~{GRAPH_MAX_RAINFALL:F0})", Foreground = _brushGraphRain, FontSize = 9, FontWeight = System.Windows.FontWeights.Bold };
                Canvas.SetLeft(yLabelRain, canvasWidth - 45); Canvas.SetTop(yLabelRain, 5); canvasGraph.Children.Add(yLabelRain);
            }

            // 데이터 포인트 위치 계산 (0~24시 기준)
            int dataIndex = _temperatureHistory.Count - 1;
            double hourFloat = (dataIndex * 5.0) / 60.0;  // 5분 단위를 시간으로 변환
            double x = GRAPH_PADDING + (hourFloat / 24.0) * graphWidth;

            // 기온 포인트 (고정 범위 -10 ~ 40도에서 계산)
            double tempRange = GRAPH_MAX_TEMP - GRAPH_MIN_TEMP;
            double normalizedTemp = Math.Max(GRAPH_MIN_TEMP, Math.Min(GRAPH_MAX_TEMP, temperature));
            double yTemp = canvasHeight - GRAPH_PADDING - ((normalizedTemp - GRAPH_MIN_TEMP) / tempRange) * graphHeight;
            _tempPoints.Add(new Point(x, yTemp));

            // 강수량 포인트 (고정 범위 0 ~ 50mm에서 계산, 전체 높이의 70% 사용하여 더 크게 표현)
            double normalizedRain = Math.Min(GRAPH_MAX_RAINFALL, rainfall);
            double yRain = canvasHeight - GRAPH_PADDING - (normalizedRain / GRAPH_MAX_RAINFALL) * (graphHeight * 0.7);
            _rainPoints.Add(new Point(x, yRain));
        }

        /// <summary>
        /// 꺾은선 그래프 렌더링 (사용 안 함 - 새로운 방식 사용)
        /// </summary>
        private void DrawGraph()
        {
            canvasGraph.Children.Clear();
            if (_temperatureHistory.Count <= 1)
            {
                TextBlock tb = new TextBlock { Text = "시뮬레이션 데이터를 기다리는 중...", Foreground = _brushGraphLabel, HorizontalAlignment = HorizontalAlignment.Center };
                canvasGraph.Children.Add(tb);
                return;
            }

            double canvasWidth = canvasGraph.ActualWidth > 0 ? canvasGraph.ActualWidth : 350;
            double canvasHeight = canvasGraph.ActualHeight > 0 ? canvasGraph.ActualHeight : 200;
            double padding = 50;
            double graphWidth = canvasWidth - 2 * padding;
            double graphHeight = canvasHeight - 2 * padding;

            // 기온 범위 계산
            double minTemp = _temperatureHistory.Min();
            double maxTemp = _temperatureHistory.Max();
            if (maxTemp == minTemp) maxTemp = minTemp + 1;

            // 강수량 범위 계산
            double maxRainfall = Math.Max(_rainfallHistory.Max() + 1, 1);

            // X, Y축 그리기
            canvasGraph.Children.Add(new Line { X1 = padding, Y1 = canvasHeight - padding, X2 = canvasWidth - padding, Y2 = canvasHeight - padding, Stroke = _brushGraphAxis, StrokeThickness = 2 });
            canvasGraph.Children.Add(new Line { X1 = padding, Y1 = padding, X2 = padding, Y2 = canvasHeight - padding, Stroke = _brushGraphAxis, StrokeThickness = 2 });

            // X축 라벨
            TextBlock xLabel = new TextBlock { Text = "시간 (시)", Foreground = _brushGraphLabel, FontSize = 10, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(xLabel, canvasWidth - 60); Canvas.SetTop(xLabel, canvasHeight - 18); canvasGraph.Children.Add(xLabel);

            // Y축 라벨 (기온 범위)
            TextBlock yLabelTemp = new TextBlock { Text = $"기온°C\n({minTemp:F1}~{maxTemp:F1})", Foreground = _brushGraphTemp, FontSize = 9, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(yLabelTemp, 2); Canvas.SetTop(yLabelTemp, 5); canvasGraph.Children.Add(yLabelTemp);

            // 강수량 범위 표시 (우측)
            TextBlock yLabelRain = new TextBlock { Text = $"강수mm\n(0~{maxRainfall:F1})", Foreground = _brushGraphRain, FontSize = 9, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(yLabelRain, canvasWidth - 45); Canvas.SetTop(yLabelRain, 5); canvasGraph.Children.Add(yLabelRain);

            // 범례 추가
            TextBlock legendTemp = new TextBlock { Text = "●", Foreground = _brushGraphTemp, FontSize = 12, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(legendTemp, padding + 10); Canvas.SetTop(legendTemp, padding + 15); canvasGraph.Children.Add(legendTemp);

            TextBlock legendTempText = new TextBlock { Text = "기온", Foreground = _brushGraphLabel, FontSize = 10 };
            Canvas.SetLeft(legendTempText, padding + 25); Canvas.SetTop(legendTempText, padding + 15); canvasGraph.Children.Add(legendTempText);

            TextBlock legendRain = new TextBlock { Text = "●", Foreground = _brushGraphRain, FontSize = 12, FontWeight = System.Windows.FontWeights.Bold };
            Canvas.SetLeft(legendRain, padding + 80); Canvas.SetTop(legendRain, padding + 15); canvasGraph.Children.Add(legendRain);

            TextBlock legendRainText = new TextBlock { Text = "강수량", Foreground = _brushGraphLabel, FontSize = 10 };
            Canvas.SetLeft(legendRainText, padding + 95); Canvas.SetTop(legendRainText, padding + 15); canvasGraph.Children.Add(legendRainText);

            // X축 눈금과 시간 레이블 (0시, 3시, 6시, ... 24시)
            int totalDataPoints = _temperatureHistory.Count;
            for (int hour = 0; hour <= 24; hour += 3)
            {
                int dataIndex = (int)(hour / 24.0 * (totalDataPoints - 1));
                if (dataIndex >= totalDataPoints) dataIndex = totalDataPoints - 1;

                double x = padding + (dataIndex / (double)(totalDataPoints - 1)) * graphWidth;

                // 눈금선
                canvasGraph.Children.Add(new Line { X1 = x, Y1 = canvasHeight - padding, X2 = x, Y2 = canvasHeight - padding + 5, Stroke = _brushGraphAxis, StrokeThickness = 1 });

                // 시간 레이블
                TextBlock timeLabel = new TextBlock { Text = hour.ToString(), Foreground = _brushGraphLabel, FontSize = 8 };
                Canvas.SetLeft(timeLabel, x - 10); Canvas.SetTop(timeLabel, canvasHeight - padding + 8); canvasGraph.Children.Add(timeLabel);
            }

            // 기온 포인트 및 라인 그리기
            PointCollection tempPoints = new PointCollection();
            for (int i = 0; i < _temperatureHistory.Count; i++)
            {
                double x = padding + (i / (double)(_temperatureHistory.Count - 1)) * graphWidth;
                double y = canvasHeight - padding - ((_temperatureHistory[i] - minTemp) / (maxTemp - minTemp)) * graphHeight;
                tempPoints.Add(new Point(x, y));
            }
            canvasGraph.Children.Add(new Polyline { Points = tempPoints, Stroke = _brushGraphTemp, StrokeThickness = 3 });

            // 강수량 포인트 및 라인 그리기 (Y축 상단 40% 영역에만 표시)
            PointCollection rainPoints = new PointCollection();
            for (int i = 0; i < _rainfallHistory.Count; i++)
            {
                double x = padding + (i / (double)(_rainfallHistory.Count - 1)) * graphWidth;
                double y = canvasHeight - padding - (_rainfallHistory[i] / maxRainfall) * (graphHeight * 0.4);
                rainPoints.Add(new Point(x, y));
            }
            canvasGraph.Children.Add(new Polyline { Points = rainPoints, Stroke = _brushGraphRain, StrokeThickness = 3 });
        }

        private void UpdateGraphStats()
        {
            if (_temperatureHistory.Count == 0) return;
            txtGraphStats.Text = $"최고기온: {_temperatureHistory.Max():F1} °C | 최저기온: {_temperatureHistory.Min():F1} °C | 누적강수: {_rainfallHistory.Sum():F1} mm";
        }

        #endregion

        #region 메인 시뮬레이션 및 렌더링

        // 30FPS 주기로 렌더링
        // 차량 이동, 신호 시간 진행, 신호 색상 변경 등을 처리
        private void RenderTimer_Tick(object sender, EventArgs e)
        {
            // 시뮬레이션이 실행 중이 아니면 렌더링 건너뛰기
            if (!_isDayRunning) return;

            // 실제 경과 시간 계산
            DateTime now = DateTime.Now;
            double realDeltaTime = (now - _lastTickTime).TotalSeconds;
            _lastTickTime = now;

            // 배속을 고려한 시뮬레이션 경과 시간 계산
            // 배속 2배면 실제 1초가 시뮬레이션 2초로 진행
            double simulatedDeltaTime = realDeltaTime * _currentSpeed;
            // TrafficController에 시간 전달하여 신호 시간을 진행
            _trafficController?.PassTime(simulatedDeltaTime);

            double speedMultiplier = _policySpeedMultiplier;

            if (_forceBlackIce)
            {
                speedMultiplier *= 0.4; // 블랙아이스면 속도를 강제로 60% 감소
            }

            if (!_isRushHourEnabled)
            {
                speedMultiplier *= 1.2; // 혼잡 모드 해제 시 속도 증가
            }

            double moveSpeed = _baseMoveSpeed * speedMultiplier * _currentSpeed;
            //double moveSpeed = _baseMoveSpeed * _policySpeedMultiplier * _currentSpeed;
            int dynamicCooldown = _baseSpawnCooldown / _currentSpeed;

            // 긴급 제어 타이머 처리 (배속 반영)
            bool isEmergencyStop = false;
            if (_emergencyTimer > 0)
            {
                isEmergencyStop = true;
                _emergencyTimer -= _currentSpeed;  // 배속에 따라 감소
                if (_emergencyTimer <= 0)
                {
                    _emergencyTimer = 0;
                    if (_trafficController != null) _trafficController.ForceWrongWay = false;   // 3초 지나면 강제상태 해제
                    _dataGenerator?.ForceWrongWay(false);           // DataGenerator 역주행 강제 해제
                    UpdateUI();
                }
            }

            // 쿨다운 갱신
            if (_spawnCooldownN > 0) _spawnCooldownN--; if (_spawnCooldownS > 0) _spawnCooldownS--;
            if (_spawnCooldownE > 0) _spawnCooldownE--; if (_spawnCooldownW > 0) _spawnCooldownW--;

            UpdateTrafficLightsUI();

            // GC 부하 줄이기 위한 Clear (초기화)
            _activeN.Clear(); _activeS.Clear(); _activeE.Clear(); _activeW.Clear();
            bool isOccupiedByEW = false; bool isOccupiedByNS = false;

            // 차량 그룹화 및 교차로 내부 점유 상태 파악 (꼬리물기 방지용)
            for (int j = 0; j < MAX_CARS; j++)
            {
                var c = _carPool[j]; if (!c.IsActive) continue;

                if (c.Direction == "N") _activeN.Add(c);
                else if (c.Direction == "S") _activeS.Add(c);
                else if (c.Direction == "E") _activeE.Add(c);
                else if (c.Direction == "W") _activeW.Add(c);

                if ((c.Direction == "E" || c.Direction == "W") && (c.X > STOP_LINE_N && c.X < STOP_LINE_S)) isOccupiedByEW = true;
                if ((c.Direction == "N" || c.Direction == "S") && (c.Y > STOP_LINE_W && c.Y < STOP_LINE_E)) isOccupiedByNS = true;
            }

            // 개별 차량 이동 로직 (Predictive Move)
            for (int i = 0; i < MAX_CARS; i++)
            {
                var car = _carPool[i]; if (!car.IsActive) continue;
                bool canMove = true;

                // 다음 프레임 위치 예측
                double nextX = car.Direction == "E" ? car.X - moveSpeed : (car.Direction == "W" ? car.X + moveSpeed : car.X);
                double nextY = car.Direction == "N" ? car.Y + moveSpeed : (car.Direction == "S" ? car.Y - moveSpeed : car.Y);

                bool isControllerRedN = (_lastTrafficArgs == null || _lastTrafficArgs.Light_N.Color != LightColor.Green);
                bool isControllerRedE = (_lastTrafficArgs == null || _lastTrafficArgs.Light_E.Color != LightColor.Green);

                // 정지선 통제 조건 (신호 위반, 긴급 제어, 꼬리물기 중 하나라도 걸리면 정지 대상)
                bool isRedLightForMe = (car.Direction == "N" || car.Direction == "S")
                                     ? (isControllerRedN || isEmergencyStop || isOccupiedByEW)
                                     : (isControllerRedE || isEmergencyStop || isOccupiedByNS);

                if (isRedLightForMe)
                {
                    if (car.Direction == "N" && car.Y < STOP_LINE_N && nextY >= STOP_LINE_N) canMove = false;
                    else if (car.Direction == "S" && car.Y > STOP_LINE_S && nextY <= STOP_LINE_S) canMove = false;
                    else if (car.Direction == "E" && car.X > STOP_LINE_E && nextX <= STOP_LINE_E) canMove = false;
                    else if (car.Direction == "W" && car.X < STOP_LINE_W && nextX >= STOP_LINE_W) canMove = false;
                }

                // 앞차 추돌 방지 (자신이 속한 방향 리스트만 순회)
                if (canMove)
                {
                    var targetList = car.Direction == "N" ? _activeN : (car.Direction == "S" ? _activeS : (car.Direction == "E" ? _activeE : _activeW));
                    foreach (var other in targetList)
                    {
                        if (car == other) continue;
                        if (car.Direction == "N" && other.Y > car.Y && other.Y - car.Y < CAR_FOLLOW_DISTANCE) { canMove = false; break; }
                        if (car.Direction == "S" && other.Y < car.Y && car.Y - other.Y < CAR_FOLLOW_DISTANCE) { canMove = false; break; }
                        if (car.Direction == "E" && other.X < car.X && car.X - other.X < CAR_FOLLOW_DISTANCE) { canMove = false; break; }
                        if (car.Direction == "W" && other.X > car.X && other.X - car.X < CAR_FOLLOW_DISTANCE) { canMove = false; break; }
                    }
                }

                // 이동 가능하면 좌표 업데이트
                if (canMove)
                {
                    car.X = nextX; car.Y = nextY;
                    car.Transform.X = car.X; car.Transform.Y = car.Y;
                }

                // 화면 이탈 차량 반납
                if (car.X < -50 || car.X > 550 || car.Y < -50 || car.Y > 550) { car.IsActive = false; car.Shape.Visibility = Visibility.Hidden; }
            }

            if (_emergencyTimer == 0 && (_currentData?.IsWrongWay ?? false) == false)
            {
                rectWrongWayCar.Visibility = Visibility.Hidden;
            }

            // 위에서 카운트해 둔 리스트 개수를 바로 재사용
            if (_currentData != null) SyncAndReportTraffic(dynamicCooldown);
        }

        /// <summary>
        /// 차량 소환 조건 검사 및 동기화
        /// </summary>
        private void SyncAndReportTraffic(int dynamicCooldown)
        {
            TrySpawnIfNeeded("N", _currentData.WaitingCars_N, _activeN.Count, dynamicCooldown);
            TrySpawnIfNeeded("S", _currentData.WaitingCars_S, _activeS.Count, dynamicCooldown);
            TrySpawnIfNeeded("E", _currentData.WaitingCars_E, _activeE.Count, dynamicCooldown);
            TrySpawnIfNeeded("W", _currentData.WaitingCars_W, _activeW.Count, dynamicCooldown);

            UpdateCarCountText(txtCarsN, _activeN.Count); UpdateCarCountText(txtCarsS, _activeS.Count);
            UpdateCarCountText(txtCarsE, _activeE.Count); UpdateCarCountText(txtCarsW, _activeW.Count);
        }

        /// <summary>
        /// 안전 거리가 확보되었을 때만 Pool에서 차량 꺼내기
        /// </summary>
        private void TrySpawnIfNeeded(string dir, int target, int current, int cooldownLimit)
        {
            int visualTarget = Math.Min(target, 8); // 화면에는 최대 8대까지만 시각화
            if (current >= visualTarget) return;
            if ((dir == "N" && _spawnCooldownN > 0) || (dir == "S" && _spawnCooldownS > 0) || (dir == "E" && _spawnCooldownE > 0) || (dir == "W" && _spawnCooldownW > 0)) return;

            double entrySafetyMargin = 20 * _currentSpeed; bool isEntryClear = true;
            for (int i = 0; i < MAX_CARS; i++)
            {
                var car = _carPool[i]; if (!car.IsActive || car.Direction != dir) continue;
                if (dir == "N" && car.Y < entrySafetyMargin) isEntryClear = false; if (dir == "S" && car.Y > 500 - entrySafetyMargin) isEntryClear = false;
                if (dir == "E" && car.X > 500 - entrySafetyMargin) isEntryClear = false; if (dir == "W" && car.X < entrySafetyMargin) isEntryClear = false;
            }

            if (isEntryClear)
            {
                SpawnCar(dir);
                if (dir == "N") _spawnCooldownN = cooldownLimit; if (dir == "S") _spawnCooldownS = cooldownLimit;
                if (dir == "E") _spawnCooldownE = cooldownLimit; if (dir == "W") _spawnCooldownW = cooldownLimit;
            }
        }

        private void SpawnCar(string dir)
        {
            CarUI car = _carPool.FirstOrDefault(c => !c.IsActive);
            if (car == null) return;

            car.IsActive = true; car.Direction = dir; car.Shape.Visibility = Visibility.Visible;
            if (dir == "N") { car.Shape.Width = 10; car.Shape.Height = 20; car.X = 210; car.Y = -20; }
            else if (dir == "S") { car.Shape.Width = 10; car.Shape.Height = 20; car.X = 280; car.Y = 500; }
            else if (dir == "E") { car.Shape.Width = 20; car.Shape.Height = 10; car.X = 500; car.Y = 210; }
            else if (dir == "W") { car.Shape.Width = 20; car.Shape.Height = 10; car.X = -20; car.Y = 280; }

            car.Transform.X = car.X; car.Transform.Y = car.Y;
        }

        #endregion

        #region 버튼 이벤트

        private void UpdateCarCountText(TextBlock txtBlock, int count)
        {
            txtBlock.Text = $"{count}대";
            if (count >= 10) txtBlock.Foreground = _brushRed; else if (count >= 5) txtBlock.Foreground = _brushYellow; else txtBlock.Foreground = _brushGreen;
        }

        private void BtnToggleBlackIce_Click(object sender, RoutedEventArgs e)
        {
            _forceBlackIce = !_forceBlackIce;
            UpdateUI();
        }

        private void BtnToggleRushHour_Click(object sender, RoutedEventArgs e)
        {
            _isRushHourEnabled = !_isRushHourEnabled;
            _dataGenerator?.SetRushHourMode(_isRushHourEnabled); // 가능한 경우 모듈에 전달
            btnToggleRushHour.Content = _isRushHourEnabled ? "혼잡 시간 모드 해제" : "혼잡 시간 모드 활성";
            UpdateUI();
        }

        private void BtnTriggerWrongWay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _dataGenerator?.ForceWrongWay(true);                // DataGenerator 역주행 강제
                rectWrongWayCar.Visibility = Visibility.Visible;    // UI 시각화
                TriggerEmergencyStop(); // 긴급 정지 발동 (TrafficController.ForceWrongWay=true + emergencyTimer 시작)
                UpdateUI();
            }
            catch (Exception ex) { MessageBox.Show($"역주행 제어 실패: {ex.Message}"); }
        }

        // 하루 시뮬레이션 시작 버튼 클릭 이벤트
        // 모든 시뮬레이션을 초기화하고 24시간 데이터 생성 시작
        private void BtnStartDay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 이미 시뮬레이션 중이면 중복 시작 방지
                if (_isDayRunning) return;

                // 진행 중인 시뮬레이션 완전 종료 (이전 상태 정리)
                _dataGenerator?.Stop();

                // 시뮬레이션 시작 플래그 설정
                _isDayRunning = true;
                // 이전 데이터 모두 제거
                _dataManager?.ClearHistory();
                // 그래프 데이터 초기화
                _temperatureHistory.Clear();
                _rainfallHistory.Clear();

                // 신호 상태 초기화 (남북 신호부터 시작)
                _trafficController?.Reset();
                // 그래프 축 그리기
                InitializeGraph();
                // 렌더링 타이머를 위한 시간 기록
                _lastTickTime = DateTime.Now;
                // 센서 데이터 생성 시작
                _dataGenerator?.StartDay();

                // UI 업데이트
                btnStartDay.Content = "시뮬레이션 진행 중...";
                btnStartDay.IsEnabled = false;
            }
            catch (Exception ex) { MessageBox.Show($"시뮬레이션 오류: {ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        /// <summary>
        /// 24시간 시뮬레이션 종료 시 초기화 및 저장 결과 통보
        /// </summary>
        private void OnDayCompleted()
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    _isDayRunning = false;
                    SaveResult saveResult = _dataManager.AutoSaveDay(); // 자동 저장 시도
                    ResetRoadState();

                    btnStartDay.Content = "▶ 하루 시뮬레이션 시작"; btnStartDay.IsEnabled = true;

                    if (saveResult.Success)
                        MessageBox.Show($"하루 시뮬레이션 완료 및 데이터 자동 저장 성공!\n저장 위치: {saveResult.FilePath}", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show($"시뮬레이션은 완료되었으나, 데이터 저장에 실패했습니다.\n{saveResult.Message}", "저장 경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex) { MessageBox.Show($"종료 처리 오류: {ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error); }
            });
        }

        /// <summary>
        /// 다음 날짜 진행을 위한 교차로 상태 초기화 (UI 리셋)
        /// </summary>
        private void ResetRoadState()
        {
            lightN_Red.Fill = lightS_Red.Fill = lightE_Red.Fill = lightW_Red.Fill = _brushRed;
            lightN_Green.Fill = lightS_Green.Fill = lightE_Green.Fill = lightW_Green.Fill = _dimGreen;

            txtRoadCondition.Text = "건조"; bdRoadCondition.Background = _bgDry; txtRoadCondition.Foreground = _brushWhite;
            _currentSafetyScore = 0; _currentEfficiencyScore = 100;
            _forceBlackIce = false; _emergencyTimer = 0;
            _lastTrafficArgs = null;

            if (_trafficController != null)
            {
                _trafficController.ForceBlackIce = false;
                _trafficController.ForceWrongWay = false;
            }

            btnToggleBlackIce.Content = "블랙아이스 모드 강제 실행"; btnToggleBlackIce.Background = _brushOrange;
            txtActiveControl.Text = "정상 제어 중"; txtActiveControl.Foreground = _brushGreen;
            txtRoadSafetyScore.Text = "0점"; txtRoadSafetyScore.Foreground = _brushWhite;

            rectBlackIceOverlay.Opacity = 0; rectWrongWayCar.Visibility = Visibility.Hidden;
            _pedestrianN.Visibility = Visibility.Hidden; _pedestrianS.Visibility = Visibility.Hidden;
            _pedestrianE.Visibility = Visibility.Hidden; _pedestrianW.Visibility = Visibility.Hidden;
            txtSignalTimer.Text = "대기 중"; pbSignalTimer.Value = 0;

            InitializeGraph();  // 그래프 초기화
            UpdateUI();
        }

        // 배속 1배 버튼 클릭
        // 센서 업데이트와 신호 시간을 1배 속도로 설정
        private void BtnSpeed1x_Click(object sender, RoutedEventArgs e)
        {
            _currentSpeed = 1;
            _dataGenerator?.SetSpeed(1);
            _trafficController?.SetSimulationSpeed(1);
            UpdateSpeedButtonsUI();
        }

        // 배속 2배 버튼 클릭
        // 센서 업데이트와 신호 시간을 2배 속도로 설정
        private void BtnSpeed2x_Click(object sender, RoutedEventArgs e)
        {
            _currentSpeed = 2;
            _dataGenerator?.SetSpeed(2);
            _trafficController?.SetSimulationSpeed(2);
            UpdateSpeedButtonsUI();
        }

        // 배속 4배 버튼 클릭
        // 센서 업데이트와 신호 시간을 4배 속도로 설정
        private void BtnSpeed4x_Click(object sender, RoutedEventArgs e)
        {
            _currentSpeed = 4;
            _dataGenerator?.SetSpeed(4);
            _trafficController?.SetSimulationSpeed(4);
            UpdateSpeedButtonsUI();
        }

        // 윈도우 종료 이벤트
        // 모든 타이머와 리소스를 정리하여 메모리 누수 방지
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 렌더링 타이머 중지 및 해제
            _renderTimer?.Stop();
            _renderTimer = null;
            // 신호 제어 모듈 리소스 해제
            _trafficController?.Dispose();
            // 센서 데이터 생성 모듈 리소스 해제
            _dataGenerator?.Dispose();
            // 데이터 저장소 정리
            _dataManager?.ClearHistory();
        }

        #endregion
    }
}
