# 무쌍 오로치 2 얼티메이트 한국어 패치

PS3 일본판 **無双OROCHI2 Ultimate / BLJM61084**용 비공식 한국어 패치입니다. 직접 준비한 복호화 ISO 또는 RPCS3 폴더형 게임에 적용합니다. 게임 원본·ISO·세이브는 배포하지 않습니다.

## 최신 배포

**v20261010 — 2026-10-10 정식 릴리즈**

[패치 ZIP 다운로드](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases/download/v20261010/WO3U_KR_v20261010.zip) · [릴리즈 설명](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases/tag/v20261010) · [문서 사이트](https://kimjh-eclipse.github.io/musou-orochi-2-ultimate-kor/)

이번 릴리즈는 카페 제보를 반영한 번역 수정판입니다. 남아 있던 일본어 문장을 번역하고, 전투 메시지 창에서 잘리던 대사를 줄였으며, 무장·지명 표기를 정리했습니다. 게임 업데이트 1.06을 설치한 사용자를 위한 업데이트 패처도 함께 들어 있습니다.

> 패처는 위 최신 릴리즈 ZIP에서 받으세요. `tools/`는 이전 3파일 구현의 참고 소스로 보존하며 최신 배포물과 같지 않습니다.

## 설치 요약

1. RPCS3를 완전히 종료하고 ISO를 마운트했다면 해제합니다.
2. ZIP을 풀고 `WO3U_ISO_QuickPatch.exe`를 실행합니다.
3. **실제로 실행하는 ISO 또는 게임 폴더**를 지정해 상태를 검사합니다.
4. `.wo3u-backup` 경로를 확인하고 패치를 적용합니다. 이전 버전 사용자는 그때 만든 백업을 지정합니다.
5. 로그에서 적용 후 **5개 파일 전체 해시 검증 성공**을 확인합니다.
6. 게임 업데이트 1.06을 설치했다면 `WO3U_Update106_KR_Patch.exe`도 실행해 RPCS3 루트 또는 `dev_hdd0\game\BLJM61084`를 지정합니다.
7. 새로 부팅하고 게임 내부 세이브를 불러옵니다.

세이브나 게임 등록을 지울 필요는 없습니다. 복구·갱신에 쓰는 `.wo3u-backup`은 보관하세요.

[자세한 설치·업데이트·복구](docs/install.md) · [알려진 문제](docs/known-issues.md)

## 변경 범위

| 파일 | 역할 |
|---|---|
| `PS3_GAME/USRDIR/EBOOT.BIN` | 실행 파일 내 UI 문자열 |
| `PS3_GAME/USRDIR/LINKDATA.IDX` | 수정 리소스의 저장 크기 등 인덱스 |
| `PS3_GAME/USRDIR/LINKDATA.BIN` | 번역 텍스트, 한글 폰트, 메뉴 이미지 |
| `PS3_GAME/PARAM.SFO` | 게임 목록·기본 창 제목용 한국어 제목 |
| `PS3_GAME/ICON0.PNG` | 게임 목록용 한국어 아이콘 |
| `dev_hdd0/game/BLJM61084/USRDIR/EBOOT.BIN` | 업데이트 1.06 실행 파일의 UI 문자열 (업데이트 패처) |

게임 파일 크기와 ISO 배치를 유지하는 차이 구간 패치입니다. 음성·영상·세이브·RPCS3 설정은 변경하지 않습니다.

업데이트 1.06을 설치하면 게임은 디스크 실행 파일 대신 업데이트 실행 파일을 씁니다. 그래서 업데이트 사용자는 본편 패치만으로는 실행 파일 안의 메뉴 문구가 일본어로 남습니다. 업데이트 패처는 디스크 쪽 번역을 옮기고 업데이트가 추가한 문장(폭주 던전, 던전 내용 변경·요석, 4회차 이후 전생 등)을 번역합니다.

대사·이벤트·UI, 무기 속성·소재·BGM, 던전 임무·선녀 의뢰, 공통 메뉴 이미지 등에 번역이 반영되어 있습니다. **모든 화면의 완역·실행 검증을 뜻하지는 않습니다.** [알려진 문제](docs/known-issues.md)를 확인하세요.

## 파일 확인

```text
WO3U_KR_v20261010.zip  27,139,756 bytes
SHA-256 BB1551CC3AE3D2A71A1DAF6F6C14C00D334ACF2C042ECC15CABAD3CDED257B5B
```

```powershell
Get-FileHash .\WO3U_KR_v20261010.zip -Algorithm SHA256
```

[배포물 및 적용 전후 전체 해시](docs/hashes.md)

## 기술 문서

문서의 바이너리 수치는 이 일본판과 현재 빌드에서 확인한 값입니다. 다른 리전·플랫폼 전체에 일반화하지 않습니다.

- [아카이브·압축·LX 문자열 구조](docs/formats.md): IDX 필드, 압축 블록, 상대 포인터, 문자열 풀 재배치.
- [한글 인코딩과 폰트](docs/font-text.md): CP932 대체 코드, 2048×4096 아틀라스, 32×32 글리프, 이름의 별표·특수문자 원인.
- [메뉴 이미지 처리](docs/images.md): PS3 G1T 치수·채널 순서, BC2/BC3 블록, 이미지 누락 추적과 잘림 검증.
- [실행 파일·제목·아이콘](docs/metadata.md): EBOOT 문자열, 업데이트 1.06 실행 파일, SFO 필드, 고정 길이 PNG.
- [패처·ISO·백업 설계](docs/patcher.md): 팩 형식, 다중 extent, 버전 갱신, 복구 및 검증 한계.
- [빌드·검증·조사 범위](docs/validation.md): 재현 절차, 증거 수준, 임무 31개라는 수치의 정확한 의미.
- [변경 이력](CHANGELOG.md).

`docs/`는 GitBook용 `SUMMARY.md`와 GitHub Pages용 Docsify 탐색을 함께 제공합니다. 공개 `tools/`만으로 최신 번역·이미지 전체를 재빌드할 수 있는 상태는 아닙니다. [공개 소스 범위](docs/validation.md)를 먼저 확인하세요.

## 오류 제보

[Issues](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/issues)에 화면 캡처, 진입 경로, 릴리즈 버전, 업데이트 1.06 설치 여부, 상태 검사 로그, RPCS3 버전을 올려 주세요. 게임 원본·ISO·개인 세이브는 첨부하지 마세요. 이름 오류는 머리 위 표시와 대화창 표시를 각각 알려 주시면 좋습니다.

패치가 도움이 되셨다면 저장소에서 스타(★)를 눌러 주세요. 앞으로 작업을 이어 가는 데 큰 힘이 됩니다.

코드의 이용 조건은 [LICENSE](LICENSE)를 확인하세요. 원작 게임·상표·음성·이미지의 권리는 각 권리자에게 있습니다.
