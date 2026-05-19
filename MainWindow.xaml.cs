using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Smart_Road
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 핵심 모듈 및 시뮬레이션 제어 변수
        /// </summary>
        private DataGenerator _dataGenerator;
        private TrafficController _trafficController;
        private DataManager _dataManager;
        private SensorData _currentData;
        private int _currentSafetyScore = 0;
        private int _currentEfficiencyScore = 100;

        private DispatcherTimer _renderTimer;

        // 차량 최대 40대, 한번에 관리
        // 매번 차를 만들면 시간이 많이 걸리니까, 미리 만들어주고 차량 할당
        private const int MAX_CARS = 40;
        private CarUI[] _carPool = new CarUI[MAX_CARS];

        private bool _isNSGreen = true;
        private int _signalTimer = 0;
        private int _currentSpeed = 1;

        // 제어변수
        private double _baseMoveSpeed = 2.0;
        private int _baseSignalThreshold = 240;
        private int _baseSpawnCooldown = 20;

        private double _policySignalMultiplier = 1.0;
        private double _policySpeedMultiplier = 1.0;

        private int _spawnCooldownN = 0, _spawnCooldownS = 0;
        private int _spawnCooldownE = 0, _spawnCooldownW = 0;

        private bool _forceBlackIce = false;
        private int _emergencyTimer = 0;
        private bool _isRushHourEnabled = true; // 혼잡 시간 모드 (기본값: 활성)
        private bool _isDayRunning = false;     // 하루 시뮬레이션 진행 중

        // 그래프 데이터
        private List<double> _temperatureHistory = new List<double>();
        private List<double> _rainfallHistory = new List<double>();


        // 리소스 캐싱해서 그리기 속도 단축
        // 매번 새로 그리면 시간 오래걸림
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

        private class CarUI
        {
            public Rectangle Shape { get; set; }
            public TranslateTransform Transform { get; set; }
            public string Direction { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public bool IsActive { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();

            btnToggleBlackIce.Click += BtnToggleBlackIce_Click;
            btnToggleRushHour.Click += BtnToggleRushHour_Click;
            btnTriggerWrongWay.Click += BtnTriggerWrongWay_Click;
            btnStartDay.Click += BtnStartDay_Click;

            btnSpeed1x.Click += BtnSpeed1x_Click;
            btnSpeed2x.Click += BtnSpeed2x_Click;
            btnSpeed4x.Click += BtnSpeed4x_Click;

            InitializeCarPool();
            InitializeDataGenerator();
            InitializeRenderLoop();
            UpdateSpeedButtonsUI();
        }

        /// <summary>
        /// 외부 모듈 : 교통 혼잡도에 따라서, 신호 길이 및 차량 속도 동적 조절
        /// </summary>
        public void ApplyTrafficPolicy(double signalMultiplier, double speedMultiplier)
        {
            _policySignalMultiplier = signalMultiplier;
            _policySpeedMultiplier = speedMultiplier;
        }

        /// <summary>
        /// 외부 모듈 : 전체 시스템에 3초간 강제 역주행 긴급 제어 발동
        /// </summary>
        public void TriggerEmergencyStop()
        {
            if (_emergencyTimer > 0) return; // 이미 가동 중이면 무시
            _emergencyTimer = 90;       // 30FPS로 렌더링 중인데 3초 발동이니까 90
            UpdateUI();
        }

        /// <summary>
        /// 외부 모듈 : 기상 악화 시 블랙아이스 모드 전환, 신호 연장 대기
        /// </summary>
        public void SetBlackIceMode(bool isActive)
        {
            _forceBlackIce = isActive;
            UpdateUI();
        }

        /// <summary>
        /// 초기화 함수들
        /// </summary>
        private void InitializeCarPool()
        {
            // 차량 40대를 미리 만들어둠
            for (int i = 0; i < MAX_CARS; i++)
            {
                // UI적으로 숨겨놓고 필요할 때 보이게 한다
                var transform = new TranslateTransform();
                var rect = new Rectangle { Fill = _brushWhite, RadiusX = 3, RadiusY = 3, Visibility = Visibility.Hidden, RenderTransform = transform };
                canvasIntersection.Children.Add(rect);
                _carPool[i] = new CarUI { Shape = rect, Transform = transform, IsActive = false };
            }
        }

        private void InitializeDataGenerator()
        {
            // Sensor data를 UI랑 동기화
            _dataGenerator = new DataGenerator();
            _trafficController = new TrafficController(_dataGenerator);
            _dataManager = new DataManager();

            // TrafficController 이벤트 구독 (점수 기반 신호 제어)
            _trafficController.TrafficUpdated += (s, e) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    // 점수 업데이트
                    _currentSafetyScore = e.SafetyScore;
                    _currentEfficiencyScore = e.EfficiencyScore;

                    // 1. 데이터 저장 (DataManager)
                    _dataManager.RecordData(e);

                    // 2. 교통 혼잡도에 따라 신호 길이 및 속도 동적 조절
                    // efficiencyScore 기반으로 신호 대기시간 승수 결정
                    double signalMultiplier = e.EfficiencyScore < 40 ? 1.5 : (e.EfficiencyScore < 60 ? 1.2 : 1.0);
                    double speedMultiplier = 1.0; // 기본값
                    ApplyTrafficPolicy(signalMultiplier, speedMultiplier);
                });
            };

            _dataGenerator.SensorDataUpdated += (data) =>
            {
                _currentData = data;
                this.Dispatcher.Invoke(() =>
                {
                    // 그래프 데이터 수집
                    _temperatureHistory.Add(data.Temperature);
                    _rainfallHistory.Add(data.Rainfall);

                    if (data.IsWrongWay) TriggerEmergencyStop(); // 역주행 긴급 제어
                    UpdateUI();
                    DrawGraph();
                    UpdateGraphStats();
                });
            };

            _dataGenerator.DayCompleted += OnDayCompleted;

            _dataGenerator.Initialize();
        }

        private void InitializeRenderLoop()
        {
            _renderTimer = new DispatcherTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(33);
            _renderTimer.Tick += RenderTimer_Tick;
            _renderTimer.Start();
        }

        /// <summary>
        ///  UI 업데이트 작업
        /// </summary>
        private void UpdateUI()
        {
            string currentRoadCondition = _forceBlackIce ? "결빙" : (_currentData?.RoadCondition ?? "대기 중");
            bool isWrongWayActive = _emergencyTimer > 0;

            // 블랙아이스 이펙트
            rectBlackIceOverlay.Opacity = _forceBlackIce ? 0.25 : 0;

            // 역주행 차량 표시
            rectWrongWayCar.Visibility = (_currentData?.IsWrongWay ?? false) ? Visibility.Visible : Visibility.Hidden;

            if (_currentData != null)
            {
                txtTemperature.Text = $"{_currentData.Temperature:F1} °C";
                txtRainfall.Text = $"{_currentData.Rainfall:F1} mm/h";
                txtWindSpeed.Text = $"{_currentData.WindSpeed:F1} m/s";
                txtVehicleSpeed.Text = $"{_currentData.VehicleSpeed:F1} km/h";
                txtAvgSpeed.Text = $"GPS 평균속도: {_currentData.AvgSpeed_GPS:F1} km/h";

                // 역주행 상태 표시
                txtWrongWay.Text = _currentData.IsWrongWay ? "역주행: 감지됨! ⚠" : "역주행: 없음";
                txtWrongWay.Foreground = _currentData.IsWrongWay ? _brushRed : _brushGreen;

                // 보행자 상태 표시
                txtPedestrian.Text = _currentData.IsPedestrianRemaining ? "잔류 보행자: 있음 ⚠" : "잔류 보행자: 없음";
                txtPedestrian.Foreground = _currentData.IsPedestrianRemaining ? _brushYellow : _brushGreen;
            }
            txtRoadCondition.Text = currentRoadCondition;

            // Road Safety Score 표시
            txtRoadSafetyScore.Text = $"Road Safety Score: {_currentSafetyScore}";
            // 점수에 따라 색상 변경
            if (_currentSafetyScore >= 100) // 긴급
                txtRoadSafetyScore.Foreground = _brushRed;
            else if (_currentSafetyScore >= 40) // 환경위험
                txtRoadSafetyScore.Foreground = (Brush)new BrushConverter().ConvertFrom("#FF9500");
            else if (_currentSafetyScore >= 10) // 혼잡
                txtRoadSafetyScore.Foreground = _brushYellow;
            else // 정상
                txtRoadSafetyScore.Foreground = _brushGreen;

            switch (currentRoadCondition)
            {
                case "건조": bdRoadCondition.Background = _bgDry; txtRoadCondition.Foreground = _brushWhite; break;
                case "습윤": bdRoadCondition.Background = _bgWet; txtRoadCondition.Foreground = _textWet; break;
                case "결빙": bdRoadCondition.Background = _bgIce; txtRoadCondition.Foreground = _textIce; break;
                case "적설": bdRoadCondition.Background = _bgSnow; txtRoadCondition.Foreground = _brushWhite; break;
                default: bdRoadCondition.Background = _brushInactive; txtRoadCondition.Foreground = _brushWhite; break;
            }

            if (isWrongWayActive)
            {
                txtActiveControl.Text = "긴급 제어: 역주행 감지됨! (3초간 정지)";
                txtActiveControl.Foreground = _brushRed;

                btnTriggerWrongWay.Content = "긴급 제어 가동 중...";
                btnTriggerWrongWay.Background = _brushInactive;
            }
            else
            {
                txtActiveControl.Text = _forceBlackIce ? "환경 위험: 결빙 주의 모드" : "정상 제어 중";
                txtActiveControl.Foreground = _forceBlackIce ? _brushYellow : _brushGreen;

                btnTriggerWrongWay.Content = "역주행 시뮬레이션 발생";
                btnTriggerWrongWay.Background = _brushRed;
            }

            if (_forceBlackIce)
            {
                btnToggleBlackIce.Background = _brushRed;
                btnToggleBlackIce.Content = "블랙아이스 강제 해제";
            }
            else
            {
                btnToggleBlackIce.Background = (Brush)new BrushConverter().ConvertFrom("#FF9500");
                btnToggleBlackIce.Content = "블랙아이스 모드 강제 실행";
            }
        }

        private void UpdateSpeedButtonsUI()
        {
            btnSpeed1x.Background = _currentSpeed == 1 ? _brushGreen : _brushInactive;
            btnSpeed2x.Background = _currentSpeed == 2 ? _brushGreen : _brushInactive;
            btnSpeed4x.Background = _currentSpeed == 4 ? _brushGreen : _brushInactive;
        }

        /// <summary>
        /// 그래프 그리기 (기온과 강수량)
        /// </summary>
        private void DrawGraph()
        {
            canvasGraph.Children.Clear();

            if (_temperatureHistory.Count == 0) return;

            double canvasWidth = canvasGraph.ActualWidth > 0 ? canvasGraph.ActualWidth : 350;
            double canvasHeight = canvasGraph.ActualHeight > 0 ? canvasGraph.ActualHeight : 220;
            double padding = 40;
            double graphWidth = canvasWidth - 2 * padding;
            double graphHeight = canvasHeight - 2 * padding;

            // 축 그리기
            Line xAxis = new Line { X1 = padding, Y1 = canvasHeight - padding, X2 = canvasWidth - padding, Y2 = canvasHeight - padding, Stroke = (Brush)new BrushConverter().ConvertFrom("#505070"), StrokeThickness = 2 };
            Line yAxis = new Line { X1 = padding, Y1 = padding, X2 = padding, Y2 = canvasHeight - padding, Stroke = (Brush)new BrushConverter().ConvertFrom("#505070"), StrokeThickness = 2 };
            canvasGraph.Children.Add(xAxis);
            canvasGraph.Children.Add(yAxis);

            // 축 레이블
            TextBlock xLabel = new TextBlock { Text = "시간 →", Foreground = (Brush)new BrushConverter().ConvertFrom("#909090"), FontSize = 11 };
            Canvas.SetLeft(xLabel, canvasWidth - 50);
            Canvas.SetTop(xLabel, canvasHeight - 20);
            canvasGraph.Children.Add(xLabel);

            TextBlock yLabel = new TextBlock { Text = "온도 ↑", Foreground = (Brush)new BrushConverter().ConvertFrom("#909090"), FontSize = 11 };
            Canvas.SetLeft(yLabel, 5);
            Canvas.SetTop(yLabel, 5);
            canvasGraph.Children.Add(yLabel);

            // 기온 범위 설정
            double minTemp = _temperatureHistory.Min();
            double maxTemp = _temperatureHistory.Max();
            if (maxTemp == minTemp) maxTemp = minTemp + 1;

            // 기온 그래프 그리기 (파란색 - 선굵기 4)
            for (int i = 0; i < _temperatureHistory.Count - 1; i++)
            {
                double x1 = padding + (i / (double)(_temperatureHistory.Count - 1)) * graphWidth;
                double x2 = padding + ((i + 1) / (double)(_temperatureHistory.Count - 1)) * graphWidth;

                double y1 = canvasHeight - padding - ((_temperatureHistory[i] - minTemp) / (maxTemp - minTemp)) * graphHeight;
                double y2 = canvasHeight - padding - ((_temperatureHistory[i + 1] - minTemp) / (maxTemp - minTemp)) * graphHeight;

                Line line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = (Brush)new BrushConverter().ConvertFrom("#64B5F6"), StrokeThickness = 4 };
                canvasGraph.Children.Add(line);
            }

            // 강수량 그래프 그리기 (초록색 - 선굵기 4, 스케일링)
            double maxRainfall = Math.Max(_rainfallHistory.Max() + 1, 1);
            for (int i = 0; i < _rainfallHistory.Count - 1; i++)
            {
                double x1 = padding + (i / (double)(_rainfallHistory.Count - 1)) * graphWidth;
                double x2 = padding + ((i + 1) / (double)(_rainfallHistory.Count - 1)) * graphWidth;

                double y1 = canvasHeight - padding - (_rainfallHistory[i] / maxRainfall) * (graphHeight * 0.4);
                double y2 = canvasHeight - padding - (_rainfallHistory[i + 1] / maxRainfall) * (graphHeight * 0.4);

                Line line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = (Brush)new BrushConverter().ConvertFrom("#81C784"), StrokeThickness = 4 };
                canvasGraph.Children.Add(line);
            }
        }

        /// <summary>
        /// 그래프 통계 업데이트
        /// </summary>
        private void UpdateGraphStats()
        {
            if (_temperatureHistory.Count == 0) return;

            double maxTemp = _temperatureHistory.Max();
            double minTemp = _temperatureHistory.Min();
            double totalRainfall = _rainfallHistory.Sum();

            txtGraphStats.Text = $"최고기온: {maxTemp:F1} °C | 최저기온: {minTemp:F1} °C | 누적강수: {totalRainfall:F1} mm";
        }

        /// <summary>
        /// 하루 끝난 후 도로 상황 초기화 (차들은 유지)
        /// </summary>
        private void ResetRoadState()
        {
            // 신호등 모두 빨강으로
            lightN_Red.Fill = lightS_Red.Fill = lightE_Red.Fill = lightW_Red.Fill = _brushRed;
            lightN_Green.Fill = lightS_Green.Fill = lightE_Green.Fill = lightW_Green.Fill = _dimGreen;

            // 도로 상태 초기화
            txtRoadCondition.Text = "건조";
            bdRoadCondition.Background = _bgDry;
            txtRoadCondition.Foreground = _brushWhite;

            // 상태 초기화
            _currentSafetyScore = 0;
            _currentEfficiencyScore = 100;
            _forceBlackIce = false;
            _emergencyTimer = 0;

            // UI 초기화
            btnToggleBlackIce.Content = "블랙아이스 모드 강제 실행";
            btnToggleBlackIce.Background = (Brush)new BrushConverter().ConvertFrom("#FF9500");
            txtActiveControl.Text = "정상 제어 중";
            txtActiveControl.Foreground = _brushGreen;
            txtRoadSafetyScore.Text = "Road Safety Score: 0";
            txtRoadSafetyScore.Foreground = _brushGreen;

            // 블랙아이스 오버레이 제거
            rectBlackIceOverlay.Opacity = 0;
            rectWrongWayCar.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// 30FPS로 렌더링
        /// </summary>
        private void RenderTimer_Tick(object sender, EventArgs e)
        {
            // 시뮬레이션 진행 중이 아니면 차량 동작 안 함
            if (!_isDayRunning) return;
            string currentRoadCondition = _forceBlackIce ? "결빙" : (_currentData?.RoadCondition ?? "건조");
            double conditionMultiplier = (currentRoadCondition == "결빙" || currentRoadCondition == "적설") ? 1.5 : 1.0;

            // 배속 반영
            // moveSpeed : 이동속도, 배속에 비례해서 더 빨라짐            (곱셈)
            // Threshold : 신호주기/쿨다운, 배속이 빠르면 신호주기 짧아짐 (나눗셈)
            int calculatedThreshold = (int)(_baseSignalThreshold * _policySignalMultiplier * conditionMultiplier);
            double moveSpeed = (_baseMoveSpeed * _policySpeedMultiplier) * _currentSpeed;
            int dynamicSignalThreshold = calculatedThreshold / _currentSpeed;
            int dynamicCooldown = (int)(_baseSpawnCooldown / _policySpeedMultiplier) / _currentSpeed;

            bool isEmergencyStop = false;
            if (_emergencyTimer > 0)
            {
                isEmergencyStop = true;
                _emergencyTimer--;
                if (_emergencyTimer == 0) UpdateUI();
            }

            if (_spawnCooldownN > 0) _spawnCooldownN--;
            if (_spawnCooldownS > 0) _spawnCooldownS--;
            if (_spawnCooldownE > 0) _spawnCooldownE--;
            if (_spawnCooldownW > 0) _spawnCooldownW--;

            // 평시에는 신호 타이머
            if (!isEmergencyStop)
            {
                _signalTimer++;
                if (_signalTimer > dynamicSignalThreshold)
                {
                    _isNSGreen = !_isNSGreen;
                    _signalTimer = 0;
                }

                UpdateTrafficLightsUI();

                double remainingSec = (dynamicSignalThreshold - _signalTimer) / 30.0;
                txtSignalTimer.Text = $"다음 신호까지: {remainingSec:F1}초";
                pbSignalTimer.Maximum = dynamicSignalThreshold;
                pbSignalTimer.Value = _signalTimer;
                pbSignalTimer.Foreground = (conditionMultiplier > 1.0 || _policySignalMultiplier > 1.0) ? _textIce : _brushGreen;
            }
            // 긴급 정지는 3초 카운트다운
            else
            {
                double emergencyRemainingSec = _emergencyTimer / 30.0;
                txtSignalTimer.Text = $"긴급 정지 ({emergencyRemainingSec:F1}초 후 해제)";
                pbSignalTimer.Maximum = 90;
                pbSignalTimer.Value = _emergencyTimer;
                pbSignalTimer.Foreground = _brushRed;

                UpdateTrafficLightsUI(forceAllRed: true);
            }

            // 꼬리물기 방지
            // 통과 중인 차량이 있는데 다른 방향에서 출발하면 안됨
            bool isOccupiedByEW = false; bool isOccupiedByNS = false;
            for (int j = 0; j < MAX_CARS; j++)
            {
                var c = _carPool[j]; if (!c.IsActive) continue;
                if ((c.Direction == "E" || c.Direction == "W") && (c.X > 180 && c.X < 300)) isOccupiedByEW = true;
                if ((c.Direction == "N" || c.Direction == "S") && (c.Y > 180 && c.Y < 300)) isOccupiedByNS = true;
            }

            for (int i = 0; i < MAX_CARS; i++)
            {
                var car = _carPool[i]; if (!car.IsActive) continue;

                bool canMove = true;

                // 차량의 다음 X, Y 좌표를 미리 계산해보고, 문제가 있으면 취소한다
                double nextX = car.Direction == "E" ? car.X - moveSpeed : (car.Direction == "W" ? car.X + moveSpeed : car.X);
                double nextY = car.Direction == "N" ? car.Y + moveSpeed : (car.Direction == "S" ? car.Y - moveSpeed : car.Y);

                // 정지조건 판단
                // 통과 중인데 긴급정지되면 마저 다 통과
                // 대기하러 오는데 긴급정지되면 대기선까지 와서 정지
                bool isRedLightForMe = false;
                if (car.Direction == "N" || car.Direction == "S") isRedLightForMe = !_isNSGreen || isEmergencyStop || isOccupiedByEW;
                else isRedLightForMe = _isNSGreen || isEmergencyStop || isOccupiedByNS;

                if (isRedLightForMe)
                {
                    // 정지선 앞에서 멈춤
                    if (car.Direction == "N" && car.Y < 170 && nextY >= 170) canMove = false;
                    else if (car.Direction == "S" && car.Y > 310 && nextY <= 310) canMove = false;
                    else if (car.Direction == "E" && car.X > 310 && nextX <= 310) canMove = false;
                    else if (car.Direction == "W" && car.X < 170 && nextX >= 170) canMove = false;
                }

                if (canMove)
                {
                    // 앞차랑 거리조절
                    for (int j = 0; j < MAX_CARS; j++)
                    {
                        if (i == j) continue; var other = _carPool[j]; if (!other.IsActive) continue;
                        if (car.Direction == "N" && other.Direction == "N" && other.Y > car.Y && other.Y - car.Y < 25) { canMove = false; break; }
                        if (car.Direction == "S" && other.Direction == "S" && other.Y < car.Y && car.Y - other.Y < 25) { canMove = false; break; }
                        if (car.Direction == "E" && other.Direction == "E" && other.X < car.X && car.X - other.X < 25) { canMove = false; break; }
                        if (car.Direction == "W" && other.Direction == "W" && other.X > car.X && other.X - car.X < 25) { canMove = false; break; }
                    }
                }

                if (canMove)
                {
                    // 말이 될때만 업데이트
                    car.X = nextX; car.Y = nextY;
                    car.Transform.X = car.X; car.Transform.Y = car.Y;
                }

                if (car.X < -50 || car.X > 550 || car.Y < -50 || car.Y > 550)
                {
                    car.IsActive = false; car.Shape.Visibility = Visibility.Hidden;
                }
            }

            // UI랑 계산결과 동기화
            if (_currentData != null) SyncAndReportTraffic(dynamicCooldown);
        }

        /// <summary>
        /// 내부 함수 및 UI 데이터 동기화
        /// </summary>
        private void SyncAndReportTraffic(int dynamicCooldown)
        {
            // 데이터와 UI 차량 수를 동기화
            int countN = 0, countS = 0, countE = 0, countW = 0;
            for (int i = 0; i < MAX_CARS; i++)
            {
                var car = _carPool[i]; if (!car.IsActive) continue;
                if (car.Direction == "N") countN++; if (car.Direction == "S") countS++; if (car.Direction == "E") countE++; if (car.Direction == "W") countW++;
            }

            TrySpawnIfNeeded("N", _currentData.WaitingCars_N, countN, dynamicCooldown);
            TrySpawnIfNeeded("S", _currentData.WaitingCars_S, countS, dynamicCooldown);
            TrySpawnIfNeeded("E", _currentData.WaitingCars_E, countE, dynamicCooldown);
            TrySpawnIfNeeded("W", _currentData.WaitingCars_W, countW, dynamicCooldown);

            UpdateCarCountText(txtCarsN, countN); UpdateCarCountText(txtCarsS, countS); UpdateCarCountText(txtCarsE, countE); UpdateCarCountText(txtCarsW, countW);
        }

        private void TrySpawnIfNeeded(string dir, int target, int current, int cooldownLimit)
        {
            // 자리가 새로 생겼을 때만 차량 생성
            int visualTarget = Math.Min(target, 8);
            if (current >= visualTarget) return;

            if ((dir == "N" && _spawnCooldownN > 0) || (dir == "S" && _spawnCooldownS > 0) || (dir == "E" && _spawnCooldownE > 0) || (dir == "W" && _spawnCooldownW > 0)) return;

            // 차량 생성 시, 겹쳐서 나오면 안되니까 구분해서 생성
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
            // 미리 만들어져있는 차를 갖다씀
            CarUI car = null;
            for (int i = 0; i < MAX_CARS; i++) { if (!_carPool[i].IsActive) { car = _carPool[i]; break; } }
            if (car == null) return;

            car.IsActive = true; car.Direction = dir; car.Shape.Visibility = Visibility.Visible;
            if (dir == "N") { car.Shape.Width = 10; car.Shape.Height = 20; car.X = 210; car.Y = -20; }
            else if (dir == "S") { car.Shape.Width = 10; car.Shape.Height = 20; car.X = 280; car.Y = 500; }
            else if (dir == "E") { car.Shape.Width = 20; car.Shape.Height = 10; car.X = 500; car.Y = 210; }
            else if (dir == "W") { car.Shape.Width = 20; car.Shape.Height = 10; car.X = -20; car.Y = 280; }

            car.Transform.X = car.X; car.Transform.Y = car.Y;
        }

        private void UpdateCarCountText(TextBlock txtBlock, int count)
        {
            txtBlock.Text = $"{count}대";
            if (count >= 10) txtBlock.Foreground = _brushRed; else if (count >= 5) txtBlock.Foreground = _brushYellow; else txtBlock.Foreground = _brushGreen;
        }

        private void UpdateTrafficLightsUI(bool forceAllRed = false)
        {
            // 신호등 동기화 (N/S/E/W 4개 방향)
            if (forceAllRed)
            {
                // 긴급 상황: 전방향 적색
                lightN_Red.Fill = lightS_Red.Fill = lightE_Red.Fill = lightW_Red.Fill = _brushRed;
                lightN_Green.Fill = lightS_Green.Fill = lightE_Green.Fill = lightW_Green.Fill = _dimGreen;
            }
            else if (_isNSGreen)
            {
                // 남북 녹색, 동서 적색
                lightN_Red.Fill = lightS_Red.Fill = _dimRed;
                lightN_Green.Fill = lightS_Green.Fill = _brushGreen;

                lightE_Red.Fill = lightW_Red.Fill = _brushRed;
                lightE_Green.Fill = lightW_Green.Fill = _dimGreen;
            }
            else
            {
                // 동서 녹색, 남북 적색
                lightN_Red.Fill = lightS_Red.Fill = _brushRed;
                lightN_Green.Fill = lightS_Green.Fill = _dimGreen;

                lightE_Red.Fill = lightW_Red.Fill = _dimRed;
                lightE_Green.Fill = lightW_Green.Fill = _brushGreen;
            }
        }

        /// <summary>
        /// 버튼 구현
        /// </summary>
        private void BtnToggleBlackIce_Click(object sender, RoutedEventArgs e) { SetBlackIceMode(!_forceBlackIce); }
        private void BtnToggleRushHour_Click(object sender, RoutedEventArgs e)
        {
            _isRushHourEnabled = !_isRushHourEnabled;
            _dataGenerator?.SetRushHourMode(_isRushHourEnabled);
            btnToggleRushHour.Content = _isRushHourEnabled ? "혼잡 시간 모드 해제" : "혼잡 시간 모드 활성";
            btnToggleRushHour.Background = _isRushHourEnabled ? (Brush)new BrushConverter().ConvertFrom("#6750A4") : _brushInactive;
        }
        private void BtnTriggerWrongWay_Click(object sender, RoutedEventArgs e) { TriggerEmergencyStop(); }

        private void BtnStartDay_Click(object sender, RoutedEventArgs e)
        {
            if (_isDayRunning) return; // 이미 실행 중이면 무시

            _isDayRunning = true;
            _dataManager.ClearHistory();          // 새 하루 시작 전 데이터 초기화
            _temperatureHistory.Clear();          // 그래프 데이터 초기화
            _rainfallHistory.Clear();
            canvasGraph.Children.Clear();         // 캔버스 초기화
            _dataGenerator.StartDay();            // 하루 시뮬레이션 시작

            btnStartDay.Content = "시뮬레이션 진행 중...";
            btnStartDay.IsEnabled = false;
        }

        private void OnDayCompleted()
        {
            this.Dispatcher.Invoke(() =>
            {
                _isDayRunning = false;

                // 하루 완료 시 자동 저장
                bool isSaved = _dataManager.AutoSaveDay();

                // 도로 상황 초기화 (신호등, 상태 등)
                ResetRoadState();

                // 그래프 데이터 유지 (최종 상태 표시)

                // 상태 업데이트 (저장 완료)

                // 버튼 복원
                btnStartDay.Content = "▶ 하루 시뮬레이션 시작";
                btnStartDay.IsEnabled = true;

                MessageBox.Show(
                    isSaved ? "하루 시뮬레이션이 완료되었습니다.\n데이터가 자동으로 저장되었습니다." : "하루 시뮬레이션이 완료되었습니다.\n데이터 저장에 실패했습니다.",
                    "시뮬레이션 완료",
                    MessageBoxButton.OK,
                    isSaved ? MessageBoxImage.Information : MessageBoxImage.Warning);
            });
        }

        private void BtnSpeed1x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 1; _dataGenerator?.SetSpeed(1); UpdateSpeedButtonsUI(); }
        private void BtnSpeed2x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 2; _dataGenerator?.SetSpeed(2); UpdateSpeedButtonsUI(); }
        private void BtnSpeed4x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 4; _dataGenerator?.SetSpeed(4); UpdateSpeedButtonsUI(); }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { _dataGenerator?.Stop(); _renderTimer?.Stop(); }
    }
}