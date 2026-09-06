# 설치 안내

## 적용 대상

PS3 『無双OROCHI2 Ultimate』 **일본판** — 게임 ID `BLJM61084`

영문판(`NPUB31505` 등)이나 다른 리전판에는 적용할 수 없습니다. 영문판은 1바이트 폰트만 참조해 2바이트 글리프를 표시하지 못합니다.

> **원본 게임 데이터는 직접 준비해야 합니다.**
> 이 저장소와 배포물에는 게임 파일이나 ISO가 들어 있지 않으며, 제공하지도 않습니다.

## 설치 방식 두 가지 — 하나만 고르세요

패처는 하나이고 대상만 다릅니다. 두 방식이 만드는 최종 데이터는 같습니다.

| 방식 | 대상 | 수정되는 것 |
|---|---|---|
| **A. ISO 모드** | 복호화된 ISO 파일 | ISO 안의 `EBOOT.BIN` / `LINKDATA.IDX` / `LINKDATA.BIN` 구간 |
| **B. 폴더 모드** | RPCS3 폴더형 게임, 추출 폴더 | `PS3_GAME\USRDIR` 의 같은 세 파일 |

> **같은 원본에 두 방식을 겹쳐 적용하지 마세요.** 이미 패치된 대상은 패처가 "패치됨"으로 판정하고 다시 쓰지 않습니다.

## 1. 준비

1. **RPCS3 를 완전히 종료합니다.** 실행 중이면 패처가 시작하지 않습니다. ISO 를 마운트했다면 해제합니다.
2. 원본 파일이 다음과 같아야 합니다. (`PS3_GAME\USRDIR`)

   | 파일 | 크기 | 원본 SHA-256 |
   |---|---:|---|
   | `EBOOT.BIN` | 14,167,896 | `E59780C5…FABF66` |
   | `LINKDATA.IDX` | 1,198,592 | `11872DF3…8558B` |
   | `LINKDATA.BIN` | 9,259,816,960 | `E8A491D3…6EDAA9` |

   패처가 **상태 검사**에서 이 해시를 직접 확인하므로 미리 계산할 필요는 없습니다.
3. 세이브 데이터를 지울 필요는 없습니다. 만일을 대비해 백업을 권장합니다.

## 2. 내려받기

저장소 루트의 [WO3U_ISO_QuickPatch.exe](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/raw/main/WO3U_ISO_QuickPatch.exe) 를 받습니다.
정식 배포가 시작되면 [Releases](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases) 의 ZIP 을 쓰세요.

```
WO3U_ISO_QuickPatch.exe   35,891,712 바이트   (v20260906b)
SHA-256: 443523F3A4B34D51FF041E2F2692DA7FBF7ABEAB0CFCF0F7F9036B67FFE009A2
```

```powershell
Get-FileHash .\WO3U_ISO_QuickPatch.exe -Algorithm SHA256
```

실행 파일 하나에 패치 데이터가 들어 있어 다른 파일은 필요 없습니다. .NET Framework 4.x 가 있는 Windows 에서 실행됩니다.

## 3. 방법 A — 복호화 ISO

1. `WO3U_ISO_QuickPatch.exe` 를 실행합니다.
2. **A. 복호화 ISO 파일** 칸에 ISO 를 지정합니다. 창에 파일을 끌어다 놓아도 됩니다.
3. 백업 파일 경로는 기본으로 `<ISO 이름>.iso.wo3u-backup` 이 채워집니다. 필요하면 바꿉니다.
4. **ISO 상태 검사**를 눌러 세 파일이 모두 `원본`으로 나오는지 확인합니다. 9 GB 를 읽으므로 수십 초 걸립니다.
5. 주의사항 확인란을 체크하고 **ISO 에 한국어 패치 적용**을 누릅니다.
6. 로그에 `한국어 패치 적용 및 최종 해시 검증 3/3 완료`가 나오면 끝입니다.

ISO 파일 자체가 수정되며 크기는 바뀌지 않습니다. RPCS3 에 이미 등록된 ISO 라면 그대로 부팅하면 됩니다.

