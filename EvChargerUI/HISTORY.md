# 변경 이력 (HISTORY)

## No.32 - 충전 중 관리자 페이지에서 UI 프로그램 실행 시 충전 중단되나 충전정보 사라짐

> 충전중 관리자 페이지 진입 차단 (최소 변경)

### 목표
- **충전중 화면(= 충전 진행 상태)에서는 관리자 페이지 진입을 불가능**하게 차단
- 코드 변경을 최소화하고, 기존 충전/팝업/타이머 흐름에 **사이드이펙트가 생기지 않도록** 단일 진입점에 가드 추가

### 수정 파일
- `ViewModels/MainViewModel.cs`

### 변경 내용 요약
- **관리자 진입 트리거 2곳**과 **최종 관리자 창 오픈 직전 1곳**에 “충전중이면 return” 가드를 추가
- 충전중 판정은 `ChargeSequence.Charging`만 사용(과차단 방지)

### 상세 변경 포인트
1) `ConfirmPasswordPopupView(object param)`
```csharp
        private void ConfirmPasswordPopupView(object param)
        {
            #region NEW
            if (IsAnyChannelCharging())
            {
                ClosePopupInternal();
                return;
            }
            #endregion
```
- **변경**: 비밀번호가 맞아 `ShowAdminWindow()`를 호출하기 직전, 충전중이면 팝업을 닫고 종료하도록 가드 추가
- **이유**: 팝업이 이미 떠 있거나(타이밍/경합), 다른 경로로 호출되더라도 **최종 진입을 확실히 차단**하기 위한 안전장치

2) `DbClickHiddenBtn(object param)`
```csharp
        private void DbClickHiddenBtn(object param)
        {
            try
            {
                var logger = ((App)Application.Current).AppLogger;
                var delta = (DateTime.Now - _lastClickTime).TotalMilliseconds;
                logger.Info($"[AdminIconClick] invoked. deltaMs={delta:0}");
            }
            catch
            {
                // 로깅 실패는 UI 동작에 영향 주지 않음
            }

            #region NEW
            if (IsAnyChannelCharging())
            {
                return;
            }
            #endregion
```

- **변경**: 숨김 버튼 더블클릭으로 비밀번호 팝업을 열기 전에, 충전중이면 즉시 종료하도록 가드 추가
- **이유**: 충전중에 비밀번호 팝업 자체가 뜨지 않게 하여 UI 플로우 교란을 최소화

3) `OpenPasswordPopup(object param)` (`Ctrl+O` 단축키 바인딩 경로)
```csharp
        private void OpenPasswordPopup(object param)
        {
            #region NEW
            if (IsAnyChannelCharging())
            {
                return;
            }
            #endregion

            PopupInputPassword();
        }
```

- **변경**: `Ctrl+O` 등으로 비밀번호 팝업을 직접 여는 경로도 충전중이면 즉시 종료하도록 가드 추가
- **이유**: 입력 경로(키보드/숨김버튼)가 달라도 동일 정책으로 차단되도록 일관성 확보

4) `IsAnyChannelCharging()` (신규 private 헬퍼)
```csharp
        #region NEW
        private bool IsAnyChannelCharging()
        {
            try
            {
                var leftVm = LeftChargerView?.DataContext as ChargerViewModel;
                if (leftVm?.CurrentChargeSequence == ChargeSequence.Charging)
                {
                    return true;
                }

                var rightVm = RightChargerView?.DataContext as ChargerViewModel;
                if (rightVm?.CurrentChargeSequence == ChargeSequence.Charging)
                {
                    return true;
                }
            }
            catch
            {
                // 상태 확인 실패 시에는 보수적으로 차단하지 않고 기존 동작 유지
            }
~~~~
            return false;
        }
        #endregion
```
- **역할**: 좌/우 채널의 `CurrentChargeSequence`가 `ChargeSequence.Charging`인지 확인
- **설계 이유**
  - “충전중 화면”을 코드 상에서 가장 보수적이고 명확하게 표현되는 상태(`Charging`)로 한정해 **과차단(PlugConnector/Completed 등)**을 피함
  - 상태 확인 중 예외 발생 시에는 기존 동작을 유지(차단하지 않음)하여 **예외로 인해 UI 동작이 막히는 부작용**을 방지

## No.12 - 예약 취소 시 SMS 미전송 하는 현상

> 예약 취소(사용자/자동) 시 SENDSMS msg=2 전송 추가

### 목표
- **예약 취소 버튼**으로 취소 시, 사양서 기준 `SENDSMS`의 `msg=2`(예약취소) 메시지가 전송되도록 수정
- **예약 대기 타이머 자동 취소** 케이스에서도 동일하게 `msg=2`가 전송되도록 일관성 확보

### 수정 파일
- `Models/Charger.cs`
- `ViewModels/ChargerViewModel.cs`

### 변경 내용 요약
- `Charger.SendSendSmsResvCancel()` 신규 추가: `SENDSMS(msg=2)` 호출 래핑
- 예약 취소 처리 성공 시점에 `SendSendSmsResvCancel()` 호출을 연결
  - 사용자 취소: `CancelReservationNo(...)`
  - 자동 취소: `ReservationWaitingTimer_Tick(...)`

