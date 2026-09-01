# JKMon

Windows 작업 표시줄 위에 장치 사용량과 동기화 상태를 함께 보여주는 가벼운 데스크톱 모니터.

CPU, 메모리, 네트워크, 디스크 처리량을 한 줄로 보여주고 그 옆에 OneDrive, Syncthing, Global Secure Access의
상태 아이콘을 띄운다. 설치가 필요 없는 포터블 앱이다.

## Requirements

Windows 11 x64. .NET 설치는 필요 없다. 관리자 권한도 필요 없다.

## Install

1. [Releases](https://github.com/krjungki/JKMon/releases)에서 zip을 받는다.
2. **파일 속성에서 "차단 해제"를 체크한다.** 인터넷에서 받은 서명 없는 실행 파일이라 이 단계를 건너뛰면
   SmartScreen이 실행을 막는다. PowerShell이면 `Unblock-File .\JKMon-*.zip`.
3. 원하는 폴더에 풀고 `JKMon.exe`를 실행한다.

설정과 로그는 실행 파일과 같은 폴더에 저장된다. 폴더를 통째로 옮기면 설정이 따라간다. 지우려면 폴더를 지우고
설정 창에서 시작 프로그램 등록을 해제하면 된다.

## What it shows

| 항목 | 표시 |
|---|---|
| CPU | 숫자, 세로 바, 또는 논리 프로세서별 바 |
| 메모리 | 숫자, 세로 바, 또는 파이 |
| 네트워크 / 디스크 | 초당 처리량, 방향별 색 |
| OneDrive | 전송 활동으로 동기화 중을 추정한다. OneDrive가 상태를 외부에 공개하지 않기 때문이다 |
| Syncthing | loopback REST로 폴더 상태를 읽는다 |
| Global Secure Access | 클라이언트가 남기는 연결 상태 이벤트를 읽는다 |

아이콘 아래 막대는 완료면 녹색, 동기화 중이거나 오류면 붉은색, 판별 불가면 회색이다. 마우스를 올리면 이유가
tooltip에 나온다. 실행 중이 아닌 공급자는 아예 표시하지 않는다.

트레이 아이콘에서 표시/숨김, 설정, 창 계층 전환, 갱신 주기 변경, 종료를 할 수 있다.

## Settings

글꼴, 색 10종, 배경 불투명도, 게이지 모양과 윤곽선 두께, 상단 캡션, 표시 모니터와 위치, 아이콘 순서,
갱신 주기(1-10초), 시작 프로그램 등록을 설정 창에서 바꿀 수 있고 즉시 반영된다.

## Privacy

- 파일 이름이나 내용을 수집하지 않는다. telemetry를 보내지 않는다.
- 장치 성능 수치와 공급자의 집계 상태만 로컬에서 처리한다.
- Syncthing API key는 Syncthing 설정 파일에서 런타임에 읽어 메모리에서만 쓴다. 복사본을 만들지 않는다.
- Global Secure Access는 상태 이벤트만 읽는다. 클라이언트 레지스트리에 있는 계정 UPN과 테넌트 식별자는 읽지 않는다.
- 네트워크 호출은 loopback으로 제한한다.

## Updates

트레이 메뉴의 **Check for updates...** 로 직접 확인하거나, 설정에서 매일/매주 자동 확인을 켜면 된다. **기본값은 꺼져
있다.** 켜면 앱이 이 저장소의 릴리스 페이지에서 버전 파일 하나를 읽는다. 식별자를 보내지 않는다.

새 버전이 있으면 물어보고, 동의하면 이렇게 진행한다.

1. 릴리스 zip을 임시 폴더로 내려받고 함께 공개된 SHA-256과 대조한다. **불일치면 중단하고 설치본을 건드리지 않는다.**
2. 앱을 종료한다. 응답하지 않으면 강제 종료한다.
3. 릴리스가 실어 보내는 파일만 교체한다. `settings.json`과 `diagnostics.log`는 그대로 남는다.
4. 재실행하고 임시 파일을 지운다. 교체가 실패하면 이전 파일로 되돌린다.

앞 버전은 교체가 끝날 때까지 앱 폴더의 `.jkmon-previous`에 보관된다.

## Updates

트레이 메뉴의 **Check for updates...** 로 직접 확인하거나, 설정에서 매일/매주 자동 확인을 켜면 된다. **기본값은 꺼져
있다.** 켜면 앱이 이 저장소의 릴리스에서 버전 파일 하나를 읽는다. 식별자를 보내지 않는다.

새 버전이 있으면 물어보고, 동의하면 이렇게 진행한다.

1. 릴리스 zip을 임시 폴더로 내려받고 함께 공개된 SHA-256과 대조한다. **불일치하면 중단하고 설치본을 건드리지 않는다.**
2. 앱을 종료한다. 응답하지 않으면 강제 종료한다.
3. 릴리스가 실어 보내는 파일만 교체한다. `settings.json`과 `diagnostics.log`는 그대로 남는다.
4. 재실행하고 임시 파일을 지운다. 교체가 실패하면 이전 파일로 되돌린다.

앞 버전은 교체가 끝날 때까지 앱 폴더의 `.jkmon-previous`에 보관된다.

## Platform Support

| OS | Status | Architecture | Runtime | Artifact |
|---|---|---|---|---|
| Windows 11 | supported | x64 | .NET 10 self-contained | `JKMon.exe` |
| macOS | not-targeted | — | — | — |
| Linux | not-targeted | — | — | — |

## Known limitations

| 항목 | 내용 |
|---|---|
| 서명 | 의도적으로 서명하지 않았다. 첫 실행에 SmartScreen 경고가 뜬다 |
| OneDrive | 상태가 추정이다. 아주 작은 동기화는 놓칠 수 있고 동기화 외 전송이 붉은색을 유발할 수 있다 |
| 메모리 | 안정 상태 private working set 약 105 MB |
| 트레이 아이콘 | Windows 11 기본값대로 숨김 영역에 생성된다. 한 번 고정하면 유지된다 |
| 로케일 | 한국어 표시 Windows에서 성능 카운터 수집은 아직 검증하지 못했다 |
| Syncthing HTTPS | Syncthing GUI에 HTTPS를 켜면 자체 서명 인증서 때문에 상태를 읽지 못하고 회색으로 남는다 |

## Build

.NET 10 SDK가 필요하다.

```text
dotnet restore JKMon.slnx
dotnet build JKMon.slnx -c Release
dotnet test JKMon.slnx -c Release --no-build
dotnet publish src/JKMon.App/JKMon.App.csproj -c Release -o dist/win-x64
```

런타임 식별자와 self-contained, single-file 설정은 `src/JKMon.App/JKMon.App.csproj`에 있다. 배포본은
`JKMon.exe` 하나와 WPF가 프로세스 시작 시 직접 적재하는 네이티브 DLL 5개, 그리고 라이선스 파일로 구성된다.

## License

[MIT](LICENSE). 번들된 .NET 런타임의 고지는 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)에 있다.
