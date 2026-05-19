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
        private SensorData _currentData;

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
            btnTriggerWrongWay.Click += BtnTriggerWrongWay_Click;

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
            _dataGenerator.SensorDataUpdated += (data) =>
            {
                _currentData = data;
                this.Dispatcher.Invoke(() =>
                {
                    if (data.IsWrongWay) TriggerEmergencyStop(); // 모듈 사용
                    UpdateUI();
                });
            };
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

            if (_currentData != null)
            {
                txtTemperature.Text = $"{_currentData.Temperature:F1} °C";
                txtRainfall.Text = $"{_currentData.Rainfall:F1} mm/h";
            }
            txtRoadCondition.Text = currentRoadCondition;

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
        /// 30FPS로 렌더링
        /// </summary>
        private void RenderTimer_Tick(object sender, EventArgs e)
        {
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
            // 신호등 동기화
            if (forceAllRed)
            {
                lightNE_Red.Fill = lightSW_Red.Fill = lightNW_Red.Fill = lightSE_Red.Fill = _brushRed;
                lightNE_Green.Fill = lightSW_Green.Fill = lightNW_Green.Fill = lightSE_Green.Fill = _dimGreen;
            }
            else if (_isNSGreen)
            {
                lightNE_Red.Fill = lightSW_Red.Fill = _dimRed; lightNE_Green.Fill = lightSW_Green.Fill = _brushGreen;
                lightNW_Red.Fill = lightSE_Red.Fill = _brushRed; lightNW_Green.Fill = lightSE_Green.Fill = _dimGreen;
            }
            else
            {
                lightNE_Red.Fill = lightSW_Red.Fill = _brushRed; lightNE_Green.Fill = lightSW_Green.Fill = _dimGreen;
                lightNW_Red.Fill = lightSE_Red.Fill = _dimRed; lightNW_Green.Fill = lightSE_Green.Fill = _brushGreen;
            }
        }

        /// <summary>
        /// 버튼 구현 
        /// </summary>
        private void BtnToggleBlackIce_Click(object sender, RoutedEventArgs e) { SetBlackIceMode(!_forceBlackIce); }
        private void BtnTriggerWrongWay_Click(object sender, RoutedEventArgs e) { TriggerEmergencyStop(); }

        private void BtnSpeed1x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 1; _dataGenerator?.SetSpeed(1); UpdateSpeedButtonsUI(); }
        private void BtnSpeed2x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 2; _dataGenerator?.SetSpeed(2); UpdateSpeedButtonsUI(); }
        private void BtnSpeed4x_Click(object sender, RoutedEventArgs e) { _currentSpeed = 4; _dataGenerator?.SetSpeed(4); UpdateSpeedButtonsUI(); }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { _dataGenerator?.Stop(); _renderTimer?.Stop(); }
    }
}