### 상세 변경 포인트
1) `ChargerViewModel.cs` > `ReservationWatingTimer_Tick(object sender, EventArgs e)`
```csharp
        private async void ReservationWaitingTimer_Tick(object sender, EventArgs e)
        {
            _logger.Info("[UI] Reservation waiting timer expired. Auto-cancelling reservation due to no response.");
            DisposeReservationWaitingTimer();
            
            // 예약 취소 로직 실행
            if (!string.IsNullOrEmpty(_chargerChannel.ReservationPhoneNo))
            {
                #region NEW
                bool cancelOk = _charger.SendCancelReservation(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                if (cancelOk)
                {
                    _charger.SendSendSmsResvCancel(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                }
                #endregion
                // 취소 요청 후 서버 처리 시간을 고려하여 약간 대기
                await Task.Delay(500);
            }
            
            // 예약 상태 다시 확인하여 취소 반영 여부 확인
            _isReservationAuth = false;
            _chargerChannel.InitReservationInfo();
            OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
            OnPropertyChanged(nameof(ReservationWaitingText));

            // 자동 취소 팝업 표시 (큰 팝업)
            _parentViewModel.PopupAutoCancelReservation(this);
        }
```
- 자동 취소 시에도 동일하게 성공 시 `SendSendSmsResvCancel(...)` 호출

2) `ChargerViewModel.cs` > `CancelReservationNo(object param)`
```csharp
        private async void CancelReservationNo(object param) 
        {
            string reservationNumber = param as string;
            if (reservationNumber == null || reservationNumber.Length != 4) return;

            if (_chargerChannel.ReservationNo != reservationNumber)
            {
                _parentViewModel.PopupWrongReservationNumber(this);
            }
            else
            {
                bool cancelOk = _charger.SendCancelReservation(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                if (cancelOk)
                {
                    _charger.SendSendSmsResvCancel(_chargerChannel.ChannelNo, _chargerChannel.ReservationPhoneNo);
                }
                // 취소 요청 후 서버 처리 시간을 고려하여 약간 대기
                await Task.Delay(500);
                
                // 예약 상태 다시 확인하여 취소 반영 여부 확인
                _isReservationAuth = false;
                _chargerChannel.InitReservationInfo();
                OnPropertyChanged(nameof(ReservationWaitingPhoneLast4));
                OnPropertyChanged(nameof(ReservationWaitingText));
                
                _parentViewModel.PopupCancelReservation(this);
            }
        }
```

- `SendCancelReservation(...)` 성공(`true`)일 때 `SendSendSmsResvCancel(...)` 호출

3) 예약 취소 성공 시 SMS 전송 연결
```csharp
        public bool SendSendSmsResvCancel(int channelNo, string phoneNo)
        {
            ChargerChannel ch = _channels[channelNo];
            return _evCommService.SendSendSMS(ch.StationId, ch.ChargerId, phoneNo, "2", "SMS", null, ch.ChargerId, null, null, null);
        }
```

- **전송값**: `msg="2"`, `msg_type="SMS"`, `Data1=null`, `Data2=charger_id` (사양서 기준)

## No.14 - 충전 시작 시 Reservation waiting timer 만료로 충전 중단되는 현상

> 예약 대기 타이머 만료 시 “충전 진행 중 초기화”로 이어지는 경로 차단 (최소 변경)

### 목표
- `ChargeSequence.WaitReservation`에서 시작된 예약 대기 타이머가 **충전/커넥터 단계까지 남아** 만료될 경우,
  - 자동 취소 팝업(`PopupAutoCancelReservation`) → 30초 후 `ConfirmReservationCancelCommand` 실행 → `InitializeCharger()` 호출로 이어져 **충전이 중단될 수 있는 문제**를 차단
- 기존 플로우를 바꾸지 않으면서, **최소한의 변경(단일 진입점 + Tick 가드)**으로 부작용을 줄임

### 수정 파일
- `ViewModels/ChargerViewModel.cs`

### 변경 내용 요약
- `RefreshChargerSequence()` 초입에서 **WaitReservation이 아니면 예약 대기 타이머를 즉시 정리**하도록 추가
- `ReservationWaitingTimer_Tick(...)`에서 **WaitReservation 상태가 아니면 무시**하도록 가드 추가

### 상세 변경 포인트
1) `ChargerViewModel.cs` > `RefreshChargerSequence()`
```csharp
        private void RefreshChargerSequence()
        {
            // 무한 루프 방지
            if (_isRefreshing)
                return;
                
            _isRefreshing = true;
            try
            {
                #region NEW
                if (CurrentChargeSequence != ChargeSequence.WaitReservation)
                {
                    DisposeReservationWaitingTimer();
                }
                #endregion
                
                DisposePaymentMethodSelectTimer();
                ...
```
- **변경**: `RefreshChargerSequence()` 진입 시점에 “현재 시퀀스가 WaitReservation이 아니면 타이머 Dispose” 처리
- **이유**: 기존에는 `SelectConnector`에서만 정리되어, `WaitReservation → PlugConnector/Charging` 등 **SelectConnector를 거치지 않는 전환**에서 타이머가 남을 수 있었음

2) `ChargerViewModel.cs` > `ReservationWaitingTimer_Tick(object sender, EventArgs e)`
```csharp
        private async void ReservationWaitingTimer_Tick(object sender, EventArgs e)
        {
            #region NEW
            if (CurrentChargeSequence != ChargeSequence.WaitReservation)
            {
                _logger.Warn($"[UI] Reservation waiting timer fired outside WaitReservation (CurrentChargeSequence={CurrentChargeSequence}). Disposing timer and ignoring.");
                DisposeReservationWaitingTimer();
                return;
            }
            #endregion

            _logger.Info("[UI] Reservation waiting timer expired. Auto-cancelling reservation due to no response.");
            DisposeReservationWaitingTimer();
            ...
        }
```
- **변경**: Tick 시점에 현재 시퀀스를 검사하여 `WaitReservation`이 아니면 **취소 로직/팝업을 실행하지 않고 종료**
- **효과**: 타이머가 어떤 이유로 남아 있어도, **충전 중단(InitializeCharger) 트리거로 이어지는 경로 자체를 차단**
