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
| 네트워크 / 디스크 | 초당 처리량. 아래에 이름과 색 막대가 붙고, In/Out 합산량에 따라 색이 4단계로 바뀐다 |
| OneDrive | 전송 활동으로 동기화 중을 추정한다. OneDrive가 상태를 외부에 공개하지 않기 때문이다 |
| Syncthing | loopback REST로 폴더 상태를 읽는다 |
| Global Secure Access | 클라이언트가 남기는 연결 상태 이벤트를 읽는다 |

아이콘 아래 막대는 완료면 녹색, 동기화 중이거나 오류면 붉은색, 판별 불가면 회색이다. 실행 중이 아닌 공급자는 아예
표시하지 않는다. 오버레이는 클릭이 통과하도록 만들어져 마우스 이벤트를 받지 않으므로, 상태의 자세한 이유는
tooltip이 아니라 실행 파일 옆 `diagnostics.log`에 남는다.

트레이 아이콘에서 표시/숨김, 설정, 창 계층 전환, 갱신 주기 변경, 종료를 할 수 있다.

## Settings

설정 창에서 바꾼 값은 즉시 반영되고 실행 파일 옆 `settings.json`에 저장된다. **Reset**은 모든 값을 기본값으로
되돌린다. 아래는 설정 창의 구획 순서를 그대로 따른다.

### THEME

밝은 테마와 어두운 테마 두 가지를 담고 있다. 테마는 **색과 글꼴만** 소유한다. 캡션 내용, 오버레이 위치와 모니터,
글자 크기, 갱신 주기, 임계값 같은 선택은 테마를 바꿔도 그대로 남는다.

두 테마는 색뿐 아니라 형태도 다르다. 밝은 테마는 둥근 모서리와 알약 모양 버튼을 쓰고, 어두운 테마는 각진 모서리와
대문자 라벨을 쓴다. 오버레이 패널은 **두 테마 모두 어둡다.** 바탕화면 위에 떠 있어 어두운 카드에 밝은 글자가
가장 잘 읽히기 때문이다. 테마 이름은 설정 창의 밝기를 가리킨다.

| 항목 | 의미 |
|---|---|
| Palette | Light / Dark. 바꾸면 확인을 거쳐 **앱이 다시 시작된다.** 두 창 모두 만들어질 때의 글꼴로 크기를 계산하기 때문이다 |
| Accent stripe | 오버레이 패널과 설정 창 위쪽에 그리는 색 띠. None / Solid / Tricolour. 기본값은 None |
| Stripe colour 1-3 | 띠의 색. Solid는 1번만, Tricolour는 왼쪽부터 순서대로 3개를 쓴다 |
| Saved themes | 저장해 둔 테마 목록. **Load**로 불러오고 **Delete**로 지운다 |

**Theme save**는 지금 보이는 모습을 이름을 붙여 `themes.json`에 저장한다. 색, 글꼴, 글자·인디케이터 크기,
불투명도, 외곽선 두께, 그림자, 액센트 띠, 팔레트가 담긴다. 위치·모니터·갱신 주기·임계값·게이지 종류처럼
"무엇을 어디에 보여줄지"에 해당하는 값은 담기지 않으므로, 테마를 불러와도 그 설정은 유지된다.

불러온 테마가 지금과 다른 팔레트를 담고 있으면 저장 후 다시 시작한다. 같은 팔레트면 즉시 반영된다.

### CUSTOM TEXT

오버레이 맨 위에 표시하는 사용자 문자열이다. 이 캡션은 **반투명 배경 밖**에 놓여 바탕화면 위에 바로 뜬다.

| 항목 | 의미 |
|---|---|
| Caption | 표시할 문자열. **비우면 캡션 줄 자체가 사라진다.** 최대 64자 |
| Caption font | 캡션 전용 글꼴. 본문 글꼴과 따로 고른다 |
| Caption size | 9-72 |
| Caption colour | 캡션 글자색 |
| Caption align | 왼쪽 / 가운데 / 오른쪽. 왼쪽·오른쪽은 패널 내용의 가장자리에 맞춘다 |
| Caption shadow | 캡션이 배경 밖에 있어 밝은 바탕화면에서는 대비가 사라지므로 따로 둔 그림자. 본문의 Text shadow와 독립이며 흐림 정도가 캡션 크기에 비례한다 |

### APPEARANCE

| 항목 | 의미 |
|---|---|
| Text colour | 본문 글자색. `CPU` `Memory` `Net` `Disk` **네 이름도 이 색을 따른다** |
| Background | 반투명 패널의 배경색 |

### GAUGES

| 항목 | 의미 |
|---|---|
| CPU | 숫자 또는 세로 막대 |
| Show individual cores instead | 논리 프로세서마다 가는 막대 하나씩. 켜면 위의 CPU 게이지 대신 표시되므로 그 선택은 비활성화된다 |
| Memory | 숫자, 세로 막대, 또는 파이 |
| Outline | 막대와 파이 테두리 색. 어떤 바탕화면 위에서도 형태가 보이게 한다 |
| Outline width | 0-6 px. **0이면 테두리를 그리지 않는다** |
| Label size | 막대와 파이 **위에 얹는 백분율 숫자** 크기. 6-32. 코어 막대에는 붙지 않는다 |
| Caption size | 숫자 게이지 위의 `CPU` / `Memory` 이름 크기. 0-32이며 **0이면 숨긴다.** 막대와 파이는 모양으로 구분되므로 이름을 붙이지 않는다 |
| CPU usage | CPU 게이지 색 |
| Memory usage | 메모리 게이지 색 |

