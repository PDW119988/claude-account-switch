# Claude Account Switch

[English](README.en.md) | **한국어**

Windows 에서 **Claude Code 로그인 계정을 클릭 한 번으로 바꾸는** 작은 트레이 프로그램입니다.
회사 계정과 개인 계정처럼 여러 계정을 번갈아 쓸 때, 매번 로그아웃하고 다시 로그인하지 않아도 됩니다.

> **비공식 도구입니다.** Anthropic 과 관계가 없고, Anthropic 이 지원하지 않습니다.

## 기능

- 계정 목록에서 골라 **[전환]**
- 작업표시줄 알림 영역 아이콘
  - 마우스를 올리면 지금 계정이 보이고, 계정마다 아이콘 색이 다릅니다
  - 왼쪽 클릭: 창 열기 · 오른쪽 클릭: 계정 바로 전환
- **[계정 등록]**: 이름을 넣고 [연결하기] → 브라우저에서 Claude 인증 → 완료
- Windows 시작 시 자동 실행 (체크로 켜고 끔)
- 설치 없음. exe 하나로 동작 (.NET Framework 4 · Windows 기본 포함)

## 설치

1. [Releases](../../releases) 에서 `ClaudeAccountSwitch.exe` 를 내려받아 원하는 폴더에 둡니다.
2. 더블클릭해 실행합니다.

