# 릴리즈 노트


## 📑 버전 목차

| 버전 | 날짜 | 핵심 테마 | 주요 변경 |
|------|------|-----------|----------|
| [v1.0.1.41](RELEASE_NOTES.md#v10141---2026-04-14) | 2026-04-14 | 파일 잠금 · 카운트다운 안정성 | 세션 파일 lock+임시파일 방식, Stopwatch 기반 카운트다운, 취소 처리 단일화 |
| [v1.0.1.39](RELEASE_NOTES.md#v10139---2025-02-06) | 2025-02-06 | 세션 복구 · 덤프 재전송 · 모니터링 강화 | 충전 세션 영속화/복구, TransmissionLog 덤프, DSP·Emergency 모니터링, 인프로세스 업데이트 개선 |
| [v1.0.1.38](RELEASE_NOTES.md#v10138---2025-02-05) | 2025-02-05 | Thread Safety | 타이머 변수명 수정, volatile/lock 동기화, Dispose 패턴 개선 |
| [v1.0.1.37](RELEASE_NOTES.md#v10137---2025-01-27) | 2025-01-27 | 안정성: 예외 처리 | 전역 예외 핸들러 3종 등록, 크래시 로그 기록 후 종료 |
| [v1.0.1.36](RELEASE_NOTES.md#v10136---2025-12-24) | 2025-12-24 | 기준 버전 | — |

---

### v1.0.1.41 — 파일 잠금 · 카운트다운 안정성
- 🔒 세션 파일 잠금 오류 수정 — `lock` 동기화 + 임시 파일(`.tmp`) 원자적 교체 + 3회 재시도
- ⏱️ 카운트다운 점프 버그 수정 — `DispatcherTimer` Tick 누적 → `Stopwatch` 기반 실제 경과 시간 계산
- 🛡️ 취소/타임아웃 처리 안정화 — `InitializeCharger()` 이중 호출 방지, `?.Stop()` null 안전 처리
- 📁 수정 파일: `EvChargerUI/Models/ChargingSessionState.cs`, `EvChargerUI/ViewModels/ChargerViewModel.cs`
- 🔗 [상세 보기 →](RELEASE_NOTES.md#v10141---2026-04-14)

### v1.0.1.39 — 세션 복구 · 덤프 재전송 · 모니터링 강화
- 🔋 충전 세션 영속화/복구 — 비정상 종료 시 세션 파일 저장 → 재시작 시 자동 복구·부분환불
- 📤 TransmissionLog 덤프 재전송 — 서버 요청으로 과거 전문(0~4 타입)을 덤프 엔드포인트로 재전송
- 📡 DSP 연결 상태 모니터링 — 실시간 DSP 연결/Fault 감지 → 이벤트 발생·`EVSE_DSP_Status` 자동 갱신
- 🚨 Emergency 알람 이력 강화 — 채널별 FaultCode 저장 → 경보 발생/해제 시 AlarmHistory 전송
- 🔄 ChargerMode 4조건 관리 — DSP·Emergency·EVSE_Status·Network 조건으로 운영모드 판단
- 📊 실시간 충전기 상태 보고 — `SendRTimeChargerStatus()`·`GetChargerInfo()` 구현
- 📱 QR 충전 원격 시작/종료 — `StartChargingAndRemoteDone`·`StopChargingAndRemoteDone` 이벤트
- 🔄 인프로세스 업데이트 개선 — `UpdateFrontFile` 폴더 지원·충전완료 상태 업데이트 허용
- 🗄️ SQLite 데이터베이스 — `SqliteService`·`TransmissionLog` 테이블 도입
- 📁 수정 파일: `EvChargerUI/Models/Charger.cs`, `EvChargerUI/App.xaml.cs`
- 📁 추가 파일: `ChargingSessionState.cs`, `MoneyUtil.cs`, `SqliteService.cs`, `JSonDumpCmd.cs` 외
- 🔗 [상세 보기 →](RELEASE_NOTES.md#v10139---2025-02-06)

### v1.0.1.38 — Thread Safety
- 🔧 `_ReartimeChageeTimer` → `_realtimeChargerTimer` 변수명 오타 수정
- 🔒 `volatile` 키워드 추가 (`_isEmergencyTickBusy`, `_isRealtimeTickBusy`)
- 🔒 `_timerLock` 동기화 객체 도입 — 모든 타이머 Start/Stop에 `lock` 적용
- ♻️ `Dispose()` 패턴 개선 — `?.` 연산자 + lock 동기화
- 📁 수정 파일: `EvChargerUI/Models/Charger.cs`
- 🔗 [상세 보기 →](RELEASE_NOTES.md#v10138---2025-02-05)

### v1.0.1.37 — 안정성: 예외 처리
- 🛡️ 전역 예외 핸들러 3종 등록 (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`)
- 크래시 발생 시 `FileLogger`로 로그 기록 후 `Environment.Exit(1)` 안전 종료
- 📁 수정 파일: `EvChargerUI/App.xaml.cs`
- 🔗 [상세 보기 →](RELEASE_NOTES.md#v10137---2025-01-27)

### v1.0.1.36 — 기준 버전
- 최초 릴리즈
- 🔗 [상세 보기 →](RELEASE_NOTES.md#v10136---2025-12-24)

---
## 📑 버전 상세내역

## v1.0.1.41 - 2026-04-14

### 🐛 버그 수정

#### 1. 충전 세션 파일 잠금 오류 수정
- **파일**: `EvChargerUI/Models/ChargingSessionState.cs`
- **증상**: `session_0.json` 저장 시 _"파일은 다른 프로세스에서 사용 중이므로 액세스할 수 없습니다"_ 오류 발생
- **원인**: `ChargerViewModel.UpdateProgressInfo()` (0.5초 주기)와 `Charger.SendChargingProgress()`가 동시에 동일 파일에 `File.WriteAllText()` 호출 → 파일 잠금 충돌
- **수정**:
  - 모든 파일 접근에 `lock (_fileLock)` 동기화 적용
  - `File.WriteAllText()` → 임시 파일(`.tmp`)에 `FileShare.None` 독점 쓰기 후 `File.Move()` 원자적 교체
  - `IOException` 발생 시 100ms 간격 최대 3회 재시도
  - `SaveSession()` 내 `LoadSession()` 호출 시 데드락 방지를 위해 `LoadSessionInternal()` 분리
  - `DeleteSession()` 시 잔여 `.tmp` 임시 파일도 함께 정리

#### 2. 충전 시작 대기 카운트다운 점프 버그 수정
- **파일**: `EvChargerUI/ViewModels/ChargerViewModel.cs` — `StartChargingAsync()`
- **증상**: 대기 팝업 카운트다운이 간헐적으로 2~3초씩 감소
- **원인**: `DispatcherTimer.Tick`이 UI 스레드 블로킹 시 누적되어 한번에 여러 번 실행됨
- **수정**:
  - `WaitingChargeRemainSeconds--` → `Stopwatch` 기반 `timeoutSeconds - elapsed` 계산
  - `while` 루프 조건: `WaitingChargeRemainSeconds > 0` → `stopwatch.Elapsed.TotalSeconds < timeoutSeconds`
  - Timer 간격: 1초 → 0.5초 (Tick은 UI 표시 전용, 타임아웃 판정은 Stopwatch 기준)

#### 3. 충전 시작 대기 취소/타임아웃 처리 안정성 개선
- **파일**: `EvChargerUI/ViewModels/ChargerViewModel.cs` — `StartChargingAsync()`
- **수정**:
  - `_waitingTimer == null` 불필요 체크(데드코드) 제거
  - 취소 처리를 Tick·while 양쪽 중복 → **while 루프 단일 처리**로 통합 (`InitializeCharger()` 이중 호출 방지)
  - 루프 탈출 후 `_waitingTimer?.Stop()` null 안전 처리 적용
  - `stopwatch.Stop()` 루프 탈출 직후 명시적 호출
  - evsis·일반 분기 양쪽 동일하게 적용

### 📁 변경 파일

| 파일 | 변경 유형 |
|------|-----------|
| `EvChargerUI/Models/ChargingSessionState.cs` | 버그 수정 |
| `EvChargerUI/ViewModels/ChargerViewModel.cs` | 버그 수정 |

---

## v1.0.1.39 - 2025-02-06

> **핵심 테마**: 충전 세션 복구 · TransmissionLog 덤프 재전송 · 모니터링 강화

---

### 1. 🔋 충전 세션 영속화/복구 시스템 (신규)

프로그램 비정상 종료(크래시, 업데이트, 정전 등) 시 충전 중이었던 세션을 자동 복구하여 **충전 종료 전문 전송** 및 **부분환불**을 처리합니다.

#### 세션 저장/삭제 연동
- `SendChargingStart()` — 충전 시작 시 즉시 세션 파일 저장
- `SendChargingProgress()` — 충전 중 주기적 세션 갱신
- `SendChargingEnd()` — 정상 종료 시 세션 파일 삭제

#### 프로그램 종료 시 세션 보존
- `HandleShutdown()` — `App.OnExit()`에서 호출, 활성 충전 세션을 `sessions/` 폴더에 JSON 저장

#### 프로그램 시작 시 세션 복구
- `RestoreChargingSessions()` — `EvCommInitialize()` 내 2초 지연 후 호출
- `ProcessInterruptedCharging()` — 비동기 처리:
  1. DSP에서 현재 적산량 읽기 (리셋 시 세션값 fallback)
  2. 실제 충전량 = `currentEnergy - StartEnergy`
  3. 과금 금액 = `MoneyUtil.TruncateWonUnit((int)(chargePower * 347.2))`
  4. `SendChargingEnd` (charge_end_type=4: 비정상 종료)
  5. 부분환불 (`_paymentService.CancelPay`)
  6. `InterruptedChargingRestored` 이벤트 발생

#### 추가된 파일
| 파일 | 설명 |
|------|------|
| `Models/ChargingSessionState.cs` | 세션 상태 DTO + `ChargingSessionManager` (static, JSON 파일 관리) |
| `Commons/Util/MoneyUtil.cs` | `TruncateWonUnit()` — 원단위(1원 자리) 절삭(10원 단위 내림) |

---

### 2. 📤 TransmissionLog 덤프 재전송 시스템 (신규)

서버 원격 요청(`DumpReq`)으로 SQLite `TransmissionLog` 테이블에 저장된 과거 전문을 **덤프 전용 엔드포인트**로 재전송합니다.

#### 지원 덤프 타입
| 코드 | messageType | 덤프 엔드포인트 | 원본 엔드포인트 |
|------|-------------|----------------|----------------|
| 0 | chargers | `station/dChargers/` | `station/chargers/` |
| 1 | chargingInfo | `station/dChargingInfo/` | `station/charginginfo/` |
| 2 | chargingStart | `station/dChargingStart/` | `station/chargingstart/` |
| 3 | chargingEnd | `station/dChargingEnd/` | `station/chargingend/` |
| 4 | alarmHistory | `station/dAlarmHistory/` | `station/alarmhistory/` |

#### 구현 특징
- **동시 실행 방지**: `Interlocked.CompareExchange`로 `_dumpInProgressFlag` 관리
- **비동기 백그라운드 처리**: `Task.Run(async () => ...)` — UI 스레드 블로킹 없음
- **시간 범위 필터링**: `dumpStartTime` ~ `dumpEndTime` 내 레코드만 전송
- **JSON 정규화**: 이중 중괄호(`{{...}}`), 이스케이프, 문자열 리터럴 자동 처리 (재귀 깊이 3 제한)
- **서버 보호 스로틀**: 레코드 간 `Task.Delay(100)` 적용

#### 추가된 파일
| 파일 | 설명 |
|------|------|
| `Services/Database/SqliteService.cs` | SQLite DB 초기화·쿼리 유틸리티 (`TransmissionLog` 테이블 포함) |
| `Services/EvComm/HttpJsonRequest/JSonDumpCmd.cs` | 덤프 요청 JSON 모델 |

---

### 3. 📡 DSP 연결 상태 모니터링 강화

1초 주기 실시간 타이머에서 **DSP 연결 + Fault 상태**를 복합 감지하고, 상태 변경 시 이벤트를 발생합니다.

#### 추가된 항목
| 항목 | 설명 |
|------|------|
| `_isDspDisconnected` 필드 | DSP 이상 상태 플래그 |
| `IsDspDisconnected` 프로퍼티 | 외부 읽기용 |
| `DspConnectionLost` 이벤트 | DSP 연결 끊김/Fault 발생 시 |
| `DspConnectionRestored` 이벤트 | DSP 연결 복구 + Fault 해제 시 |
| `EVSE_DSP_Status` 설정값 | 0: 정상, 1: 오류 (자동 갱신 + `AppSettingsManager.Save()`) |
| 초기 DSP 상태 체크 | `EvCommInitialize()`에서 초기 상태 설정 |

---

### 4. 🚨 Emergency 알람 이력 강화

비상정지 발생/해제 시 **채널별 FaultCode를 보존**하여 `SendAlarmHistory` 전문에 포함합니다.

| 항목 | 설명 |
|------|------|
| `_channelFaultCodes` | `Dictionary<int, string>` — 채널별 발생 시점 FaultCode 보관 |
| `GetFaultCode(channelNo)` | DSP 연결 정상이면 FaultCode 반환, 아니면 `"9999"` |
| FaultCode 4자리 정규화 | `faultCodeInt % 10000` 적용 |
| `ClearEmergency()` | 발생 시 저장된 FaultCode로 해제 알람 전송 후 제거 |

---

### 5. 🔄 ChargerMode 4조건 관리

`CheckAndUpdateChargerMode()` 메서드로 4가지 조건을 복합 판단합니다.

| 조건 | 확인 방법 | 정상 기준 |
|------|-----------|-----------|
| DSP 연결 | `_dspControlService.IsOpen()` | `true` |
| 비상정지 해제 | `!_isEmergency && !GetEmergencyStatus()` | `true` |
| EVSE_Status | `EVSE_Status != 1` | 점검중 아님 |
| 네트워크 | `EVSE_Network_Status == 0` | 연결 정상 |

> ⚠️ `EVSE_Status`는 관리자 설정값이므로 물리적 문제 해결 시에도 자동 복구하지 않음

---

### 6. 📊 실시간 충전기 상태 보고

- `SendRTimeChargerStatus()` — 28개 항목(충전기 상태, RF/IC 상태, 비상정지, 디스크 여유 공간, 메모리, 단가 등) 서버 전송
- `GetChargerInfo()` — 동일 상태를 `JObject`로 반환 (원격 조회 응답)
- `GetLocalIPAddress()` — 로컬 IPv4 주소 가져오기 헬퍼

---

### 7. 📱 QR 충전 원격 시작/종료

- `StartChargingAndRemoteDone()` → `QrChargingStarted` 이벤트 발생
- `StopChargingAndRemoteDone()` → `QrChargingEnded` 이벤트 발생
- `previousTrno` 결정 로직: QR → `ch.QrTid`, 신용카드 → `PrePaymentInfo.AuthNum`, 기타 → `"-9999"`

---

### 8. 🔄 인프로세스 업데이트 개선

| 변경 전 (v1.0.1.38) | 변경 후 (v1.0.1.39) |
|---------------------|---------------------|
| EXE 존재 + 원본 존재 조건만 처리 | EXE · `UpdateFrontFile` 독립 감지 |
| `File.Move` 사용 | `File.Copy`로 백업 (잠김 방지) |
| `SelectConnector` 상태만 유휴 판정 | `Completed` 상태도 유휴로 허용 |
| 업데이트 없으면 타이머만 재시작 | 빈 update 폴더 자동 정리 (`Directory.Delete`) |
| — | `UpdateFrontFile` 폴더를 대상 경로에 복사 |
| — | 프론트 전용 업데이트 시 앱 종료 불필요 |
| — | `LastUiUpdateDate` 기록 |

---

### 9. 🗄️ SQLite 데이터베이스 도입

`SqliteService` 클래스를 통해 로컬 SQLite DB를 초기화하고 전문 이력을 관리합니다.

#### 생성 테이블
| 테이블 | 용도 |
|--------|------|
| `ChargingSessions` | 충전 세션 이력 (StationId, ChargerId, EnergyWh, Cost 등) |
| `TransmissionLog` | 서버 전송 전문 이력 (message_type, request_json, status, retry_count) |
| `KeyValues` | 키-값 설정 저장소 |

---

### 10. 🛠️ App.xaml.cs 강화

| 항목 | 설명 |
|------|------|
| `LogManager` | 로그 관리자 시작/종료 (`Services/LogManager.cs`) |
| `DspLogger` | DSP 전용 로그 분리 (`DspLogSaveYn` 설정 On/Off) |
| `ApplySystemSettingsOnStartup()` | 저장된 밝기/볼륨 설정 시작 시 적용 |
| `InitializeDebugWindow()` | `DebugWindow` + `DebugTraceListener` 등록 |
| 중복 프로세스 종료 | 시작 시 동일 프로세스 `Kill()` + 3초 대기 |
| `HandleShutdown()` 호출 | `OnExit()`에서 세션 저장 후 `Dispose()` |
| SQLite 초기화 | `OnStartup()`에서 `SqliteService.Initialize()` 호출 |

---

### 📊 영향받는 파일

#### 수정된 파일
- `EvChargerUI/Models/Charger.cs` — 세션 복구, 덤프, DSP 모니터링, Emergency 알람, 상태 보고, 업데이트 개선
- `EvChargerUI/App.xaml.cs` — LogManager, DspLogger, DebugWindow, HandleShutdown, SQLite 초기화

#### 추가된 파일
- `EvChargerUI/Models/ChargingSessionState.cs` — 세션 상태 DTO + `ChargingSessionManager`
- `EvChargerUI/Commons/Util/MoneyUtil.cs` — 원단위 절삭 유틸
- `EvChargerUI/Services/Database/SqliteService.cs` — SQLite 초기화·쿼리 유틸
- `EvChargerUI/Services/EvComm/HttpJsonRequest/JSonDumpCmd.cs` — 덤프 요청 JSON 모델
- `EvChargerUI/Services/LogManager.cs` — 로그 관리자

---

### 🎯 기대 효과

#### 안정성
- ✅ 비정상 종료 시 충전 데이터 유실 방지
- ✅ 자동 부분환불로 고객 결제 보호
- ✅ DSP 연결/Fault 실시간 감지 → 점검중 자동 전환

#### 운영성
- ✅ 과거 전문 덤프 재전송으로 데이터 정합성 확보
- ✅ 28개 항목 실시간 충전기 상태 원격 모니터링
- ✅ QR 원격 충전 시작/종료 지원

#### 유지보수성
- ✅ 프론트 파일 업데이트 시 앱 재시작 불필요
- ✅ DSP 전용 로그 분리로 장애 분석 효율 향상
- ✅ SQLite 기반 전문 이력 관리로 데이터 추적 가능

---

### 🧪 권장 테스트 시나리오

1. **세션 복구 테스트**
   - 충전 중 프로그램 강제 종료 → 재시작 후 세션 복구 확인
   - 부분환불 금액 정확성 확인
   - DSP 리셋 후 세션값 fallback 확인

2. **덤프 재전송 테스트**
   - 각 덤프 타입(0~4) 정상 전송 확인
   - 시간 범위 필터링 정확성
   - 동시 덤프 요청 시 중복 실행 방지 확인

3. **DSP 모니터링 테스트**
   - DSP 케이블 분리 → `DspConnectionLost` 이벤트 발생 확인
   - DSP 재연결 → `DspConnectionRestored` 이벤트 발생 확인
   - Fault 상태에서 Emergency 알람 FaultCode 정확성

4. **인프로세스 업데이트 테스트**
   - `UpdateFrontFile` 폴더만 존재 시 프론트 업데이트 (재시작 없음)
   - EXE + 프론트 동시 업데이트 → 앱 종료 + 재시작
   - 충전 완료(`Completed`) 상태에서 업데이트 허용 확인

5. **QR 충전 테스트**
   - QR 원격 시작/종료 이벤트 정상 발생 확인
   - `previousTrno` 값 정확성 (QR tid / AuthNum / -9999)

---

### 📝 주의사항

#### 개발자 주의사항
1. `ProcessInterruptedCharging()`은 `async void` — 예외가 전역 핸들러로 전파됨
2. `DumpReq()`는 즉시 `true` 반환 후 백그라운드 처리 — 호출자는 완료를 기다리지 않음
3. `CheckAndUpdateChargerMode()` 내 `GetEmergencyStatus()`는 동기 호출 — UI 타이머에서 호출 시 주의
4. `_channelFaultCodes`는 동기화 없이 사용 — 현재 UI 스레드에서만 접근

#### 배포 전 체크리스트
- [ ] 빌드 성공 확인
- [ ] `sessions/` 폴더 쓰기 권한 확인
- [ ] SQLite `TransmissionLog` 테이블 스키마 확인
- [ ] 덤프 엔드포인트 (`station/dChargers/` 등) 서버 측 구현 확인
- [ ] `MoneyUtil.TruncateWonUnit` 원단위 절삭 로직 검증
- [ ] 부분환불 결제 단말기 연동 테스트
- [ ] 타이머·세션 복구·업데이트 종합 테스트

---

## v1.0.1.38 - 2025-02-05

### 🔧 수정 사항 (Bug Fixes)

#### Thread Safety 개선
- **변수명 오타 수정**: `_ReartimeChageeTimer` → `_realtimeChargerTimer`로 변경
  - 더 명확하고 일관된 네이밍으로 개선
  - 타이머 관련 변수의 가독성 향상

#### 멀티스레드 안전성 강화
- **volatile 키워드 추가**
  ```csharp
  private volatile bool _isEmergencyTickBusy = false;
  private volatile bool _isRealtimeTickBusy = false;
  ```
  - 멀티스레드 환경에서 변수 가시성 보장
  - CPU 캐시로 인한 오래된 값 읽기 방지

- **타이머 동기화 메커니즘 추가**
  ```csharp
  private readonly object _timerLock = new object();
  ```
  - 모든 타이머 조작에 lock 적용
  - 경쟁 상태(Race Condition) 방지
  - 타이머 중복 실행 방지

#### 타이머 관리 개선
다음 타이머 조작에 lock 적용:
- ✅ `_emergencyTimer` 시작
- ✅ `_realtimeChargerTimer` 시작
- ✅ `_updateCheckTimer` 시작/중지
- ✅ `Dispose()` 메서드 내 모든 타이머 중지

#### Dispose 패턴 개선
```csharp
public void Dispose()
{
    lock (_timerLock)
    {
        _emergencyTimer?.Stop();
        _statusTimer?.Stop();
        _realtimeChargerTimer?.Stop();
        _updateCheckTimer?.Stop();
    }

    _evCommService?.Close();
    _paymentService?.Close();
    _dspControlService?.Close();
}
```
- Null 조건 연산자(`?.`) 사용으로 안전성 강화
- 타이머 중지 시 동기화 보장
- 리소스 해제 순서 최적화

### 📊 영향받는 파일

#### 수정된 파일
- `EvChargerUI/Models/Charger.cs`
  - 타이머 변수명 수정
  - Thread safety 메커니즘 추가
  - Dispose 메서드 개선

### 🎯 기대 효과

#### 안정성 향상
- ✅ 경쟁 상태(Race Condition) 방지
- ✅ 타이머 중복 실행 방지
- ✅ 멀티스레드 환경에서 안전한 동작 보장

#### 유지보수성 향상
- ✅ 명확한 변수명으로 코드 가독성 개선
- ✅ 일관된 동기화 패턴 적용
- ✅ 안전한 리소스 해제 보장

#### 성능 영향
- ⚡ Lock 사용으로 약간의 오버헤드 발생 (무시할 수 있는 수준)
- ⚡ 타이머 시작/중지 시에만 동기화 수행 (빈번하지 않음)
- ⚡ 전체적인 성능 저하 없음

### 🧪 테스트 결과

#### 빌드 테스트
- ✅ 빌드 성공
- ✅ 컴파일 에러 없음
- ✅ 경고 메시지 없음

#### 권장 테스트 시나리오
다음 시나리오에서 충분한 테스트를 권장합니다:

1. **타이머 동시 실행 테스트**
   - 여러 타이머가 동시에 실행될 때 안정성 확인
   - Emergency 타이머와 Realtime 타이머 동시 실행

2. **프로그램 종료 테스트**
   - 충전 중 프로그램 종료
   - 여러 타이머 실행 중 종료
   - Dispose 호출 시 정상 종료 확인

3. **장시간 운영 테스트**
   - 24시간 이상 연속 운영
   - 메모리 누수 없음 확인
   - 타이머 안정성 확인

4. **업데이트 시나리오 테스트**
   - 충전기 유휴 상태에서 업데이트
   - 업데이트 타이머 동작 확인

### 🔍 기술적 세부사항

#### Volatile 키워드의 역할
```csharp
private volatile bool _isEmergencyTickBusy = false;
```
- 컴파일러 최적화 방지
- CPU 캐시 일관성 보장
- 스레드 간 변수 가시성 보장

#### Lock 패턴
```csharp
lock (_timerLock)
{
    _updateCheckTimer.Stop();
}
```
- 상호 배제(Mutual Exclusion) 보장
- 데드락 방지를 위한 최소 범위 lock
- 타이머 조작에만 lock 적용

### 📝 주의사항

#### 개발자 주의사항
1. 타이머 관련 코드 수정 시 반드시 `_timerLock` 사용
2. `volatile` 변수의 원자성은 보장되지 않음 (단순 읽기/쓰기만 안전)
3. 복잡한 상태 변경은 lock 사용 권장

#### 배포 전 체크리스트
- [ ] 빌드 성공 확인
- [ ] 타이머 동작 테스트
- [ ] 프로그램 종료 테스트
- [ ] 메모리 누수 확인
- [ ] 로그 확인 (타이머 관련 에러 없음)

### 🔗 관련 이슈

#### 해결된 문제
- Thread Safety 문제 (#5)
  - 타이머 변수명 오타
  - volatile 키워드 누락
  - 타이머 동기화 부재
  - Dispose 메서드 개선 필요

#### 참고 문서
- [C# volatile 키워드 설명](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile)
- [lock 문 설명](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock)
- [DispatcherTimer 스레드 안전성](https://docs.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer)

### 📌 다음 버전 계획

#### 추가 개선 사항 검토 중
1. .NET Framework 4.8로 업그레이드 (보안 강화)
2. Health Check 로직 활성화 검토
3. Debug/Release 설정 통일
4. 예외 처리 강화 (결제 서비스)

---

## v1.0.1.37 - 2025-01-27

> **핵심 테마**: 안정성 — 예외 처리

### 🔧 수정 사항

#### 전역 예외 핸들러 3종 등록
`OnStartup()`에서 아래 3종의 핸들러를 등록하여 모든 예외 경로를 포착:
```csharp
// 1. WPF UI 스레드 예외
this.DispatcherUnhandledException += App_DispatcherUnhandledException;

// 2. 백그라운드 스레드 / AppDomain 수준 예외
AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

// 3. Task 미관찰 예외 (async/await)
TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
```

| 핸들러 | 대상 | 포착 범위 |
|--------|------|-----------|
| `App_DispatcherUnhandledException` | WPF UI 스레드 | Dispatcher 루프 내 예외 |
| `UnhandledExceptionHandler` | AppDomain 전역 | 관리/비관리 스레드 예외 |
| `TaskScheduler_UnobservedTaskException` | Task 비동기 | `await` 없이 실행된 Task 예외 |

#### 크래시 로그 기록 후 안전 종료
3종 핸들러 모두 공통 메서드 `LogAndExit()`로 통합 처리:
```csharp
private void LogAndExit(Exception ex)
{
    string msg = (ex != null) ? ex.ToString() : "알 수 없는 오류";
    AppLogger.Error("Program Error", ex);   // FileLogger로 스택 트레이스 기록
    System.Environment.Exit(1);              // 안전 종료
}
```
- 예외 발생 → `FileLogger`에 전체 스택 트레이스 기록 → `Environment.Exit(1)` 호출
- 로그 파일 위치: `logs/APP_YYYYMMDD.log`

### 📊 영향받는 파일

#### 수정된 파일
- `EvChargerUI/App.xaml.cs`
  - `OnStartup()` — 전역 예외 핸들러 3종 등록
  - `App_DispatcherUnhandledException()` — UI 스레드 예외 처리
  - `UnhandledExceptionHandler()` — AppDomain 예외 처리
  - `TaskScheduler_UnobservedTaskException()` — Task 미관찰 예외 처리
  - `LogAndExit()` — 공통 로그 기록 + 종료 메서드

### 🎯 기대 효과
- ✅ 어떤 스레드에서든 예외 발생 시 크래시 원인 로그 확보
- ✅ 무응답 상태 대신 안전 종료로 시스템 자원 즉시 해제
- ✅ 운영 환경에서 장애 분석용 로그 데이터 확보

---

## v1.0.1.36 - 2025-12-24

> **기준 버전** — 최초 릴리즈

---
