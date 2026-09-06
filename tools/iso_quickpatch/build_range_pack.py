#!/usr/bin/env python3
"""WO3U(BLJM61084) 한국어 패치용 ISO 범위 팩 생성기.

원본 = 일본판 원본 ISO(읽기 전용) 안의 ISO9660 파일 extent,
대상 = 현재 hdd0 canonical 폴더형 게임(USRDIR)의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN.

세 파일의 크기는 원본과 대상이 같아야 한다(제자리 설치가 전제).  바이트 단위로
차이를 찾아 MERGE_GAP 이내의 인접 차이를 합치고, 대상 쪽 바이트만 팩에 담는다.

팩 형식 (little-endian):
  magic "WO3URNG1" | u32 format=1 | u16 verlen + utf8 version | u32 file_count
  file: u16 pathlen + utf8 iso_path | u64 size | 32B source_sha256 | 32B target_sha256
        | u32 range_count | range: u64 offset | u32 length | bytes
"""
from __future__ import annotations

import hashlib
import io
import json
import os
import struct
import sys
import time
from pathlib import Path

import numpy as np

MAGIC = b"WO3URNG1"
FORMAT_VERSION = 1
MERGE_GAP = 16
CHUNK = 16 * 1024 * 1024
SECTOR = 2048

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
SOURCE_ISO = ROOT.parent / "Musou Orochi 2 Ultimate (Japan).iso"
TARGET_DIR = Path(r"C:\Emul\PS3\rpcs3-v0.0.27-14986-db7f84f9_win64\dev_hdd0\disc\BLJM61084\PS3_GAME\USRDIR")
OUTPUT = HERE / "WO3U_ISO_ranges.bin"
MANIFEST = HERE / "WO3U_ISO_ranges.manifest.json"

# 릴리즈 표기 버전. hdd0 상태가 바뀌면(예: v26 설치) 갱신 후 재실행.
VERSION_TEXT = "v20260906"
INSTALL_AUTHORITY = "jp_ui_v26/install_report.json"  # 팩 생성 시점(22:52) hdd0 = v26. v27(entry40) 은 미포함

FILES = (
    "PS3_GAME/USRDIR/EBOOT.BIN",
    "PS3_GAME/USRDIR/LINKDATA.IDX",
    "PS3_GAME/USRDIR/LINKDATA.BIN",
)


# ---------------------------------------------------------------- ISO9660
def _rec(d: bytes, o: int):
    length = d[o]
    if length == 0:
        return None
    nl = d[o + 32]
    raw = d[o + 33:o + 33 + nl]
    if nl == 1 and raw in (b"\x00", b"\x01"):
        name = "." if raw == b"\x00" else ".."
    else:
        name = raw.split(b";")[0].decode("ascii", "replace")
    return {
        "name": name,
        "lba": struct.unpack_from("<I", d, o + 2)[0],
        "size": struct.unpack_from("<I", d, o + 10)[0],
        "flags": d[o + 25],
        "fu": d[o + 26],
        "gap": d[o + 27],
        "len": length,
    }