### INDICATORS

| 항목 | 의미 |
|---|---|
| Icon order | 동기화 아이콘의 좌우 순서. **이 PC에서 실행 중인 클라이언트만 나열한다.** 없는 공급자는 목록에 나타나지 않지만 순서는 저장돼 있어 나중에 설치하면 원래 자리로 돌아온다 |

### ACTIVITY BARS

Net과 Disk 열 아래의 `▬▬▬ Net`, `▬▬▬ Disk` 표시다. 막대와 **속도 숫자의 색**이 여기서 정해진다.

색은 **In과 Out을 합친 값**(디스크는 Read+Write)으로 4단계에서 고른다.

| 항목 | 의미 |
|---|---|
| Hide and show how busy | 끄면 이름과 막대가 사라지고 세로 공간을 돌려받는다 |
| Idle | 합산 전송량이 **정확히 0**일 때. 조용한 상태를 한눈에 구분하려고 별도 단계로 뒀다 |
| Normal | 0 초과, 첫 임계값 미만. **1 B/s만 흘러도 Idle을 벗어난다** |
| Elevated | 첫 임계값 이상 |
| High | 둘째 임계값 이상 |
| Net KiB/s | 네트워크가 Elevated / High로 바뀌는 지점 |
| Disk KiB/s | 스토리지가 Elevated / High로 바뀌는 지점 |

임계값을 네트워크와 스토리지에 따로 두는 이유는, 링크를 포화시키는 속도가 SSD에는 아무것도 아니기 때문이다.
기본값은 네트워크 1024 / 10240 KiB/s, 스토리지 5120 / 51200 KiB/s다.

### TYPOGRAPHY

| 항목 | 의미 |
|---|---|
| Background opacity | 0-100%. **0이면 배경이 완전히 사라져** 글자만 뜬다 |
| Font | 본문 글꼴 |
| Font size | 9-32. 게이지 높이와 열 간격이 이 값을 따라간다 |
| Indicator size | 동기화 아이콘 지름 |
| Bold | 본문 글자 굵기 |
| Text shadow | 본문 글자 그림자. 캡션 그림자와 독립이다 |

### BEHAVIOUR

| 항목 | 의미 |
|---|---|
| Refresh | 1-10초. 지표를 다시 읽는 주기 |
| Edge margin | 화면 가장자리와의 간격(px) |
| Monitor | 표시할 모니터. **Automatic**은 창이 현재 있는 모니터를 따라간다 |
| Position | 하단 왼쪽 / 가운데 / 오른쪽 |
| Window layer | **Desktop**은 바탕화면에 붙어 다른 창에 가려진다. **Always on top**은 항상 위에 뜬다 |
| Start with Windows | 로그인 시 자동 실행 등록. 폴더를 옮기면 다음 실행 때 경로가 갱신된다 |
| Fade out while the mouse is over it | 포인터가 오버레이 위에 있는 동안 투명해지고 벗어나면 돌아온다. Always on top과 함께 쓸 때 유용하다. 기본 꺼짐 |
| Hide and pause during full screen apps | 전체화면 영상이나 게임 동안 오버레이를 숨기고 **측정도 멈춘다.** 기본 켬 |

**전체화면 감지**는 두 가지를 함께 본다. Windows가 알려주는 배타적 전체화면·프레젠테이션 모드와, 전경 창이 모니터를
가득 덮는지 여부다. 후자는 요즘 플레이어와 게임이 쓰는 **테두리 없는 전체화면**을 잡기 위한 것이다.

멀티 모니터에서는 **오버레이가 있는 모니터가 전체화면일 때만** 숨긴다. 다른 화면의 게임은 무시한다. 바탕화면이나
작업 표시줄은 본래 화면을 다 덮으므로 전체화면으로 치지 않는다.

### Updates

| 항목 | 의미 |
|---|---|
| Update check | 안 함 / 매일 / 매주 |
| Also check when the app starts | 켜면 **주기와 무관하게 앱을 켤 때마다 한 번** 확인한다. 끄면 실행만으로는 확인하지 않고 앱이 한 주기 동안 켜져 있어야 확인한다 |

`Update check`가 **안 함**이면 시작 시 확인을 켜도 아무 일도 일어나지 않는다. 빈도가 먼저 판단되기 때문이다.
트레이 메뉴의 수동 확인은 어느 설정에서든 즉시 실행된다.

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
| 메모리 | 안정 상태 private working set 약 25 MB |
| 하이브리드 그래픽 | 이 앱은 디스플레이 드라이버를 전혀 로드하지 않는다. 다만 증상이 있던 노트북에서 GPU 전환이 실제로 되는지는 **아직 확인하지 못했다** |
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
