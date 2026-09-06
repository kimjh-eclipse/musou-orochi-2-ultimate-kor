#!/usr/bin/env python3
"""범위 팩 독립 검증기.

팩을 파싱해 원본 ISO 의 각 파일 extent 를 순서대로 읽으면서 범위 데이터를 덧씌운
결과의 SHA-256 이 팩에 기록된 target_sha256 과 같은지, 덧씌우기 전 스트림이
source_sha256 과 같은지 확인한다.  ISO 는 읽기 전용으로만 연다.

  python verify_range_pack.py [팩 경로] [ISO 경로]
"""
from __future__ import annotations

import hashlib
import io
import struct
import sys
import time
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_range_pack import ExtentReader, iso_extents, MAGIC, OUTPUT, SOURCE_ISO  # noqa: E402

CHUNK = 16 * 1024 * 1024


def parse_pack(path: Path):
    data = path.read_bytes()
    mv = memoryview(data)
    p = 0
    if bytes(mv[0:8]) != MAGIC:
        raise ValueError("magic 불일치")
    p = 8
    fmt, = struct.unpack_from("<I", data, p); p += 4
    vl, = struct.unpack_from("<H", data, p); p += 2
    version = bytes(mv[p:p + vl]).decode("utf-8"); p += vl
    count, = struct.unpack_from("<I", data, p); p += 4
    files = []
    for _ in range(count):
        pl, = struct.unpack_from("<H", data, p); p += 2
        iso_path = bytes(mv[p:p + pl]).decode("utf-8"); p += pl
        size, = struct.unpack_from("<Q", data, p); p += 8
        shash = bytes(mv[p:p + 32]).hex().upper(); p += 32
        thash = bytes(mv[p:p + 32]).hex().upper(); p += 32
        rc, = struct.unpack_from("<I", data, p); p += 4
        ranges = []
        last_end = -1
        for _ in range(rc):
            off, ln = struct.unpack_from("<QI", data, p); p += 12
            if off < last_end:
                raise ValueError("범위가 정렬되지 않았거나 겹칩니다")
            if off + ln > size:
                raise ValueError("범위가 파일 크기를 벗어납니다")
            ranges.append((off, mv[p:p + ln])); p += ln
            last_end = off + ln
        files.append((iso_path, size, shash, thash, ranges))
    if p != len(data):
        raise ValueError("팩 끝에 알 수 없는 데이터")
    return fmt, version, files


def main() -> None:
    pack = Path(sys.argv[1]) if len(sys.argv) > 1 else OUTPUT
    iso_path = Path(sys.argv[2]) if len(sys.argv) > 2 else SOURCE_ISO
    fmt, version, files = parse_pack(pack)
    print(f"pack={pack.name} format={fmt} version={version} files={len(files)} sha256={hashlib.sha256(pack.read_bytes()).hexdigest().upper()}")
    ok = True
    t0 = time.time()
    with iso_path.open("rb", buffering=0) as iso:
        for name, size, shash, thash, ranges in files:
            reader = ExtentReader(iso, iso_extents(iso, name))
            if reader.size != size:
                print(f"[FAIL] {name}: ISO 크기 {reader.size} != 팩 {size}")
                ok = False
                continue
            hs, ht = hashlib.sha256(), hashlib.sha256()
            ri = 0
            off = 0
            while off < size:
                n = min(CHUNK, size - off)
                a = reader.read_at(off, n)
                hs.update(a)
                b = bytearray(a)
                # 이 청크와 겹치는 범위를 덧씌운다
                while ri < len(ranges) and ranges[ri][0] < off + n:
                    roff, rdata = ranges[ri]
                    rlen = len(rdata)
                    s = max(roff, off)
                    e = min(roff + rlen, off + n)
                    b[s - off:e - off] = rdata[s - roff:e - roff]
                    if roff + rlen <= off + n:
                        ri += 1
                    else:
                        break
                ht.update(b)
                off += n
            got_s, got_t = hs.hexdigest().upper(), ht.hexdigest().upper()
            s_ok, t_ok = got_s == shash, got_t == thash
            ok = ok and s_ok and t_ok
            print(f"[{'OK' if s_ok and t_ok else 'FAIL'}] {name.rsplit('/',1)[-1]:13} ranges={len(ranges):,} source={'일치' if s_ok else got_s} target={'일치' if t_ok else got_t}  {time.time()-t0:.0f}s")
    print("RESULT:", "PASS" if ok else "FAIL")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
