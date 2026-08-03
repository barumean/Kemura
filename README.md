# Kemura

**Emuera era-script 게임 에뮬레이터 (Godot 4.7 + C# / net8.0)**

uEmuera(Unity 포트)를 Godot 4.7 + .NET 8으로 이식한 프로젝트입니다.

---

## 현재 상태

솔직하게 적습니다. 이전 README는 검증되지 않은 "✅ 지원"을 나열하고 있었습니다.

| 항목 | 상태 |
|------|------|
| C# 컴파일 (`dotnet build`) | ✅ CI에서 검증 |
| 씬/리소스 로딩 | ✅ CI에서 헤드리스 임포트 검증 |
| 게임 목록 → 게임 시작 흐름 | ✅ 구현 (이전엔 버튼이 무반응이었음) |
| 콘솔 텍스트 렌더링 | ✅ 구현 (이전엔 배경색만 그려졌음) |
| 클릭 가능한 선택지 버튼 | ✅ 구현 (BBCode `[url]` + `meta_clicked`) |
| 숫자/문자열 입력 | ✅ 구현 (하단 입력 바) |
| 터치 / 마우스 / 키보드 진행 | ✅ 구현 |
| SHIFT-JIS `emuera.config` 키 인식 | ✅ 변환 테이블 로더 이식 완료 |
| 일본어 폰트 | ✅ `Fonts/msgothic.ttf` 포함 (라이선스 주의 — 아래 참조) |
| 실기(Android) 동작 검증 | ⚠️ **미검증** — 실기 테스트가 필요합니다 |
| 이미지/스프라이트 표시 | ⚠️ 부분 구현 (텍스처 캐시는 동작, 화면 배치는 미구현) |
| APK 서명 | ⚠️ 릴리스는 keystore 직접 설정 필요 |

### 알려진 제약

- **`MainWindow.Update()`의 데이터 경합**: 표시 갱신은 메인 스레드에서 돌지만,
  읽는 대상(`EmueraConsole.displayLineList`)은 Emuera 스레드가 갱신합니다.
  uEmuera 원본과 같은 구조로 이식했기 때문에 이 경합이 남아 있습니다.
  근본 해결에는 엔진 측 표시 리스트에 락을 넣는 별도 작업이 필요합니다.
- **이미지 출력**: `Graphics.DrawImage` 계열은 아직 로그만 남기는 스텁입니다.
  텍스처 로딩/캐시(`SpriteManager`)는 동작하지만 화면에 그리는 경로가 없습니다.
- **미구현 스텁**: `MainWindow.ShowConfigDialog` / `Reboot`, `DebugDialog`,
  `Media.SystemSounds` 등은 로그만 남깁니다.

---

## 프로젝트 구조

```
Kemura/
├── project.godot           # Godot 4.7 프로젝트 설정 (main_scene = main.tscn)
├── kemura.csproj           # .NET (net8.0, Godot.NET.Sdk 4.7.0)
├── kemura.sln
├── main.tscn               # 루트 씬: EmueraContent + FirstWindow + EmueraMain
├── first_window.tscn       # 게임 선택 UI (main.tscn에 인스턴스로 포함)
├── export_presets.cfg      # Android APK 내보내기 설정
├── .editorconfig           # nullable 경고를 신규 코드에만 적용
├── .github/workflows/      # CI (dotnet build + Godot 헤드리스 임포트)
├── Scripts/
│   ├── EmueraMain.cs       # 라이프사이클 + 매 프레임 표시 갱신 구동
│   ├── EmueraContent.cs    # 콘솔 렌더링 / 입력
│   ├── EmueraThread.cs     # Emuera 엔진 스레드
│   ├── FirstWindow.cs      # 게임 선택 + 권한 처리
│   ├── SpriteManager.cs    # 텍스처 캐시
│   ├── ConfigMaps.cs       # SHIFT-JIS config 키 변환 테이블 로더
│   ├── GenericUtils.cs     # 표시 계층 브리지
│   ├── FontUtils.cs        # 폰트 탐색
│   ├── Emuera/             # era 스크립트 엔진 (이식)
│   ├── uEmuera/            # Godot 호환 레이어 (이식)
│   └── Shaders/color_matrix.gdshader
├── Fonts/msgothic.ttf      # 일본어 폰트 (라이선스 주의: Fonts/README.md)
└── Text/                   # config 키 변환 테이블 (3파일 1:1 대응, 97행)
```

> 구 Unity 프로젝트(`Assets/`, `Packages/`, `ProjectSettings/`)는 삭제했습니다.
> Emuera 엔진 소스가 이중으로 존재해 어느 쪽을 수정해야 하는지 알 수 없는
> 상태였습니다. 필요하면 git 이력에서 복구할 수 있습니다.
> 삭제 전에 실제로 쓰이는 자산(`msgothic.ttf`, config 변환 테이블 3개)만
> `Fonts/`와 `Text/`로 옮겼습니다.

> Android 권한은 `export_presets.cfg`의 `permissions/*` 키로 지정합니다.
> 이전에 있던 `android/AndroidManifest.xml`은 Godot이 **읽지 않는 위치**였기
> 때문에(gradle 빌드 시 경로는 `android/build/src/com/godot/game/`) 삭제했습니다.

---

## 빌드

### 사전 요구사항

- **Godot 4.7 (.NET 에디션)**: https://godotengine.org/download
  - 파일명에 `mono` / `dotnet`이 들어간 것. 일반판은 C#을 실행할 수 없습니다.
  - 압축을 풀 때 **`GodotSharp/` 폴더를 exe와 같은 위치에 유지**해야 합니다.
    exe만 옮기면 `Microsoft.Build.Framework를 찾을 수 없음` 오류가 납니다.
- **.NET SDK 8.0** — 런타임(Runtime)이 아니라 **SDK**여야 합니다:
  https://dotnet.microsoft.com/download/dotnet/8.0
  - `GodotSharp` 4.7.x는 `net8.0`만 제공하므로 8.0으로 맞춥니다.
  - 설치 확인: `dotnet --list-sdks` 에 항목이 나와야 합니다.
- Android 내보내기: **JDK 17+**, Android SDK (Godot 에디터 설정에서 경로 지정)

> `kemura.csproj`의 `Godot.NET.Sdk` 버전은 설치한 Godot 버전과 맞춰야 합니다.
> 현재 `4.7.0`. 에디터 버전은 Godot의 **Help → About**에서 확인하세요.

### 컴파일 확인

```bash
dotnet build kemura.csproj -c Release
```

CI와 동일한 검사입니다. 여기서 실패하면 에디터에서도 실행되지 않습니다.

### 폰트

`Fonts/msgothic.ttf`가 이미 포함되어 있어 추가 작업은 없습니다.
단 **공개 배포 시에는 Noto Sans JP로 교체해야 합니다**
(MS Gothic 재배포는 라이선스 위반). [Fonts/README.md](Fonts/README.md) 참조.

### 데스크톱 실행

```bash
godot --path .
```

### Android APK 패키징

현재 프리셋은 **gradle 빌드를 끈 상태**(`gradle_build/use_gradle_build=false`)라
Godot 기본 내보내기 템플릿을 씁니다. **빌드 템플릿 설치가 필요 없습니다.**
단 APK 서명에 Android SDK의 `apksigner`가 필요하므로 SDK 자체는 있어야 합니다.

#### 1단계: 사전 준비 (최초 1회)

**JDK 17** — https://adoptium.net (Temurin 17 LTS)

**Android SDK** — 두 가지 방법 중 하나:

| 방법 | 내용 |
|---|---|
| Android Studio | 설치하면 SDK가 함께 깔림. 가장 간단 |
| Command line tools | https://developer.android.com/studio#command-tools <br>압축 해제 후 `sdkmanager "platform-tools" "build-tools;34.0.0"` 실행 |

기본 SDK 경로 (Windows): `C:\Users\<사용자명>\AppData\Local\Android\Sdk`

**내보내기 템플릿** — Godot 에디터에서
`Editor > Manage Export Templates > Download and Install`
(설치된 Godot과 **정확히 같은 버전**이어야 합니다)

#### 2단계: 디버그 keystore 생성 (최초 1회)

디버그 APK도 서명이 필요합니다. JDK의 `keytool`로 만듭니다:

```powershell
keytool -keyalg RSA -genkeypair -alias androiddebugkey ^
  -keypass android -keystore debug.keystore -storepass android ^
  -dname "CN=Android Debug,O=Android,C=US" -validity 9999 -deststoretype pkcs12
```

> `keytool`을 못 찾으면 JDK의 `bin` 폴더를 PATH에 넣거나 전체 경로로 실행하세요.
> 예: `"C:\Program Files\Eclipse Adoptium\jdk-17...\bin\keytool.exe"`

Android Studio를 설치했다면 이미 있을 수 있습니다:
`C:\Users\<사용자명>\.android\debug.keystore`

#### 3단계: Godot 에디터 설정 (최초 1회)

`Editor > Editor Settings > Export > Android`

| 항목 | 값 |
|---|---|
| Java SDK Path | JDK 설치 폴더 (`bin`의 부모) |
| Android SDK Path | SDK 폴더 (`platform-tools`의 부모) |
| Debug Keystore | 2단계에서 만든 `debug.keystore` 경로 |
| Debug Keystore User | `androiddebugkey` |
| Debug Keystore Pass | `android` |

#### 4단계: 내보내기

**에디터에서:** `Project > Export > Android` 선택 → `Export Project`
→ 저장 위치 `build/kemura.apk`

**CLI에서:**
```powershell
godot --headless --path . --export-debug Android build/kemura.apk
```

> CLI로 하기 전에 한 번은 에디터로 프로젝트를 열어 임포트를 끝내야 합니다
> (`.godot/` 캐시 생성). C# 어셈블리도 먼저 빌드되어 있어야 합니다.

#### 5단계: 설치

```powershell
adb install -r build\kemura.apk
```

USB 디버깅이 켜져 있어야 합니다 (설정 → 개발자 옵션). `adb`는 SDK의
`platform-tools`에 있습니다.

또는 APK 파일을 기기로 복사해 파일 관리자에서 탭 → "출처를 알 수 없는 앱" 허용.

#### 설치 후 필수 설정

**설정 → 앱 → Kemura → 권한 → 파일 및 미디어 → 모든 파일 접근 허용**

Android 11+ 에서 `MANAGE_EXTERNAL_STORAGE`는 앱 내 팝업으로 받을 수 없습니다.
게임 목록이 비어 보이면 이것부터 확인하세요.

게임은 `/storage/emulated/0/emuera/게임이름/ERB/` 에 넣습니다.

---

### 릴리스 빌드 (스토어 배포용)

디버그 keystore로 서명한 APK는 배포할 수 없습니다. 릴리스 keystore를 따로 만듭니다:

```powershell
keytool -keyalg RSA -genkeypair -alias kemura ^
  -keystore release.keystore -validity 10000 -deststoretype pkcs12
```

`export_presets.cfg`의 다음 항목을 채웁니다 (**저장소에 커밋하지 마세요**):

```
keystore/release="release.keystore 절대경로"
keystore/release_user="kemura"
keystore/release_password="설정한 비밀번호"
```

> 비밀번호를 파일에 쓰는 대신 환경 변수를 쓸 수 있습니다:
> `GODOT_ANDROID_KEYSTORE_RELEASE_PATH`,
> `GODOT_ANDROID_KEYSTORE_RELEASE_USER`,
> `GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD`

```powershell
godot --headless --path . --export-release Android build/kemura-release.apk
```

Google Play는 AAB를 요구합니다. 그때는 gradle 빌드가 필요합니다:
`export_presets.cfg`에서 `gradle_build/use_gradle_build=true`,
`gradle_build/export_format=1` 로 바꾸고
`Project > Install Android Build Template` 실행 후 내보내세요.

또한 스토어 배포 시 `Fonts/msgothic.ttf`를 Noto Sans JP로 교체해야 합니다
(MS Gothic 재배포는 라이선스 위반 — [Fonts/README.md](Fonts/README.md)).

---

### 자주 막히는 지점

| 증상 | 원인 / 해결 |
|---|---|
| `Android SDK path must be configured` | 3단계 미완료 |
| `Could not find keytool` / 서명 실패 | JDK 미설치 또는 Java SDK Path 오류 |
| `No export template found` | 템플릿 버전이 에디터 버전과 다름 |
| `Cannot instantiate C# script...` | C# 어셈블리 미빌드. `dotnet build kemura.csproj` 먼저 실행 |
| 앱이 켜지자마자 종료 | `adb logcat -s godot:V DEBUG:V` 로 로그 확인 |
| 게임 목록이 비어 있음 | "모든 파일 접근" 권한 미허용 |
| 일본어가 □로 표시 | `Fonts/` 에 폰트 없음 |

manifest를 직접 커스터마이즈해야 한다면 gradle 빌드를 켜고
`Project > Install Android Build Template`을 실행한 뒤
`android/build/src/com/godot/game/AndroidManifest.xml`을 수정하세요.
(현재 권한은 `export_presets.cfg`의 `permissions/*` 로 지정되어 있어
manifest 수정 없이도 동작합니다.)

---

## 게임 파일 배치

```
/storage/emulated/0/emuera/
└── 게임이름/
    ├── ERB/         (스크립트)
    ├── CSV/         (데이터)
    └── emuera.config
```

`ERB/`(또는 `erb/`) 폴더나 `emuera.config`가 있는 폴더만 목록에 표시됩니다.

경로는 첫 화면의 **[찾아보기]** 로 바꿀 수 있고 `user://settings.cfg`에 저장됩니다.
직접 입력한 뒤 Enter 또는 **[새로고침]** 을 눌러도 됩니다.
경로를 비우고 새로고침하면 플랫폼 기본값으로 돌아갑니다.

## 글자 크기

첫 화면의 **A- / A+** 로 조절하며(12~64px, 기본 28px), 게임 화면 우상단에도
같은 버튼이 있어 플레이 중에 바로 바꿀 수 있습니다. 설정은 저장됩니다.

> 표시 크기는 `emuera.config`의 `FontSize`와 별개입니다. 후자는 엔진 내부의
> 줄바꿈 계산용 값이고, 여기서 조절하는 것은 화면 표시 크기입니다.

### 권한 (Android 11+)

`MANAGE_EXTERNAL_STORAGE`는 런타임 팝업으로 받을 수 없습니다.
앱의 **[권한 설정]** 버튼을 누른 뒤,
**설정 → 앱 → Kemura → 권한 → 모든 파일 접근**을 허용하세요.
앱으로 돌아오면 자동으로 다시 검색합니다.

---

## Android 대응

| Android | API | 처리 방식 |
|---------|-----|-----------|
| 6 ~ 10 | 23~29 | `READ/WRITE_EXTERNAL_STORAGE` 런타임 권한 |
| 11+ | 30+ | `MANAGE_EXTERNAL_STORAGE` (설정에서 수동 허용) |
| 13+ | 33+ | `READ_MEDIA_IMAGES/VIDEO/AUDIO` 추가 |
| 15+ | 35+ | Godot 4.6 기본 템플릿이 16KB 페이지 정렬을 만족 |

빌드 대상 아키텍처는 `arm64-v8a` 단독입니다(`export_presets.cfg`).

> 위 표는 **설정상 대응**을 의미합니다. 실기 검증은 아직 수행되지 않았습니다.

---

## 원본 프로젝트

- uEmuera: https://github.com/xerysherry/uEmuera (Unity3D 포트)
- XEmuera: https://github.com/Fegelein21/XEmuera (Xamarin 포트)
- gEmuera: https://github.com/wwwXiaoHan17/gEmuera (Godot 포트 — 참고)
- Emuera: https://wiki.eragames.rip/index.php/Emuera
