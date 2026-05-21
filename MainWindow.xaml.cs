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
    public partial class MainWindow : Window
    {
        #region 핵심 모듈 및 변수

        private DataGenerator _dataGenerator;
        private TrafficController _trafficController;
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
        }

        /// <summary>
        /// 점수에 따른 차량 속도 가감 
        /// </summary>
        public void ApplyTrafficPolicy(double speedMultiplier) { _policySpeedMultiplier = speedMultiplier; }

        /// <summary>
        /// 역주행 감지 시 3초 긴급 정지
        /// </summary>
        public void TriggerEmergencyStop() {
            if (_emergencyTimer == 0) {
                _emergencyTimer = 90;
                if (_trafficController != null) _trafficController.ForceWrongWay = true;
                UpdateUI();
            }
        }

        /// <summary>
        /// 블랙아이스 모드 수동 전환
        /// </summary>
        public void SetBlackIceMode(bool isActive) {
            _forceBlackIce = isActive;
            if (_trafficController != null) _trafficController.ForceBlackIce = isActive;
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
        }

        /// <summary>
        /// UI에서 DataGenerator 및 TrafficController와 연동
        /// </summary>
        private void InitializeSystem()
        {
            _dataGenerator = new DataGenerator();
            _trafficController = new TrafficController(_dataGenerator);
            _dataManager = new DataManager();

            _trafficController.TrafficUpdated += HandleTrafficUpdate;

            // 센서 데이터가 들어오면 UI 스레드에서 화면 갱신
            _dataGenerator.SensorDataUpdated += (data) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _currentData = data;
                        _temperatureHistory.Add(data.Temperature);
                        _rainfallHistory.Add(data.Rainfall);

                        if (data.IsWrongWay) TriggerEmergencyStop();

                        UpdateUI();
                        DrawGraph();
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
        }

        #endregion

        #region UI 시각화 및 렌더링

        /// <summary> 
        /// 대시보드 텍스트 및 색상 갱신
        /// </summary>
        private void UpdateUI()
        {
            string currentRoadCondition = _forceBlackIce ? "결빙" : (_currentData?.RoadCondition ?? "대기 중");
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
                bdActiveControl.Background = (Brush)new BrushConverter().ConvertFrom("#102020");
            }
            else if (isEmergency)
            {
                txtActiveControl.Text = "🚨 긴급 제어: 전 방향 정지!";
                txtActiveControl.Foreground = _brushRed;
                bdActiveControl.BorderBrush = _brushRed; bdActiveControl.Background = (Brush)new BrushConverter().ConvertFrom("#201010");
                btnTriggerWrongWay.Content = "긴급 제어 가동 중..."; btnTriggerWrongWay.Background = _brushInactive;
            }
            else
            {
                if (_lastTrafficArgs?.CurrentState == TrafficState.Congested)
                {
                    txtActiveControl.Text = "⚠️ 교통 혼잡 (신호 연장)";
                    txtActiveControl.Foreground = _brushYellow;
                    bdActiveControl.BorderBrush = _brushYellow; bdActiveControl.Background = (Brush)new BrushConverter().ConvertFrom("#202010");
                }
                else if (_lastTrafficArgs?.CurrentState == TrafficState.EnvironmentalHazard || _forceBlackIce)
                {
                    txtActiveControl.Text = "❄️ 환경 위험 (안전 제어)";
                    txtActiveControl.Foreground = _textIce;
                    bdActiveControl.BorderBrush = _textIce; bdActiveControl.Background = (Brush)new BrushConverter().ConvertFrom("#102020");
                }
                else
                {
                    txtActiveControl.Text = "🟢 정상 제어 중";
                    txtActiveControl.Foreground = _brushGreen;
                    bdActiveControl.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#505070"); bdActiveControl.Background = (Brush)new BrushConverter().ConvertFrom("#202030");
                }
                btnTriggerWrongWay.Content = "🚨 강제 역주행 시뮬레이션"; btnTriggerWrongWay.Background = _brushRed;
            }

            btnToggleBlackIce.Background = _forceBlackIce ? _brushRed : (Brush)new BrushConverter().ConvertFrom("#FF9500");
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

        /// <summary>
        /// 꺾은선 그래프 렌더링
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
            double padding = 40;
            double graphWidth = canvasWidth - 2 * padding;
            double graphHeight = canvasHeight - 2 * padding;

            // X, Y축 그리기
            canvasGraph.Children.Add(new Line { X1 = padding, Y1 = canvasHeight - padding, X2 = canvasWidth - padding, Y2 = canvasHeight - padding, Stroke = _brushGraphAxis, StrokeThickness = 2 });
            canvasGraph.Children.Add(new Line { X1 = padding, Y1 = padding, X2 = padding, Y2 = canvasHeight - padding, Stroke = _brushGraphAxis, StrokeThickness = 2 });

            // 라벨 추가
            TextBlock xLabel = new TextBlock { Text = "시간 →", Foreground = _brushGraphLabel, FontSize = 11 };
            Canvas.SetLeft(xLabel, canvasWidth - 50); Canvas.SetTop(xLabel, canvasHeight - 20); canvasGraph.Children.Add(xLabel);

            TextBlock yLabel = new TextBlock { Text = "단위 ↑", Foreground = _brushGraphLabel, FontSize = 11 };
            Canvas.SetLeft(yLabel, 5); Canvas.SetTop(yLabel, 5); canvasGraph.Children.Add(yLabel);

            // 기온 포인트 계산
            double minTemp = _temperatureHistory.Min(); double maxTemp = _temperatureHistory.Max();
            if (maxTemp == minTemp) maxTemp = minTemp + 1;

            PointCollection tempPoints = new PointCollection();
            for (int i = 0; i < _temperatureHistory.Count; i++)
            {
                double x = padding + (i / (double)(_temperatureHistory.Count - 1)) * graphWidth;
                double y = canvasHeight - padding - ((_temperatureHistory[i] - minTemp) / (maxTemp - minTemp)) * graphHeight;
                tempPoints.Add(new Point(x, y));
            }
            // Polyline 사용
            canvasGraph.Children.Add(new Polyline { Points = tempPoints, Stroke = _brushGraphTemp, StrokeThickness = 4 });

            // 강수량 포인트 계산
            double maxRainfall = Math.Max(_rainfallHistory.Max() + 1, 1);
            PointCollection rainPoints = new PointCollection();
            for (int i = 0; i < _rainfallHistory.Count; i++)
            {
                double x = padding + (i / (double)(_rainfallHistory.Count - 1)) * graphWidth;
                double y = canvasHeight - padding - (_rainfallHistory[i] / maxRainfall) * (graphHeight * 0.4);
                rainPoints.Add(new Point(x, y));
            }
            canvasGraph.Children.Add(new Polyline { Points = rainPoints, Stroke = _brushGraphRain, StrokeThickness = 4 });
        }

        private void UpdateGraphStats()
        {
            if (_temperatureHistory.Count == 0) return;
            txtGraphStats.Text = $"최고기온: {_temperatureHistory.Max():F1} °C | 최저기온: {_temperatureHistory.Min():F1} °C | 누적강수: {_rainfallHistory.Sum():F1} mm";
        }

        #endregion

        #region 메인 시뮬레이션 및 렌더링

        /// <summary>
        /// 30FPS 주기 렌더링 (차량 이동, 정지, 생성)
        /// </summary>
        private void RenderTimer_Tick(object sender, EventArgs e)
        {
            if (!_isDayRunning) return;

            DateTime now = DateTime.Now;
            double realDeltaTime = (now - _lastTickTime).TotalSeconds;
            _lastTickTime = now;

            double simulatedDeltaTime = realDeltaTime * _currentSpeed;  // 배속 고려
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

            // 긴급 제어 타이머 처리
            bool isEmergencyStop = false;
            if (_emergencyTimer > 0)
            {
                isEmergencyStop = true;
                _emergencyTimer--;
                if (_emergencyTimer == 0)
                {
                    if (_trafficController != null) _trafficController.ForceWrongWay = false;   // 3초 지나면 강제상태 해제
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
                rectWrongWayCar.Visibility = Visibility.Visible;    // UI 시각화
                TriggerEmergencyStop(); // 긴급 정지 발동
                UpdateUI();
            }
            catch (Exception ex) { MessageBox.Show($"역주행 제어 실패: {ex.Message}"); }
        }

        private void BtnStartDay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isDayRunning) return;

                _isDayRunning = true;
                _dataManager?.ClearHistory();
                _temperatureHistory.Clear(); _rainfallHistory.Clear(); canvasGraph.Children.Clear();

                _lastTickTime = DateTime.Now;
                _dataGenerator?.StartDay();

                btnStartDay.Content = "시뮬레이션 진행 중..."; btnStartDay.IsEnabled = false;
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
                    bool isSaved = _dataManager.AutoSaveDay(); // 자동 저장 시도
                    ResetRoadState();

                    btnStartDay.Content = "▶ 하루 시뮬레이션 시작"; btnStartDay.IsEnabled = true;

                    if (isSaved) MessageBox.Show("하루 시뮬레이션 완료 및 데이터 자동 저장 성공!", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    else MessageBox.Show("시뮬레이션은 완료되었으나, 데이터 저장에 실패했습니다.\n저장 폴더 권한이나 디스크 용량을 확인하세요.", "저장 경고", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            btnToggleBlackIce.Content = "블랙아이스 모드 강제 실행"; btnToggleBlackIce.Background = (Brush)new BrushConverter().ConvertFrom("#FF9500");
            txtActiveControl.Text = "정상 제어 중"; txtActiveControl.Foreground = _brushGreen;
            txtRoadSafetyScore.Text = "0점"; txtRoadSafetyScore.Foreground = _brushWhite;

            rectBlackIceOverlay.Opacity = 0; rectWrongWayCar.Visibility = Visibility.Hidden;
            txtSignalTimer.Text = "대기 중"; pbSignalTimer.Value = 0;

            UpdateUI();
        }

        private void BtnSpeed1x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 1; _dataGenerator?.SetSpeed(1); UpdateSpeedButtonsUI(); }
        private void BtnSpeed2x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 2; _dataGenerator?.SetSpeed(2); UpdateSpeedButtonsUI(); }
        private void BtnSpeed4x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 4; _dataGenerator?.SetSpeed(4); UpdateSpeedButtonsUI(); }

        /// <summary>
        /// 리소스 해제
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _renderTimer?.Stop(); _renderTimer = null;
            _trafficController?.Dispose();
            _dataGenerator?.Dispose();
            _dataManager?.ClearHistory();
        }

        #endregion
    }
}