# Kemura

**Emuera era-script 게임 에뮬레이터 (Godot 4.7 + C# / net9.0)**

uEmuera(Unity 포트)를 Godot 4.7 + .NET 9로 이식한 프로젝트입니다.

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
| APK 빌드 | ✅ 실기용 APK 빌드 성공 |
| 실기(Android) 실행 | ✅ 앱 실행·게임 목록 표시 확인 (Galaxy S26) |
| 실기(Android) 게임 로드 | ⚠️ 파일명 대소문자 문제를 수정했으나 **재검증 필요** |
| 이미지/스프라이트 표시 | ⚠️ 부분 구현 (텍스처 캐시는 동작, 화면 배치는 미구현) |
| APK 서명 | ⚠️ 릴리스는 keystore 직접 설정 필요 |
| EmueraEE 확장 명령 | ❌ **미구현** — 이걸 쓰는 게임은 실행되지 않습니다 (아래 참조) |

### 알려진 제약

- **EmueraEE 확장 명령 미구현.** 이 엔진은 **Emuera 1.824 계열**입니다.
  EmueraEE(Emuera Extended Edition) 계열이 추가한 명령은 하나도 없습니다.
  이걸 쓰는 게임은 로딩 중 「해석할 수 없는 행입니다」 로 중단됩니다.

  | 분류 | 명령 |
  |---|---|
  | DataTable | `DT_CREATE` `DT_RELEASE` `DT_EXIST` `DT_COLUMN_ADD` `DT_COLUMN_OPTIONS` `DT_ROW_ADD` `DT_ROW_REMOVE` `DT_ROW_LENGTH` `DT_CELL_GET(S)` `DT_CELL_SET` `DT_SELECT` `DT_FROMXML` |
  | Map | `MAP_CREATE` `MAP_RELEASE` `MAP_EXIST` `MAP_HAS` `MAP_GET` `MAP_SET` `MAP_SIZE` |
  | XML | `XML_GET` `XML_ADDNODE` `XML_REMOVENODE` |
  | 사운드 | `PLAYBGM` `STOPBGM` `STOPSOUND` `SETBGMVOLUME` `SETSOUNDVOLUME` |
  | 리플렉션 | `EXISTVAR` `GETVAR` `GETVARS` `GETMETH` `GETMETHS` `EXISTFUNCTION` |
  | 기타 | `REGEXPMATCH` `TIMESF` `HTML_STRINGLEN` `GETTEXTBOX` `MOUSEB` |

  `LOADTEXT` 는 있지만 EE 와 인수 타입이 달라
  `LOADTEXT("dat/schema.xml")` 같은 문자열 경로 호출은 실패합니다.

  **임시 대응**: 우상단 **≡ 메뉴 → [해석 오류 무시]** 를 켜면 강제로
  실행됩니다. 다만 해당 행이 실제로 실행되는 기능은 오작동합니다
  (`emuera.config` 의 `解釈不可能な行があっても実行する` 와 같은 설정).

  대응 비용 검토와 계층별 우선순위는 [`docs/EMUERA_EM_GAP.md`](docs/EMUERA_EM_GAP.md)
  에 정리했습니다. 규격 출처는 <https://gitlab.com/EvilMask/emuera.em.doc> 입니다.

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
├── kemura.csproj           # .NET (net9.0, Godot.NET.Sdk 4.7.0)
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
- **.NET SDK 9.0** — 런타임(Runtime)이 아니라 **SDK**여야 합니다:
  https://dotnet.microsoft.com/download/dotnet/9.0
  - Godot 4.7 (.NET 에디션) 이 net9.0 을 전제로 빌드/내보내기합니다.
  - 설치 확인: `dotnet --list-sdks` 에 항목이 나와야 합니다.
- Android 내보내기: **JDK 17 (정확히 17)**, Android SDK
  — 자세한 버전 요구사항은 아래 [버전 요구사항](#버전-요구사항) 표 참조

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

## 버전 요구사항

**버전이 하나라도 어긋나면 빌드가 실패합니다.** 아래가 검증된 조합입니다.

| 구성요소 | 필요 버전 | 확인 명령 | 근거 |
|---|---|---|---|
| Godot | **4.7** (.NET 에디션) | Help → About | `project.godot` 의 `config/features` |
| `Godot.NET.Sdk` | **4.7.0** | `kemura.csproj` 1행 | 에디터 버전과 일치해야 함 |
| .NET SDK | **9.0** | `dotnet --list-sdks` | Godot 4.7 (.NET) 이 net9.0 전제 |
| `TargetFramework` | **net9.0** | `kemura.csproj` | 위와 동일 |
| JDK | **17 (정확히 17)** | `java -version` | Godot 4.x Android 내보내기 요구사항 |
| Android SDK build-tools | **34.0.0** 이상 | `sdkmanager --list_installed` | APK 서명(`apksigner`)에 필요 |
| 내보내기 템플릿 | **에디터와 동일 버전** | Editor → Manage Export Templates | 버전 불일치 시 내보내기 실패 |

### TargetFramework 는 net9.0 입니다

Godot 4.7 (.NET 에디션) 은 net9.0 을 전제로 C# 을 빌드하고 내보냅니다.
에디터가 기대하는 TFM 과 어긋나면 내보내기 단계에서 걸립니다.

> **이전 README 의 설명은 틀렸습니다.** "GodotSharp 4.7.x 가 `lib/net8.0` 만
> 제공하므로 net8.0 을 써야 한다"고 적혀 있었지만, 이는 근거가 되지 않습니다.
> net9.0 프로젝트는 net8.0 라이브러리를 그대로 참조할 수 있으므로
> `lib/net8.0` 의 존재가 프로젝트의 TFM 을 net8.0 으로 묶지 않습니다.
> `.NET SDK` 와 `TargetFramework` 는 **에디터가 요구하는 버전**에 맞춥니다.

### JDK 17 을 정확히 써야 하는 이유

`JDK 17 이상`이 아닙니다. JDK 21/24/25 를 쓰면 gradle 빌드가
`Unsupported class file major version` 또는 `Unsupported Java` 로 실패합니다.
Godot이 쓰는 Android Gradle Plugin이 JDK 17 기준입니다.

**JDK가 여러 개 설치된 경우가 가장 흔한 실패 원인입니다.** 예:

```
JDK 25 설치  +  JDK 17 설치  →  Godot/gradle 이 25를 집어서 실패
JAVA_HOME = C:\Program Files\Unity\Hub\Editor\2022.3.x\...\OpenJDK  (Unity 잔재)
```

**해결 — 세 곳을 모두 17로 맞추세요:**

1. **Godot 에디터 설정**
   `Editor Settings > Export > Android > Java SDK Path`
   → JDK 17 폴더 (예: `C:\Program Files\Eclipse Adoptium\jdk-17.0.13.11-hotspot`)

2. **`JAVA_HOME` 환경 변수** — gradle이 이것을 우선 봅니다
   ```powershell
   # 현재 값 확인
   echo $env:JAVA_HOME
   # 영구 설정 (관리자 PowerShell)
   [Environment]::SetEnvironmentVariable("JAVA_HOME", "C:\Program Files\Eclipse Adoptium\jdk-17.0.13.11-hotspot", "Machine")
   ```
   설정 후 **Godot을 완전히 종료하고 다시 실행**해야 반영됩니다.

3. **확인**
   ```powershell
   java -version          # openjdk version "17.x.x"
   echo $env:JAVA_HOME    # JDK 17 경로
   ```

> `android/build/gradle.properties` 에 `org.gradle.java.home` 을 직접 적어
> 강제할 수도 있습니다. 다른 방법이 안 통할 때 쓰는 최후 수단입니다.

---

### Android APK 패키징

> **C# 프로젝트는 gradle 빌드가 필수입니다.**
> `.NET` 런타임을 APK에 통합하려면 gradle을 거쳐야 하므로,
> `gradle_build/use_gradle_build=true` 이고 **Android 빌드 템플릿을 설치해야** 합니다.
> 설치하지 않으면 `'android'은(는) 내부 또는 외부 명령이 아닙니다` 오류가 납니다
> (Godot이 없는 `android/gradlew` 를 실행하려 한 것).

#### 1단계: 사전 준비 (최초 1회)

**JDK 17** — https://adoptium.net (Temurin **17** LTS. 21/24 아님)

**.NET SDK 9.0** — https://dotnet.microsoft.com/download/dotnet/9.0
(**SDK** 열에서 받으세요. Runtime 아님)

**Android SDK** — 두 가지 방법 중 하나:

| 방법 | 내용 |
|---|---|
| Android Studio | 설치하면 SDK가 함께 깔림. 가장 간단 |
| Command line tools | https://developer.android.com/studio#command-tools <br>압축 해제 후 아래 명령 실행 |

```powershell
sdkmanager "platform-tools" "build-tools;35.0.0" "platforms;android-35" "cmdline-tools;latest"
```

gradle 빌드를 쓰므로 `platforms;android-35` 도 **필요합니다**
(`export_presets.cfg` 의 `gradle_build/target_sdk="35"` 와 맞춰야 함).

기본 SDK 경로 (Windows): `C:\Users\<사용자명>\AppData\Local\Android\Sdk`

**내보내기 템플릿** — Godot 에디터에서
`Editor > Manage Export Templates > Download and Install`
(설치된 Godot과 **정확히 같은 버전**이어야 합니다)

#### 1.5단계: Android 빌드 템플릿 설치 (최초 1회, C#에 필수)

Godot 에디터에서 `Project > Install Android Build Template`

이것이 `android/build/` 폴더에 gradle 프로젝트(`gradlew`, `src/`, `build.gradle`)를
생성합니다. **이 단계를 빠뜨리면 내보내기가 `'android'... 명령이 아닙니다` 로 실패합니다.**

> 실행 전에 `Editor Settings > Export > Android` 의 SDK/JDK 경로가
> 먼저 설정되어 있어야 합니다(3단계).
> 순서가 꼬였으면 `android/` 폴더를 지우고 다시 설치하세요.

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
| **`'android'은(는) 내부 또는 외부 명령이 아닙니다`** | **Android 빌드 템플릿 미설치.** `Project > Install Android Build Template` 실행 (1.5단계).<br>확인: `dir android\build\gradlew.bat` 이 존재해야 함 |
| `Android build template not installed` | 위와 동일 |
| **`Unrecognized UID: "uid://..."`** <br> `Error opening file ''` | **낡은 `.godot/` 캐시.** 이미 사라진 리소스의 UID를 붙들고 있음.<br>Godot 종료 후 `rmdir /s /q .godot obj bin` → 다시 열어 재임포트 |
| `Android SDK path must be configured` | 3단계 미완료 |
| `Could not find keytool` / 서명 실패 | JDK 미설치, 또는 Java SDK Path가 JDK 17이 아님 |
| `Unsupported class file major version` <br> `Unsupported Java` | **JDK 버전 불일치**. 21/24/25가 잡혀 있음 → `JAVA_HOME` 과 에디터 설정 **양쪽** 을 17로 |
| `apksigner not found` | `build-tools` 미설치 → `sdkmanager "build-tools;34.0.0"` |
| `No export template found` | 템플릿 버전이 에디터 버전과 다름 |
| `NU1102: Unable to find package Godot.NET.Sdk` | csproj의 SDK 버전이 실제 Godot 버전과 다름 |
| `No .NET SDKs were found` | 런타임만 설치됨 → **SDK** 9.0 설치 |
| `... requires a newer runtime` (실행 시) | 설치된 .NET 런타임이 `TargetFramework`보다 낮음. **SDK 9.0** 설치 확인 |
| `Cannot instantiate C# script...` | C# 어셈블리 미빌드 또는 `AssemblyName` 불일치. `dotnet build kemura.csproj` 실행 후 `<AssemblyName>Kemura</AssemblyName>` 확인 |
| 앱이 켜지자마자 종료 | `adb logcat -s godot:V DEBUG:V` 로 로그 확인 |
| 게임 목록이 비어 있음 | "모든 파일 접근" 권한 미허용 |
| 일본어가 □로 표시 | `Fonts/` 에 폰트 없음 |

### Gradle 이 Windows 에서 실패할 때

```
Could not move temporary workspace (...groovy-dsl\<hash>-<uuid>)
                    to immutable location (...groovy-dsl\<hash>)
```

Gradle 이 임시 폴더를 최종 위치로 원자적 이동(rename)하려는데 Windows 가
거부한 것입니다. **Godot 과 무관한 Gradle 의 알려진 Windows 문제**입니다.

원인은 거의 항상 셋 중 하나입니다.

| 원인 | 설명 |
|---|---|
| 실시간 검사(백신) | Gradle 이 쓴 파일을 백신이 즉시 스캔하며 핸들을 잡아 rename 실패. **가장 흔함** |
| Gradle 데몬 잔존 | 이전 실행의 데몬이 캐시를 붙들고 있음 |
| 캐시 손상 | 중단된 빌드가 반쯤 쓴 파일을 남김 |

#### 순서대로 시도

**1. 데몬 종료 + 캐시 삭제** (대부분 여기서 해결)

```powershell
# Godot 종료 후
taskkill /f /im java.exe
rmdir /s /q %USERPROFILE%\.gradle\caches
```

**2. Windows Defender 예외 추가** — 재발 방지에 가장 효과적

`Windows 보안 > 바이러스 및 위협 방지 > 설정 관리 > 제외 항목 추가`
에 아래 **폴더** 를 추가합니다.

```
%USERPROFILE%\.gradle
<프로젝트 폴더>\android
```

또는 관리자 PowerShell 에서:

```powershell
Add-MpPreference -ExclusionPath "$env:USERPROFILE\.gradle"
Add-MpPreference -ExclusionPath "C:\Users\yooshin\Kemura\android"
```

**3. Gradle 캐시를 짧고 안 스캔되는 경로로 옮기기**

경로가 길면 Windows 260자 제한에도 걸립니다.

```powershell
[Environment]::SetEnvironmentVariable("GRADLE_USER_HOME", "C:\gradle", "Machine")
```

설정 후 Godot 을 완전히 재시작하세요.

**4. 데몬 없이 실행**

`android/build/gradle.properties` 에 추가:

```properties
org.gradle.daemon=false
org.gradle.parallel=false
```

> `android/` 는 `.gitignore` 대상이라 이 수정은 로컬에만 남습니다.
> 빌드 템플릿을 다시 설치하면 사라지므로 재적용이 필요합니다.

#### Docker 는 필요 없습니다 — CI 로 빌드하세요

Docker 로 Android 빌드 환경을 꾸릴 수는 있지만, Godot 에디터가 gradle
프로젝트를 생성하는 구조라 설정이 번거롭습니다. **더 간단한 방법은 CI 입니다.**

`.github/workflows/android.yml` 을 추가해 두었습니다. Linux 러너에서는
위 파일 락 문제가 발생하지 않습니다.

```
GitHub 저장소 > Actions > android-apk > Run workflow
```

완료되면 `kemura-debug-apk` 아티팩트를 내려받아 기기에 설치하면 됩니다.
`v` 로 시작하는 태그를 푸시해도 자동 실행됩니다.

```powershell
git tag v0.3.0 && git push --tags
```

이 워크플로는 에디터의 `Install Android Build Template` 과 같은 일을
스크립트로 합니다 — 내보내기 템플릿 안의 `android_source.zip` 을
`android/build/` 에 풀어놓을 뿐입니다.

---

#### `.import` / `.uid` 파일은 커밋하세요

Godot이 처음 프로젝트를 열면 `*.import`(임포트 설정)와 `*.uid`(리소스 식별자)를
생성합니다. `.gitignore` 에서 일부러 제외해 두었으니 **생성된 뒤 커밋**하세요.

```powershell
git add -A -- "*.import" "*.uid"
git commit -m "Godot import metadata 추가"
```

커밋하지 않으면 머신마다 UID가 새로 생성되어 `.tscn` 의 `ext_resource` 참조가
어긋나고, 위의 `Unrecognized UID` 오류가 반복됩니다.

#### 진단용 한 줄 점검

빌드 전에 이것만 돌려도 버전 문제 대부분이 걸러집니다:

```powershell
java -version; dotnet --list-sdks; findstr /n "Godot.NET.Sdk TargetFramework AssemblyName" kemura.csproj
```

기대값:
```
openjdk version "17.x.x"
9.0.xxx [C:\Program Files\dotnet\sdk]
1:<Project Sdk="Godot.NET.Sdk/4.7.0">
9:    <TargetFramework>net9.0</TargetFramework>
23:    <AssemblyName>Kemura</AssemblyName>
```

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

`ERB/` 폴더나 `emuera.config` 가 있는 폴더만 목록에 표시됩니다.
폴더명·파일명의 대소문자는 구분하지 않습니다(`erb/`, `Erb/`, `ERB/` 모두 인식).

경로는 첫 화면의 **[찾아보기]** 로 바꿀 수 있고 `user://settings.cfg`에 저장됩니다.
직접 입력한 뒤 Enter 또는 **[새로고침]** 을 눌러도 됩니다.
경로를 비우고 새로고침하면 플랫폼 기본값으로 돌아갑니다.

## 글자 크기

게임 화면 우상단의 **≡** 메뉴 → **글자 크기 설정** 에서 **A- / A+** 로
조절합니다(12~64px, 기본 28px). 설정은 `user://settings.cfg` 에 저장되어
다음 실행에도 유지됩니다.

같은 메뉴에 **다시 시작 / 로그 저장 / 게임 종료(목록으로) / 앱 종료** 가 있습니다.

> 표시 크기는 `emuera.config`의 `FontSize`와 별개입니다. 후자는 엔진 내부의
> 줄바꿈 계산용 값이고, 여기서 조절하는 것은 화면 표시 크기입니다.

### 권한 (Android 11+)

`MANAGE_EXTERNAL_STORAGE`는 런타임 팝업으로 받을 수 없고, Godot 에는 해당
설정 화면을 직접 여는 API 가 없습니다. 그래서 두 가지 방법이 있습니다.

**방법 A — 모든 파일 접근 허용**

앱의 **[권한 설정]** 버튼을 누른 뒤,
**설정 → 앱 → Kemura → 권한 → 모든 파일 접근**을 허용하세요.
앱으로 돌아오면 자동으로 다시 검색합니다.

**방법 B — 권한 없이 (앱 전용 폴더)**

```
/storage/emulated/0/Android/data/com.kemura.emuera/files/emuera/
```

앱 전용 외부 경로라 **어떤 Android 버전에서도 권한이 필요 없습니다.**
PC 에 USB 로 연결(MTP)해서 이 경로에 게임 폴더를 넣고,
첫 화면의 **[앱 전용 폴더]** 버튼을 누르세요.

> Android 11+ 에서는 기기 내 파일 관리자로 `Android/data/` 안에 들어가는 것이
> 제한됩니다. PC 에서 USB 로 넣는 편이 확실합니다.

### 경로 지정이 번거로울 때

Godot 의 파일 대화상자는 모바일에서 조작이 불편합니다. 첫 화면의
**[내장 저장소] / [앱 전용 폴더] / [상위]** 버튼으로 대화상자를 열지 않고
바로 이동할 수 있고, 경로를 직접 입력한 뒤 Enter 를 눌러도 됩니다.

---

## Android 대응

| Android | API | 처리 방식 |
|---------|-----|-----------|
| 6 ~ 10 | 23~29 | `READ/WRITE_EXTERNAL_STORAGE` 런타임 권한 |
| 11+ | 30+ | `MANAGE_EXTERNAL_STORAGE` (설정에서 수동 허용) |
| 13+ | 33+ | `READ_MEDIA_IMAGES/VIDEO/AUDIO` 추가 |
| 15+ | 35+ | Godot 4.7 기본 템플릿이 16KB 페이지 정렬을 만족 |

빌드 대상 아키텍처는 `arm64-v8a` 단독입니다(`export_presets.cfg`).

### 파일 이름 대소문자 (PC에서는 되고 실기에서만 안 될 때)

Windows 파일 시스템은 대소문자를 구분하지 않지만 **Android/Linux는 구분합니다.**
Emuera 엔진은 파일명을 `GAMEBASE.CSV`, `ABL.CSV` 처럼 대문자로 하드코딩하고,
스크립트도 `Directory.GetFiles(dir, "*.ERB")` 로 찾습니다. `Directory.GetFiles`는
**패턴의 대소문자까지 구분**하므로, 확장자가 소문자인 게임에서는 ERB 파일이
0개로 나와 게임이 통째로 로드되지 않았습니다.

원본 uEmuera 는 소문자 패턴으로 한 번 더 검색하는 코드를 넣어 대응했지만,
그 블록이 모두 `#if (UNITY_ANDROID || UNITY_IOS)` 로 감싸여 있어서 Godot 이식
후에는 한 번도 컴파일되지 않았습니다.

지금은 `Scripts/PathResolver.cs` 가 이 문제를 전담합니다.

- 정확한 경로가 존재하면 그대로 사용합니다(빠른 경로 — 올바른 대소문자로
  배포된 게임에는 추가 비용이 없습니다).
- 실패했을 때만 디렉터리를 훑어 대소문자를 무시해 찾고, 결과를 캐시합니다.
- `PathResolver.GetFiles` 는 glob 패턴을 IgnoreCase 정규식으로 바꿔 처리하므로
  `*.ERB` 하나로 `.erb` / `.Erb` / `.ERB` 를 모두 잡습니다.

### 화면에 보이지 않는 오류 (진단 방법)

Android 에는 볼 수 있는 콘솔이 없습니다. 예전에는 로드 실패가
`GD.PushError` 와 `MessageBox` 스텁(로그 출력)으로만 남아서 사용자에게는
**빈 화면**으로만 보였고, PC에서는 stdout 으로 원인이 보였기 때문에 증상이
전혀 달랐습니다.

지금은 다음이 화면에 직접 표시됩니다.

- 게임 시작 전 사전 검사 — ERB 폴더 부재, `.ERB` 파일 부재, 권한 부족을
  각각 구체적인 문구로 첫 화면 상태줄에 표시합니다.
- 엔진 스레드가 예외로 죽으면 예외 종류와 메시지를 본문에 붉게 출력합니다.

그래도 원인을 모를 때는 USB 디버깅을 켜고 로그를 직접 봅니다.

```bash
adb logcat -c                       # 기존 로그 삭제
adb logcat -s godot:V DEBUG:V AndroidRuntime:E
```

`[FirstWindow]`, `Game path:`, `PathResolver:` 로 시작하는 줄을 확인하세요.

### 런처 아이콘

`android_icons/` 에 legacy 192x192 와 적응형 전경/배경 432x432 를 두고
`export_presets.cfg` 의 `launcher_icons/*` 에 연결합니다. 비워두면 홈 화면에
프로젝트 아이콘이 축소되어 나와 거의 보이지 않습니다.

적응형 아이콘은 마스크가 가장자리를 잘라내므로 실제로 보이는 것은 중앙
원(지름 288px)뿐입니다. 내용은 중앙 264x264 안전 영역 안에 두어야 합니다.

---

## 원본 프로젝트

- uEmuera: https://github.com/xerysherry/uEmuera (Unity3D 포트)
- XEmuera: https://github.com/Fegelein21/XEmuera (Xamarin 포트)
- gEmuera: https://github.com/wwwXiaoHan17/gEmuera (Godot 포트 — 참고)
- Emuera: https://wiki.eragames.rip/index.php/Emuera