처음 실행할 때 **"Windows의 PC 보호"** 창이 뜰 수 있습니다. 코드 서명 인증서가 없는 프로그램이라 그렇습니다.
**추가 정보 → 실행** 을 누르면 됩니다. 걱정되면 아래 [소스에서 빌드](#소스에서-빌드) 로 직접 만들어 쓰세요.

필요한 것:
- Windows 10 / 11
- [Claude Code](https://docs.claude.com/en/docs/claude-code) 설치 (`claude` 명령이 실행되는 상태)

## 사용법

### 계정 등록

1. **[계정 등록]** 을 누릅니다.
2. 목록에 보일 이름(예: `회사`, `개인`)을 넣습니다. 이메일은 선택이며, 넣으면 로그인 화면에 미리 채워집니다.
3. **[연결하기]** 를 누르면 브라우저에 Claude 인증 화면이 열립니다. 승인합니다.
4. 브라우저에 나온 **코드를 복사**해 창의 칸에 붙여넣고 **[완료]** 를 누릅니다.
   (코드를 복사한 채로 창으로 돌아오면 자동으로 채워집니다.)

> 브라우저가 claude.ai 에 다른 계정으로 로그인돼 있으면 그 계정으로 승인됩니다.
> 인증 화면에서 계정을 꼭 확인하세요.

이미 Claude Code 에 로그인된 계정은 다시 인증할 필요가 없습니다.
창 위쪽 노란 줄의 **[이 계정 등록]** 을 누르면 바로 목록에 들어갑니다.

### 전환

- 창에서 계정을 고르고 **[전환]** (계정을 더블클릭해도 됩니다)
- 또는 알림 영역 아이콘을 **오른쪽 클릭** → 계정 이름

**전환한 뒤에는 Claude Code 에서 새 대화를 열어야** 새 계정이 적용됩니다.
이미 열려 있던 대화는 이전 계정을 계속 씁니다.
VS Code 확장은 `Ctrl+Shift+P` → **Developer: Reload Window** 를 실행하면 확실합니다.

### 창과 종료

창을 닫아도 프로그램은 알림 영역에 남아 있습니다. 완전히 끄려면 아이콘 오른쪽 클릭 → **종료**.

## 동작 방식

Claude Code 는 로그인 정보를 두 곳에 둡니다. 이 프로그램은 **그 두 값만** 계정별로 저장해 두었다가 바꿔 끼웁니다.

| 파일 | 바꾸는 값 |
|---|---|
| `%USERPROFILE%\.claude\.credentials.json` | `claudeAiOauth` (로그인 토큰) |
| `%USERPROFILE%\.claude.json` | `oauthAccount` (계정 정보) |

- 파일의 나머지 내용(MCP 연결, 설정 등)은 **한 글자도 바꾸지 않습니다.** 파일을 통째로 다시 쓰지 않고 해당 값의 자리만 교체합니다.
- **계정 등록은 임시 폴더에서 로그인합니다.** `claude auth login` 을 임시 `CLAUDE_CONFIG_DIR` 로 실행하고, 끝나면 토큰만 가져온 뒤 임시 폴더를 지웁니다. 그래서 등록하는 동안 지금 쓰는 계정은 영향을 받지 않습니다.
- 로그인 토큰은 쓰는 동안 계속 새것으로 바뀝니다. 그래서 전환하기 직전에 지금 계정의 최신 토큰을 저장본에 덮어씁니다. 나중에 그 계정으로 돌아왔을 때 로그인이 풀려 있지 않게 하려는 것입니다.
- 전환할 때마다 두 파일의 원본을 `backup` 폴더에 남깁니다 (최근 10번).
- `CLAUDE_CONFIG_DIR` 환경 변수를 쓰고 있으면 그 폴더를 따릅니다.

## 저장 위치와 보안

```
%USERPROFILE%\.claude\account-switch\
  profiles\{이름}.json   계정별 토큰
  backup\                전환 직전 원본
```

- 저장본에는 **로그인 토큰이 그대로 들어 있습니다.** Claude Code 자신이 `.credentials.json` 에 토큰을 두는 것과 같은 수준의 보호입니다. 이 폴더를 다른 사람에게 주거나 클라우드 동기화 폴더로 옮기지 마세요.
- 이 프로그램은 **직접 네트워크에 접속하지 않습니다.** 로그인은 Claude Code 의 `claude auth login` 이 합니다.

## 알아 둘 것

- Claude Code 가 내부에 저장하는 파일 형식을 직접 다룹니다. **Claude Code 업데이트로 형식이 바뀌면 동작하지 않을 수 있습니다.**
- 이전 계정으로 열려 있던 Claude Code 대화가 계속 돌아가면, 그 대화가 토큰을 갱신하면서 이전 계정 정보를 다시 쓸 수 있습니다. 전환한 뒤에는 열려 있던 대화를 닫거나 창을 다시 불러오세요.
- 각 계정은 그 계정의 이용약관에 따라 쓰세요. 이 도구는 여러 계정 사이를 편하게 오가기 위한 것입니다.
- Windows 전용입니다. (macOS 의 Claude Code 는 토큰을 키체인에 둡니다.)

## 소스에서 빌드

Visual Studio 나 SDK 없이, Windows 에 들어 있는 C# 컴파일러로 빌드합니다.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

결과물: `dist\ClaudeAccountSwitch.exe`

```
src\
  Program.cs        진입점 · 중복 실행 방지
  TrayApp.cs        알림 영역 아이콘 · 자동 실행
  MainForm.cs       계정 목록 창
  RegisterForm.cs   계정 등록 창
  Login.cs          claude auth login 실행 (임시 폴더)
  Store.cs          계정 저장 · 전환 · 토큰 동기화 · 백업
  Json.cs           값 하나만 바꿔 끼우는 JSON 도우미
  Ui.cs             아이콘 · 한국어/영어 문구
tools\make-icon.ps1 앱 아이콘 생성
```

화면 언어는 Windows 표시 언어를 따릅니다 (한국어 Windows 면 한국어, 그 밖에는 영어).

## 제거

1. 창에서 **Windows 시작 시 자동 실행** 체크를 끕니다.
2. 알림 영역 아이콘 → **종료**.
3. `ClaudeAccountSwitch.exe` 를 지웁니다.
4. 저장된 계정까지 지우려면 `%USERPROFILE%\.claude\account-switch\` 폴더를 지웁니다.
   (Claude Code 의 지금 로그인 상태는 그대로 남습니다.)

## 라이선스

[MIT](LICENSE)
