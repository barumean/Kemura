# Kemura

uEmuera 포크 - 최신 Android 호환성 개선 버전

uEmuera 원본: https://github.com/xerysherry/uEmuera

## 변경사항

### Android 호환성 수정

#### AndroidManifest.xml
- `android:requestLegacyExternalStorage="true"` 추가 (Android 10 레거시 스토리지 모드)
- `READ_EXTERNAL_STORAGE` 권한 추가 (API 32 이하)
- `WRITE_EXTERNAL_STORAGE` 권한 API 29 이하로 제한
- `MANAGE_EXTERNAL_STORAGE` 권한 추가 (Android 11+ 전체 스토리지 접근)
- Unity 재빌드 시 덮어쓰기 방지 (주석 제거)

#### ProjectSettings.asset
- `AndroidMinSdkVersion`: 19 → 21 (Android 5.0+)
- `AndroidTargetSdkVersion`: 0(자동) → 28 (Android 9)

#### FirstWindow.cs
- Android 런타임 권한 요청 추가 (Android 6~10)
- Android 11+ MANAGE_EXTERNAL_STORAGE 요청 로직 추가
- 하드코딩 경로 앞의 `/` 누락 수정 (`storage/emulated/0` → `/storage/emulated/0`)
- `ExternalStorageDirectory` JNI 호출로 실제 외부 스토리지 경로 동적 감지
- `DirectoryNotFoundException` 외에 `UnauthorizedAccessException` 예외 처리 추가

#### MainEntry.cs
- Android 6~9에서 앱 시작 시 스토리지 권한 초기 요청 추가

#### Utils.cs (NormalizePath 버그 수정)
- 절대 경로(`/`로 시작)의 앞 슬래시가 제거되던 버그 수정
- `/storage/emulated/0/emuera` 같은 경로가 올바르게 처리됨

#### AndroidVersionHelper.cs (신규)
- Android SDK 버전 확인 유틸리티
- MANAGE_EXTERNAL_STORAGE 권한 확인/요청
- 외부 스토리지 디렉터리 경로 조회

## 게임 파일 배치 방법

다음 경로 중 하나에 emuera 게임 폴더를 배치하세요:

- `/storage/emulated/0/emuera/` (Android 9 이하, 또는 권한 허용 시)
- 앱 내부 데이터 디렉터리 (항상 접근 가능)

각 게임 폴더에는 `emuera.config` 파일 또는 `ERB/` 폴더가 있어야 합니다.

## 빌드 환경

- Unity 2019.4.2f1
- Target SDK: 28 (Android 9)
- Min SDK: 21 (Android 5.0)
