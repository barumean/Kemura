# Kemura

**Emuera era-script 게임 에뮬레이터 (Godot 4.6 + C#)**

uEmuera(Unity 포트)를 Godot 4.6 + .NET으로 이식한 프로젝트입니다.

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
├── project.godot           # Godot 4.6 프로젝트 설정 (main_scene = main.tscn)
├── kemura.csproj           # .NET (net8.0 데스크톱 / net9.0 Android)
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

- **Godot 4.6 (.NET 에디션)**: https://godotengine.org/download
- **.NET 8.0 SDK** (데스크톱), **.NET 9.0 SDK** (Android)
- Android 내보내기: **JDK 17+**, Android SDK (Godot 에디터 설정에서 경로 지정)

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

### Android APK

현재 프리셋은 **gradle 빌드를 끈 상태**(`gradle_build/use_gradle_build=false`)입니다.
Godot 기본 내보내기 템플릿을 쓰므로 빌드 템플릿 설치가 필요 없습니다.

```bash
# 1. Godot 에디터에서 Android 내보내기 템플릿 다운로드
#    Editor > Manage Export Templates
# 2. 디버그 APK (에디터의 디버그 keystore 사용)
godot --headless --path . --export-debug Android build/kemura.apk
```

릴리스 빌드는 `export_presets.cfg`의 `keystore/release*`를 채워야 합니다.
(`package/signed=true`로 되어 있으므로 keystore 없이는 릴리스 내보내기가 실패합니다.
서명되지 않은 APK는 Android에 설치할 수 없습니다.)

manifest를 직접 커스터마이즈해야 한다면 gradle 빌드를 켜고
`Project > Install Android Build Template`을 실행한 뒤
`android/build/src/com/godot/game/AndroidManifest.xml`을 수정하세요.

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
