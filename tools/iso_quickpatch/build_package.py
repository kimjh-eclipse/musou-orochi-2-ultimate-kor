#!/usr/bin/env python3
"""WO3U 한국어 빠른 패처 배포 패키지 조립.

순서: (선택) build_range_pack.py 재실행 → verify_range_pack.py 통과 확인 → csc 로 EXE 리빌드
→ README/SHA256SUMS 생성 → ZIP → ZIP 재추출 대조.

  python build_package.py            # 기존 팩으로 EXE 리빌드 + 패키지
  python build_package.py --rebuild  # 팩도 다시 만든다 (hdd0 상태가 바뀐 뒤)
"""
from __future__ import annotations

import hashlib
import io
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
CSC = Path(r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe")
PACK = HERE / "WO3U_ISO_ranges.bin"
MANIFEST = HERE / "WO3U_ISO_ranges.manifest.json"
SOURCE = HERE / "WO3UIsoQuickPatch.cs"
EXE = HERE / "WO3U_ISO_QuickPatch.exe"

README_TEMPLATE = """무쌍 오로치 2 얼티밋 (Warriors Orochi 3 Ultimate) 한국어 패치 {version}
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
   - RPCS3 루트 폴더 (dev_hdd0\\disc\\BLJM61084 자동 탐색)
   - BLJM61084 게임 폴더, PS3_GAME 폴더, 또는 USRDIR 폴더
4. [폴더 게임 상태 검사]로 상태를 확인합니다.
5. 주의사항에 동의한 뒤 [폴더 게임에 직접 패치]를 누릅니다.

USRDIR 의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 이 수정되며,
게임 폴더 바깥에 BLJM61084.wo3u-backup 백업이 생깁니다.

[C. 명령줄]
  WO3U_ISO_QuickPatch.exe --iso "D:\\WO3U.iso" --verify-only
  WO3U_ISO_QuickPatch.exe --iso "D:\\WO3U.iso" --yes
  WO3U_ISO_QuickPatch.exe --iso "D:\\WO3U.iso" --restore --backup "D:\\WO3U.iso.wo3u-backup"
  WO3U_ISO_QuickPatch.exe --folder "C:\\RPCS3" --verify-only
  WO3U_ISO_QuickPatch.exe --folder "C:\\RPCS3\\dev_hdd0\\disc\\BLJM61084" --yes

주의
- 패치 후 RPCS3 저장 상태(Save State)로 이어하지 마세요. 저장 상태는 패치 전
  폰트와 메모리를 되살릴 수 있습니다. 정상 부팅 후 게임 내부 세이브를 사용하세요.
- 영문판(NPUB31505 등)이나 다른 리전은 지원하지 않습니다.
- 세이브·savestate·PPU/SPU/셰이더 캐시는 건드리지 않습니다.

패치 후 파일 SHA-256 (PS3_GAME\\USRDIR)
{hash_lines}

원본(일본판) 파일 SHA-256
{source_lines}
"""


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(8 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def run(*args, cwd=None):
    subprocess.run([str(a) for a in args], check=True, cwd=cwd)


def main() -> None:
    rebuild = "--rebuild" in sys.argv
    if rebuild or not PACK.exists():
        run(sys.executable, HERE / "build_range_pack.py")
    run(sys.executable, HERE / "verify_range_pack.py", PACK)
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    version = manifest["version"]

    # EXE 리빌드 (팩을 리소스로 내장)
    run(CSC, "/nologo", "/target:winexe", "/optimize+", "/platform:anycpu",
        "/out:" + str(EXE), "/resource:" + str(PACK) + ",WO3U_ISO_ranges.bin", SOURCE)
    print("EXE", digest(EXE), EXE.stat().st_size)

    rel = ROOT / ("release_wo3u_" + version.lstrip("v"))
    pkg = rel / ("WO3U_KR_" + version)
    if pkg.exists():
        shutil.rmtree(pkg)
    pkg.mkdir(parents=True)
    shutil.copy2(EXE, pkg / EXE.name)

    names = {"EBOOT.BIN": "EBOOT.BIN   ", "LINKDATA.IDX": "LINKDATA.IDX", "LINKDATA.BIN": "LINKDATA.BIN"}
    hash_lines = "\n".join(f"{names[f['iso_path'].rsplit('/',1)[-1]]}  {f['target_sha256']}" for f in manifest["files"])
    source_lines = "\n".join(f"{names[f['iso_path'].rsplit('/',1)[-1]]}  {f['source_sha256']}" for f in manifest["files"])
    readme = README_TEMPLATE.format(version=version, hash_lines=hash_lines, source_lines=source_lines)
    (pkg / "README_사용법.txt").write_bytes(b"\xef\xbb\xbf" + readme.replace("\n", "\r\n").encode("utf-8"))

    files = sorted((p for p in pkg.rglob("*") if p.is_file()), key=lambda p: p.relative_to(pkg).as_posix())
    (pkg / "SHA256SUMS.txt").write_text(
        "\n".join(f"{digest(p)}  {p.relative_to(pkg).as_posix()}" for p in files) + "\n", encoding="utf-8")

    zip_path = rel / (pkg.name + ".zip")
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for p in sorted(pkg.rglob("*")):
            if p.is_file():
                archive.write(p, p.relative_to(rel))
    check = rel / "zip_verify"
    if check.exists():
        shutil.rmtree(check)
    with zipfile.ZipFile(zip_path) as archive:
        archive.extractall(check)
    for p in pkg.rglob("*"):
        if p.is_file():
            other = check / pkg.name / p.relative_to(pkg)
            if not other.is_file() or digest(other) != digest(p):
                raise AssertionError(f"ZIP 재추출 불일치: {p.relative_to(pkg)}")
    shutil.rmtree(check)
    print(f"package={pkg}")
    print(f"zip={zip_path.name} size={zip_path.stat().st_size:,} sha256={digest(zip_path)}")


if __name__ == "__main__":
    main()
