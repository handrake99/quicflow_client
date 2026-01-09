# QuicFlow Client

QuicFlow Client는 C#과 **Avalonia UI**를 사용하여 개발된 데스크탑 QUIC 클라이언트 애플리케이션입니다. 사용자는 QUIC 프로토콜을 사용하여 서버에 연결하고, 실시간으로 메시지를 주고받으며, 서버 로그와 연결 상태를 모니터링할 수 있습니다.

## 주요 기능 (Features)

*   **QUIC 연결 관리**: 서버 IP와 포트를 지정하여 QUIC 연결을 수립하고 해제할 수 있습니다.
*   **실시간 채팅**: 연결된 서버와 텍스트 메시지를 주고받을 수 있는 채팅 인터페이스를 제공합니다.
*   **로그 모니터링**: 서버로부터 수신된 로그 및 연결 이벤트를 실시간으로 확인할 수 있습니다.
*   **크로스 플랫폼**: .NET 8 및 Avalonia UI 기반으로 macOS, Windows, Linux 환경을 지원합니다.

## 사전 요구 사항 (Prerequisites)

이 프로젝트를 실행하기 위해서는 다음 항목들이 설치되어 있어야 합니다.

*   **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
*   **libmsquic**: QUIC 프로토콜을 위한 네이티브 라이브러리 (macOS의 경우 필수)

### macOS 설정

macOS에서는 Homebrew를 통해 `libmsquic`을 설치해야 합니다.

```bash
brew install libmsquic
```

## 설치 및 실행 (Installation & Usage)

1.  **레포지토리 클론**

    ```bash
    git clone <repository-url>
    cd quicflow_client
    ```

2.  **애플리케이션 실행**

    macOS 환경에서는 라이브러리 경로 설정을 위해 포함된 `run.sh` 스크립트를 사용하는 것을 권장합니다.

    ```bash
    chmod +x run.sh
    ./run.sh
    ```

    또는 직접 `dotnet` 명령어로 실행할 수 있습니다 (라이브러리 경로 설정 필요).

    ```bash
    dotnet run
    ```

## 프로젝트 구조 (Project Structure)

이 프로젝트는 **MVVM (Model-View-ViewModel)** 패턴을 따르고 있습니다.

*   **Views/**: 사용자 인터페이스 (`MainWindow.axaml`)
*   **ViewModels/**: UI 로직 및 데이터 바인딩 (`MainWindowViewModel.cs`)
*   **Services/**: QUIC 통신 로직 (`QuicClientService.cs`)
*   **Models/**: 데이터 모델 (`ChatData.cs` 등)

## 기술 스택 (Tech Stack)

*   **Language**: C#
*   **Framework**: .NET 8.0
*   **UI Library**: Avalonia UI 11.0.6
*   **Network**: QUIC (MsQuic)