> **`.wo3u-backup` 파일을 삭제하지 마세요.** 원본 복구와 다음 버전 갱신에 이 파일만 씁니다.
> 다른 버전으로 갱신할 때는 새 패처에 같은 백업 파일을 지정하면 원본으로 되돌린 뒤 새 버전을 적용합니다.

## 4. 방법 B — RPCS3 폴더형 게임 / 추출 폴더

1. `WO3U_ISO_QuickPatch.exe` 를 실행합니다.
2. **B. RPCS3 / 폴더형 게임** 칸에 다음 중 하나를 지정합니다.
   - RPCS3 루트 폴더 (`dev_hdd0\disc\BLJM61084` 를 자동으로 찾습니다)
   - `BLJM61084` 게임 폴더, `PS3_GAME` 폴더, 또는 `USRDIR` 폴더
3. 백업 파일 경로는 기본으로 게임 폴더 바깥의 `BLJM61084.wo3u-backup` 이 채워집니다.
4. **폴더 게임 상태 검사** → 주의사항 동의 → **폴더 게임에 직접 패치**.

`USRDIR` 의 세 파일이 제자리에서 수정됩니다. 다른 파일(`bgm`, `movie2d`, `PARAM.SFO` 등)은 건드리지 않습니다.

## 5. 명령줄

GUI 없이도 같은 기능을 씁니다. `--yes` 가 없으면 확인을 묻고, `--no-pause` 가 없으면 끝에 Enter 를 기다립니다.

```
WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --verify-only
WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --yes
WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --restore --backup "D:\WO3U.iso.wo3u-backup"
WO3U_ISO_QuickPatch.exe --folder "C:\RPCS3" --verify-only
WO3U_ISO_QuickPatch.exe --folder "C:\RPCS3\dev_hdd0\disc\BLJM61084" --yes
```

## 6. 설치 확인

**상태 검사**(또는 `--verify-only`)에서 세 파일이 모두 `패치됨`으로 나오면 정상입니다.

| 파일 | 패치 후 SHA-256 (v20260906b) |
|---|---|
| `EBOOT.BIN` | `BCC84BDA4C20C984021CB5432190120209CDE960053858A2E2377BF931EC0612` |
| `LINKDATA.IDX` | `D7DDDEC52C1771808CD804CC60D19CBBFC2BD20600FCDF14DA38362A744DD85A` |
| `LINKDATA.BIN` | `CEE8EA70120B012301A65C21280401D7E776A6D1BCCFA39AA92BCBC75ACC2FFB` |

백업 파일이 있으면 변경 구간만 비교해 몇 초 안에 판정하고, 없으면 세 파일 전체 해시를 계산합니다.

## 7. 되돌리기

**ISO 원본 복구** / **폴더 게임 원본 복구** 버튼(또는 `--restore`)에 패치할 때 만든 백업 파일을 지정합니다.
백업의 구간을 되쓴 뒤 세 파일이 원본 해시와 일치하는지 확인합니다. 백업은 재적용에 대비해 남겨 둡니다.

> 게임 폴더를 통째로 지우지 마세요. 백업 복구만으로 충분합니다.

## 8. 실행 후 주의

- **RPCS3 저장 상태(Save State)로 이어하지 마세요.** 저장 상태는 패치 전 폰트와 메모리를 되살릴 수 있습니다. 정상 부팅 후 게임 내부 세이브를 쓰세요.
- 세이브·savestate·PPU/SPU/셰이더 캐시는 패처가 건드리지 않습니다. 화면 표시가 이상하면 RPCS3 게임 우클릭 → **Remove** → **Remove Shader/SPU Cache** 를 시도해 보세요.

## 오류 제보

[Issues](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/issues)에 다음을 함께 올려 주시면 확인이 빠릅니다.

1. 화면 캡처와 그 화면까지 들어간 경로
2. 사용한 패처 버전 (창 제목에 표시)
3. **상태 검사** 로그
