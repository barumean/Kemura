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
| EmueraEE 확장 명령 | ⚠️ **부분 구현** (54종). 남은 것은 아래 참조 |

### 알려진 제약

- **EmueraEE 확장 명령 부분 구현.** 이 엔진은 **Emuera 1.824 계열**이고,
  EmueraEE(EM+EE) 확장을 필요한 만큼 얹고 있습니다.
  규격 출처: <https://gitlab.com/EvilMask/emuera.em.doc>

  **구현됨 (54종)**

  | 분류 | 명령 |
  |---|---|
  | Map (9) | `MAP_CREATE` `MAP_EXIST` `MAP_RELEASE` `MAP_CLEAR` `MAP_GET` `MAP_HAS` `MAP_SET` `MAP_REMOVE` `MAP_SIZE` |
  | DataTable (19) | `DT_CREATE` `DT_EXIST` `DT_RELEASE` `DT_CLEAR` `DT_NOCASE` `DT_COLUMN_ADD/EXIST/REMOVE/LENGTH/OPTIONS` `DT_ROW_ADD/SET/REMOVE/LENGTH` `DT_CELL_GET/GETS/ISNULL/SET` `DT_SELECT` |
  | XML (13) | `XML_DOCUMENT` `XML_EXIST` `XML_RELEASE` `XML_TOSTR` `XML_GET(_BYNAME)` `XML_SET(_BYNAME)` `XML_ADDNODE(_BYNAME)` `XML_REMOVENODE` `XML_ADDATTRIBUTE` `XML_REMOVEATTRIBUTE` |
  | 사운드 (7) | `PLAYBGM` `PLAYSOUND` `STOPBGM` `STOPSOUND` `SETBGMVOLUME` `SETSOUNDVOLUME` `EXISTSOUND` |
  | 기타 (6) | `EXISTFUNCTION` `HTML_STRINGLEN` `CBRT` `LOG` `LOG10` `EXPONENT` |

  `DT_*` 는 `System.Data.DataTable`, `XML_*` 는 `System.Xml` + XPath 위임입니다.
  EM 문서가 그렇게 규정하므로 필터식·정렬식·XPath 문법이 BCL 에서 따라옵니다.

  **미구현 — 아직 이걸 쓰는 게임은 로딩 중 중단됩니다**

  | 항목 | 이유 |
  |---|---|
  | `REGEXPMATCH` | 출력 인수(`groupCount`, `matches`)가 있어 전용 `ArgumentBuilder` 필요 |
  | `DT_SELECT` 의 `output` 인수, `DT_COLUMN_NAMES` | 같음 (`DT_SELECT` 는 `RESULT:1~` 형태만 구현) |
  | `XML_GET` 의 `outputArray` (형태 2·4) | 같음 (`doOutput` → `RESULTS` 형태만 구현) |
  | `GETVAR` `GETVARS` `SETVAR` `GETMETH` `GETMETHS` `EXISTVAR` `EXISTMETH` | 엔진에 이름→변수 해석기가 없습니다(컴파일 시점에만 처리) |
  | `GETTEXTBOX` `MOUSEB` `INPUTMOUSEKEY` `TOOLTIP_*` | UI 작업 필요 |
  | `GCREATE`/`GDRAW*`/`SPRITE*` | **이미지 출력이 스텁**입니다. 그래픽 파이프라인이 먼저 |
  | `DT_TOXML/FROMXML` `MAP_TOXML/FROMXML` `XML_REPLACE` | 세이브 직렬화 규약 포함 |
  | `MAP_GETKEYS` | 내부 구현은 있으나 **함수로 등록되지 않았습니다**. 키 목록을 배열로 내보내야 하므로 출력 인수 문제와 같습니다 |
  | `TIMESF` | EM 문서 279개 목록에 없습니다. 다른 계열일 가능성 |

  `LOADTEXT` 는 있지만 EM 과 인수 타입이 달라
  `LOADTEXT("dat/schema.xml")` 같은 문자열 경로 호출은 실패합니다.

  **임시 대응**: 우상단 **≡ 메뉴 → [해석 오류 무시]** 를 켜면 강제로
  실행됩니다. 다만 해당 행이 실제로 실행되는 기능은 오작동합니다
  (`emuera.config` 의 `解釈不可能な行があっても実行する` 와 같은 설정).

  대응 비용 검토는 [`docs/EMUERA_EM_GAP.md`](docs/EMUERA_EM_GAP.md) 참조.