def _readdir(f, rec):
    f.seek(rec["lba"] * SECTOR)
    d = f.read(rec["size"])
    out, p = [], 0
    while p < len(d):
        if d[p] == 0:
            p = (p // SECTOR + 1) * SECTOR
            continue
        r = _rec(d, p)
        p += r["len"]
        if r["name"] not in (".", ".."):
            out.append(r)
    return out


def iso_extents(f, path: str) -> list[tuple[int, int]]:
    """ISO9660 PVD 기준으로 파일의 (절대 오프셋, 크기) extent 목록을 돌려준다."""
    root = None
    for s in range(16, 64):
        f.seek(s * SECTOR)
        d = f.read(SECTOR)
        if d[1:6] != b"CD001":
            continue
        if d[0] == 1:
            root = _rec(d, 156)
            break
        if d[0] == 255:
            break
    if root is None:
        raise ValueError("ISO9660 PVD 없음")
    parts = path.split("/")
    cur = root
    for part in parts[:-1]:
        m = [e for e in _readdir(f, cur) if e["name"].upper() == part.upper() and e["flags"] & 2]
        if not m:
            raise FileNotFoundError(path)
        cur = m[0]
    recs = [e for e in _readdir(f, cur) if e["name"].upper() == parts[-1].upper() and not e["flags"] & 2]
    if not recs:
        raise FileNotFoundError(path)
    for r in recs:
        if r["fu"] or r["gap"]:
            raise ValueError("인터리브 파일 미지원: " + path)
    return [(r["lba"] * SECTOR, r["size"]) for r in recs]


class ExtentReader:
    """여러 extent 를 하나의 연속 스트림처럼 읽는다."""

    def __init__(self, f, extents):
        self.f = f
        self.extents = extents
        self.size = sum(s for _, s in extents)

    def read_at(self, logical: int, n: int) -> bytes:
        out = bytearray()
        base = 0
        for off, size in self.extents:
            if logical >= base + size:
                base += size
                continue
            inside = logical - base
            take = min(n - len(out), size - inside)
            self.f.seek(off + inside)
            chunk = self.f.read(take)
            if len(chunk) != take:
                raise EOFError("ISO short read")
            out += chunk
            logical += take
            base += size
            if len(out) == n:
                break
        if len(out) != n:
            raise EOFError("extent 범위 초과")
        return bytes(out)


# ---------------------------------------------------------------- diff
def diff_ranges(src: ExtentReader, dst_path: Path):
    size = dst_path.stat().st_size
    if size != src.size:
        raise ValueError(f"크기 불일치: ISO {src.size} vs 대상 {size} ({dst_path})")
    positions = []
    src_hash, dst_hash = hashlib.sha256(), hashlib.sha256()
    with dst_path.open("rb", buffering=0) as right:
        off = 0
        while off < size:
            n = min(CHUNK, size - off)
            a = src.read_at(off, n)
            b = right.read(n)
            if len(b) != n:
                raise EOFError("대상 short read")
            src_hash.update(a)
            dst_hash.update(b)
            if a != b:
                changed = np.flatnonzero(np.frombuffer(a, np.uint8) != np.frombuffer(b, np.uint8))
                positions.append(changed.astype(np.int64) + off)
            off += n
    if positions:
        allp = np.concatenate(positions)
        cuts = np.flatnonzero(np.diff(allp) > MERGE_GAP + 1)
        starts = np.r_[0, cuts + 1]
        ends = np.r_[cuts, len(allp) - 1]
        ranges = [(int(allp[s]), int(allp[e] - allp[s] + 1)) for s, e in zip(starts, ends)]
    else:
        ranges = []
    return ranges, src_hash.hexdigest().upper(), dst_hash.hexdigest().upper(), size


def main() -> None:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    t0 = time.time()
    if not SOURCE_ISO.is_file():
        raise FileNotFoundError(SOURCE_ISO)
    manifest = {
        "version": VERSION_TEXT,
        "format": FORMAT_VERSION,
        "source_iso": str(SOURCE_ISO),
        "source_iso_size": SOURCE_ISO.stat().st_size,
        "target_dir": str(TARGET_DIR),
        "install_authority": INSTALL_AUTHORITY,
        "merge_gap": MERGE_GAP,
        "files": [],
    }
    tmp = OUTPUT.with_suffix(".bin.tmp")
    total_ranges = total_payload = 0
    with SOURCE_ISO.open("rb", buffering=0) as iso, tmp.open("wb", buffering=1 << 20) as out:
        out.write(MAGIC)
        out.write(struct.pack("<I", FORMAT_VERSION))
        ver = VERSION_TEXT.encode("utf-8")
        out.write(struct.pack("<H", len(ver)) + ver)
        out.write(struct.pack("<I", len(FILES)))
        for iso_path in FILES:
            name = iso_path.rsplit("/", 1)[-1]
            extents = iso_extents(iso, iso_path)
            reader = ExtentReader(iso, extents)
            target = TARGET_DIR / name
            print(f"[{name}] extents={len(extents)} size={reader.size:,} diff 중...", flush=True)
            ranges, shash, thash, size = diff_ranges(reader, target)
            if shash == thash:
                raise ValueError(f"{name}: 원본과 대상이 동일합니다. 대상 폴더 상태를 확인하세요.")
            enc = iso_path.encode("utf-8")
            out.write(struct.pack("<H", len(enc)) + enc)
            out.write(struct.pack("<Q", size))
            out.write(bytes.fromhex(shash))
            out.write(bytes.fromhex(thash))
            out.write(struct.pack("<I", len(ranges)))
            payload = 0
            with target.open("rb", buffering=0) as tf:
                for off, ln in ranges:
                    tf.seek(off)
                    data = tf.read(ln)
                    if len(data) != ln:
                        raise IOError("대상 short read")
                    out.write(struct.pack("<QI", off, ln))
                    out.write(data)
                    payload += ln
            total_ranges += len(ranges)
            total_payload += payload
            manifest["files"].append({
                "iso_path": iso_path,
                "size": size,
                "iso_extents": [{"offset": o, "size": s} for o, s in extents],
                "source_sha256": shash,
                "target_sha256": thash,
                "range_count": len(ranges),
                "payload_bytes": payload,
            })
            print(f"    ranges={len(ranges):,} payload={payload:,} src={shash[:16]}… dst={thash[:16]}…  {time.time()-t0:.0f}s", flush=True)
        out.flush()
        os.fsync(out.fileno())
    tmp.replace(OUTPUT)
    pack_hash = hashlib.sha256(OUTPUT.read_bytes()).hexdigest().upper()
    manifest.update({"total_ranges": total_ranges, "total_payload": total_payload,
                     "pack_size": OUTPUT.stat().st_size, "pack_sha256": pack_hash})
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"TOTAL ranges={total_ranges:,} payload={total_payload:,} pack={OUTPUT.stat().st_size:,} sha256={pack_hash}")
    print(f"manifest: {MANIFEST}  ({time.time()-t0:.0f}s)")


if __name__ == "__main__":
    main()
