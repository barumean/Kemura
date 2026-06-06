# Kemura

**Emuera era-script 게임 에뮬레이터 — Android 최신 버전 지원**

uEmuera 포크에서 시작해 Godot 4.6 + C#/.NET 9 기반으로 완전 마이그레이션.

---

## 기술 스택 결정 이유

| 항목 | Unity 2019 (구) | Godot 4.6 (현재) |
|------|----------------|-----------------|
| 엔진 상태 | EOL (2022 지원 종료) | 활성 개발 중 |
| Android NDK | r19/r21 (구버전) | 최신 지원 |
| 16KB 페이지 정렬 | 미지원 | 지원 (Android 15+) |
| .NET 버전 | .NET Standard 2.0 | **.NET 9.0** (Android) |
| AGP 버전 | 7.4.2 (최대) | 8.x 지원 |
| 라이선스 | Unity Runtime Fee | **완전 무료** |

> Unity 2019는 Android 16(2026)의 요구사항(16KB 페이지 정렬, AGP 8.x, NDK r25+)을  
> 충족하지 못합니다. Godot 4.6 + .NET 9이 최적 선택입니다.

---

## 프로젝트 구조

```
Kemura/
├── project.godot          # Godot 4.6 프로젝트 설정
├── kemura.csproj          # .NET 프로젝트 (net8.0 / net9.0 Android)
├── kemura.sln             # Visual Studio 솔루션
├── first_window.tscn      # 게임 선택 UI 씬
├── main.tscn              # 메인 에뮬레이터 씬
├── export_presets.cfg     # Android APK 내보내기 설정
├── android/
│   └── AndroidManifest.xml
├── Scripts/
│   ├── EmueraMain.cs      # 메인 노드 (게임 라이프사이클)
│   ├── EmueraContent.cs   # UI 렌더링 (Godot Control 기반)
│   ├── EmueraThread.cs    # 백그라운드 스레드
│   ├── FirstWindow.cs     # 게임 선택 화면
│   ├── SpriteManager.cs   # 텍스처 캐시 관리
│   ├── GenericUtils.cs    # 유틸리티
│   ├── FontUtils.cs       # 폰트 관리
│   ├── Emuera/            # era 스크립트 엔진 (플랫폼 독립)
│   ├── uEmuera/           # Godot 호환 레이어
│   └── Shaders/
│       └── color_matrix.gdshader
├── Fonts/                 # 폰트 파일 (MS Gothic 등 직접 추가 필요)
└── Assets/                # 구 Unity 프로젝트 (참조용 보존)
```

---

## 빌드 방법

### 사전 요구사항

- **Godot 4.6** (.NET 에디션): https://godotengine.org/download
- **.NET 8.0 SDK** (데스크톱 빌드)
- **.NET 9.0 SDK** (Android 빌드)
- **Android SDK** (API 35+), **Android NDK** (r25+)
- **JDK 17+**

### 폰트 파일 추가

`Fonts/` 폴더에 다음 중 하나를 배치:
- `msgothic.ttc` (MS Gothic — 일본어/한국어 지원)
- `NotoSansMono.ttf` (대체 폰트)

### Android APK 빌드

```bash
# 1. Godot 에디터에서 Android 내보내기 프리셋 설정
#    Project > Export > Android

# 2. CLI 빌드 (옵션)
dotnet build -p:GodotTargetPlatform=android

# 3. Godot 에디터에서 Export > Export Project
```

### 데스크톱 실행

```bash
# Godot 에디터에서 F5 또는
godot --path /path/to/Kemura
```

---

## 게임 파일 배치

Android 기기에서:

```
/storage/emulated/0/emuera/
└── 게임이름/
    ├── ERB/         (스크립트 파일)
    ├── CSV/         (데이터 파일)
    └── emuera.config
```

앱 첫 실행 시 `MANAGE_EXTERNAL_STORAGE` 권한을 허용해야 합니다.

---

## Android 호환성

| Android 버전 | API | 상태 |
|-------------|-----|------|
| Android 5.0+ | API 21+ | ✅ 최소 지원 |
| Android 10 | API 29 | ✅ |
| Android 11 | API 30 | ✅ (MANAGE_EXTERNAL_STORAGE) |
| Android 13 | API 33 | ✅ (Media 권한) |
| Android 15 | API 35 | ✅ (16KB 페이지 정렬) |
| Android 16 | API 36 | ✅ (Godot 4.6 지원) |

---

## 원본 프로젝트

- uEmuera: https://github.com/xerysherry/uEmuera (Unity3D 포트)
- XEmuera: https://github.com/Fegelein21/XEmuera (Xamarin 포트)
- gEmuera: https://github.com/wwwXiaoHan17/gEmuera (Godot 포트 — 참고)
- Emuera: https://wiki.eragames.rip/index.php/Emuera