### 화면 크기·비율 검토

`project.godot` 은 `stretch/mode=canvas_items`, `stretch/aspect=expand`,
기준 뷰포트 **720x1280**, 방향 고정 **portrait** 입니다.

`expand` 는 기준 크기를 **최소값으로 보장**합니다. 배율이
`min(화면폭/720, 화면높이/1280)` 이므로 뷰포트는 항상 720x1280 **이상**이고,
남는 쪽에만 여유가 생깁니다. 즉 UI 가 잘려나가는 경우가 없습니다.

세로로 고정된 UI 는 헤더 84 + 입력창 72 + 키패드 252 = **408px** 입니다.

| 기기 | 화면 | 배율 | 뷰포트 | 본문 높이 |
|---|---|---|---|---|
| Galaxy S26 (테스트) | 1440x3120 | 2.00 | 720x1560 | 1152 |
| 1080p 20:9 | 1080x2400 | 1.50 | 720x1600 | 1192 |
| 구형 16:9 | 720x1280 | 1.00 | 720x1280 | **872** |
| HD+ 저가 | 720x1600 | 1.00 | 720x1600 | 1192 |
| Fold 내부 (거의 정사각) | 1812x2176 | 1.70 | 1065x1280 | **872** |
| 태블릿 4:3 | 1536x2048 | 1.60 | 960x1280 | **872** |
| 21:9 | 1080x2520 | 1.50 | 720x1680 | 1272 |

최악은 세로가 가장 짧은 비율(16:9 / 4:3 / Fold)의 **872px** 이고, 기본 글자
28px 기준으로 약 20행이 보입니다. 키패드를 접으면 1124px 입니다.

가로는 최소 720 이 보장되므로 가장 넓은 고정 요소(메뉴 버튼 340,
키패드 5열)가 잘리지 않습니다.

> **떠 있는 요소를 없앴습니다.** 메뉴 버튼(≡)과 상태 표시가 본문 위에
> 떠 있어서 첫 줄이 가려졌고, 가려지는 정도가 화면비마다 달랐습니다.
> 지금은 상단 헤더 행에 넣어 흐름 레이아웃이 되었습니다.

> 실기 확인은 Galaxy S26 에서만 했습니다. 위 표는 스트레치 규칙에 따른
> **계산 결과**이고 다른 기기에서 실제로 확인한 것은 아닙니다.

### 사운드 형식 제약

Godot 이 런타임에 외부 파일로 다룰 수 있는 것은 **ogg 와 mp3** 뿐입니다.

| 형식 | 상태 |
|---|---|
| `.ogg` | ✅ |
| `.mp3` | ✅ |
| `.wav` | ❌ Godot 4 는 런타임 WAV 로더를 노출하지 않습니다 |
| `.m4a` (AAC) | ❌ 미지원. **era 게임이 흔히 쓰는 형식입니다** |

지원하지 않는 형식은 경고만 남기고 넘어갑니다(소리가 안 나는 것이 게임을
멈추는 것보다 낫습니다). `.m4a` 를 쓰는 게임은 `.ogg` 로 변환하면 됩니다.

파일은 게임 폴더의 `sound/` 를 먼저 찾고, 없으면 게임 폴더 기준 상대
경로로 찾습니다. 대소문자는 구분하지 않습니다.

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
