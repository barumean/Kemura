# 폰트 설정

이 폴더에 폰트 파일을 배치해야 한국어/일본어 텍스트가 올바르게 표시됩니다.

## 권장 폰트 (한국어 게임용)

### 옵션 1: Noto Sans KR (권장)
Google Fonts에서 무료 다운로드:
https://fonts.google.com/noto/specimen/Noto+Sans+KR

다운로드 후 `NotoSansKR-Regular.ttf` 를 이 폴더에 배치.

### 옵션 2: 나눔고딕코딩 (개발자용 모노스페이스)
https://github.com/naver/nanumfont/releases

다운로드 후 `NanumGothicCoding.ttf` 를 이 폴더에 배치.

### 옵션 3: MS Gothic (일본어 게임용, Windows에서 복사)
`C:\Windows\Fonts\msgothic.ttc` 를 이 폴더에 복사.

## 탐색 우선순위
FontUtils.cs가 다음 순서로 파일을 탐색합니다:
1. NotoSansKR-Regular.ttf
2. NotoSansKR-Regular.otf
3. NanumGothicCoding.ttf
4. NanumGothic.ttf
5. NotoSansJP-Regular.ttf
6. msgothic.ttc / msgothic.ttf
7. NotoSansCJKkr-Regular.otf
8. NotoSansMono-Regular.ttf

## Godot 에디터에서 폰트 임포트
폰트 파일을 Fonts/ 에 배치하면 Godot 에디터가 자동으로 임포트합니다.
별도 설정 없이 `res://Fonts/파일명` 경로로 접근 가능합니다.
