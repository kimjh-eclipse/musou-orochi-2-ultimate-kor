무쌍 오로치 2 얼티밋 (Warriors Orochi 3 Ultimate) 한국어 패치 v20260906b
대상: PS3 일본판 BLJM61084 (無双OROCHI2 Ultimate)

두 설치 방식 중 하나만 사용하세요. 같은 대상에 두 방식을 중복 적용하지 마세요.

[A. 복호화 ISO]
1. RPCS3 와 ISO 마운트 프로그램을 종료합니다.
2. WO3U_ISO_QuickPatch.exe 를 실행합니다.
3. 복호화된 일본판 ISO 를 선택하고 [ISO 상태 검사]를 실행합니다.
4. 주의사항에 동의한 뒤 [ISO 에 한국어 패치 적용]을 누릅니다.

ISO 자체가 수정되며, 같은 위치에 <ISO 이름>.iso.wo3u-backup 백업이 생깁니다.
이 백업 파일로만 원본 복구와 다음 버전 갱신이 가능하므로 삭제하지 마세요.
ISO 크기는 바뀌지 않습니다 (변경 구간 약 36 MB 만 제자리 기록).

[B. RPCS3 폴더형 게임 / 추출 폴더 — 직접 패치]
1. RPCS3 를 완전히 종료합니다.
2. WO3U_ISO_QuickPatch.exe 를 실행합니다.
3. `RPCS3 / 폴더형 게임` 경로에서 다음 중 하나를 선택합니다.
   - RPCS3 루트 폴더 (dev_hdd0\disc\BLJM61084 자동 탐색)
   - BLJM61084 게임 폴더, PS3_GAME 폴더, 또는 USRDIR 폴더
4. [폴더 게임 상태 검사]로 상태를 확인합니다.
5. 주의사항에 동의한 뒤 [폴더 게임에 직접 패치]를 누릅니다.

USRDIR 의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 이 수정되며,
게임 폴더 바깥에 BLJM61084.wo3u-backup 백업이 생깁니다.

[C. 명령줄]
  WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --verify-only
  WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --yes
  WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --restore --backup "D:\WO3U.iso.wo3u-backup"
  WO3U_ISO_QuickPatch.exe --folder "C:\RPCS3" --verify-only
  WO3U_ISO_QuickPatch.exe --folder "C:\RPCS3\dev_hdd0\disc\BLJM61084" --yes

주의
- 패치 후 RPCS3 저장 상태(Save State)로 이어하지 마세요. 저장 상태는 패치 전
  폰트와 메모리를 되살릴 수 있습니다. 정상 부팅 후 게임 내부 세이브를 사용하세요.
- 영문판(NPUB31505 등)이나 다른 리전은 지원하지 않습니다.
- 세이브·savestate·PPU/SPU/셰이더 캐시는 건드리지 않습니다.

패치 후 파일 SHA-256 (PS3_GAME\USRDIR)
EBOOT.BIN     BCC84BDA4C20C984021CB5432190120209CDE960053858A2E2377BF931EC0612
LINKDATA.IDX  D7DDDEC52C1771808CD804CC60D19CBBFC2BD20600FCDF14DA38362A744DD85A
LINKDATA.BIN  CEE8EA70120B012301A65C21280401D7E776A6D1BCCFA39AA92BCBC75ACC2FFB

원본(일본판) 파일 SHA-256
EBOOT.BIN     E59780C5DC9C9F27B1CECEF4C3D71AACC9E7C55B86323C7F25BEF54BFFFABF66
LINKDATA.IDX  11872DF3C8E1026FBFBD9A4A508D2E20B3926DCD45F011C5F609168629A8558B
LINKDATA.BIN  E8A491D3FA5B06154B98A56745DA598423555E32DF17DE73D24A4F2E0A6EDAA9
