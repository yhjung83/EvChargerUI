# EvChargerUI

환경부 통합 UI
---

## 📑 릴리즈 목차

> 각 버전의 상세 내용은 [RELEASE_NOTES.md](RELEASE_NOTES.md)를 참조하세요.

| 버전 | 날짜 | 핵심 테마 | 주요 변경 |
|------|------|-----------|----------|
| [v1.0.1.38](RELEASE_NOTES.md#v10138---2025-02-05) | 2025-02-05 | Thread Safety | 타이머 변수명 수정, volatile/lock 동기화, Dispose 패턴 개선 |
| [v1.0.1.37](RELEASE_NOTES.md#v10137---2026-01-27) | 2026-01-27 | 안정성: 예외 처리 | 전역 예외 핸들러 3종 등록, 크래시 로그 기록 후 종료 |
| [v1.0.1.36](RELEASE_NOTES.md#v10136---2025-12-24) | 2025-12-24 | 기준 버전 | — |

---

## 프로젝트 구조

```
EvChargerUI/
├── App.config
├── App.xaml
├── App.xaml.cs
├── EvChargerUI.csproj
├── packages.config
├── Commons/                # 공통 유틸리티, 설정, Enum 등
├── Domains/                # 도메인 모델
├── Fonts/                  # 폰트 리소스
├── Images/                 # 이미지 리소스
├── Models/                 # 핵심 모델 (예: Charger)
├── Properties/             # WPF/프로젝트 속성
├── Services/               # DSP 제어, 결제, 통신 등 서비스 계층
│   ├── DspControl/         # 제조사별 DSP 제어 서비스
│   └── EvComm/             # 충전기-서버 통신
├── ViewModels/             # MVVM ViewModel 계층
├── Views/                  # WPF XAML 뷰
│   ├── DualChannel/        # 듀얼채널 UI
│   ├── SingleChannel/      # 싱글채널 UI
│   └── Popup/              # 팝업 UI
└── ...
```

## 주요 기능

- **충전기 상태 관리**: 4개 제조사 DSP 제어 지원 (Signet, Evsis, Chaevi, Klinelex) ([Services/DspControl](EvChargerUI/Services/DspControl))
- **결제 시스템 연동**: NICE, Techleader 결제 모듈 연동 ([Services/NicePaymentService.cs](EvChargerUI/Services/NicePaymentService.cs), [Services/TechleaderPaymentService.cs](EvChargerUI/Services/TechleaderPaymentService.cs))
- **서버 통신**: HTTP/JSON 기반 충전기-서버 연동, 원격 제어 명령 지원 (RESET, PRICES, DISPLAYBRIGHTNESS, SOUND, UPDATE, STATUS, CHECKSTATUS, LIMIT, TEST, PAYYN, AUTH, STOP) ([Services/EvComm](EvChargerUI/Services/EvComm))
- **MVVM 패턴**: View, ViewModel, Model 분리 구조
- **다채널 지원**: 싱글/듀얼 채널 UI 및 로직 ([ViewModels/LeftChargerViewModel.cs](EvChargerUI/ViewModels/LeftChargerViewModel.cs), [ViewModels/RightChargerViewModel.cs](EvChargerUI/ViewModels/RightChargerViewModel.cs), [ViewModels/SingleChargerViewModel.cs](EvChargerUI/ViewModels/SingleChargerViewModel.cs))
- **로깅**: 파일 기반 로깅 (APP_*.log, DSP_*.log) ([Commons/Util/FileLogger.cs](EvChargerUI/Commons/Util/FileLogger.cs))
- **설정 관리**: INI 파일 기반 설정 관리, Configuration별 설정 파일 지원 (settings.Debug.ini, settings.Release.ini)

## 빌드 및 실행

1. **필수 환경**: .NET Framework 4.6.2 이상, Visual Studio 2019 이상
2. **패키지 복원**: NuGet 패키지 자동 복원 (예: Newtonsoft.Json, QRCoder 등)
3. **빌드**: `EvChargerUI.sln` 솔루션을 Visual Studio에서 열고 빌드

## 주요 의존성

- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
- [QRCoder](https://www.nuget.org/packages/QRCoder/)
- [WpfAnimatedGif](https://www.nuget.org/packages/WpfAnimatedGif/)
- [ini-parser](https://www.nuget.org/packages/ini-parser/)
- [Microsoft.Xaml.Behaviors.Wpf](https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Wpf/)
 - [System.Data.SQLite.Core](https://www.nuget.org/packages/System.Data.SQLite.Core/)

## 실행 절차

### NuGet 패키지 복원
> [!TIP]
> Visual Studio -> 도구 > NuGet 패키지 관리자 > 패키지 관리자 콘솔

```bash
Update-Package -reinstall
```

이후 Visual Studio에서 작업 및 빌드, 테스트를 진행하면 됩니다.


## 기타

- **로그**: 실행 로그는 `bin/Debug/logs/` 폴더에 저장됩니다.
- **설정**: 환경설정은 `App.config` 및 `Commons/Settings/` 하위 파일에서 관리합니다.
- **로컬 DB**: SQLite 파일은 `bin/<CONFIG>/data/evcharger.db` 경로에 생성되며, 앱 시작 시 자동 초기화됩니다.

---
