# SPDX-License-Identifier: GPL-2.0-or-later
# Limited Underground flash helper: a separate program the Firmware Loader starts.
# It uses Espressif's esptool (GPL-2.0-or-later) as a library, so it carries the same
# licence and ships beside the Loader, never inside it. Design: L7 step 1 (D-063).
#
# Every run prints exactly one JSON line on stdout and nothing else; all esptool
# output goes to stderr. One connection, one attempt, no retries. It never erases
# the whole chip and never changes flash mode, frequency or size.
#
#   list                                         passive: USB serial ports (no device I/O)
#   identify --port P                            read-only: chip, MAC, flash size
#   digests  --port P --spans O:L[,O:L...]       read-only: chip MD5 of each span
#   write    --port P --offset O --file F        write one image, esptool MD5 check
#   boot     --port P --seconds S                hard reset, then watch the start-up log
import argparse
import contextlib
import json
import sys
import time

REQUIRED_ESPTOOL = "5.3.1"
REQUIRED_CHIP = "ESP32-S3"
REQUIRED_FLASH = "16MB"
SECTOR = 4096
BOOT_LOADED = "Loaded app from partition at offset 0x10000"
BOOT_HEALTHY = "companion boot self-check PASS"
# Trail PR #245: repeated after the self-check and with every heartbeat, so it survives a late port reopen.
BOOT_IDENT = "lu_ident "
BOOT_IDENT_HEALTHY = ("partition=0x10000", "self_check=PASS")


def emit(obj):
    sys.__stdout__.write(json.dumps(obj, separators=(",", ":"), sort_keys=True) + "\n")
    sys.__stdout__.flush()


def number(text):
    value = int(text, 0)
    if value < 0 or value > 0xFFFFFFFF:
        raise ValueError("out of range")
    return value


def connect(port):
    import esptool
    from esptool import cmds
    if esptool.__version__ != REQUIRED_ESPTOOL:
        raise RuntimeError("esptool version " + esptool.__version__)
    esp = cmds.detect_chip(port=port, connect_mode="default-reset", connect_attempts=1)
    if esp.CHIP_NAME != REQUIRED_CHIP:
        esp._port.close()
        raise RuntimeError("chip " + esp.CHIP_NAME)
    esp = esp.run_stub()
    cmds.attach_flash(esp)
    size = cmds.detect_flash_size(esp)
    if size != REQUIRED_FLASH:
        esp._port.close()
        raise RuntimeError("flash size " + str(size))
    esp.flash_set_parameters(cmds.flash_size_bytes(size))
    return esp


def cmd_list(_args):
    from serial.tools import list_ports
    ports = [{"port": p.device, "vid": p.vid, "pid": p.pid, "serial": p.serial_number or ""}
             for p in list_ports.comports() if p.vid is not None]
    return {"ok": True, "ports": sorted(ports, key=lambda p: p["port"])}


def cmd_identify(args):
    esp = connect(args.port)
    try:
        mac = ":".join("%02X" % b for b in esp.read_mac())
        return {"ok": True, "chip": esp.CHIP_NAME, "mac": mac, "flash": REQUIRED_FLASH}
    finally:
        esp._port.close()


def parse_spans(text):
    spans = []
    for part in text.split(","):
        offset, length = part.split(":")
        offset, length = number(offset), number(length)
        if length == 0 or offset + length > 16 * 1024 * 1024:
            raise ValueError("span")
        spans.append((offset, length))
    if not spans or len(spans) > 256:
        raise ValueError("span count")
    return spans


def cmd_digests(args):
    spans = parse_spans(args.spans)
    esp = connect(args.port)
    try:
        out = [{"offset": o, "length": n, "md5": esp.flash_md5sum(o, n).upper()} for o, n in spans]
        return {"ok": True, "spans": out}
    finally:
        esp._port.close()


def cmd_write(args):
    import hashlib
    from esptool import cmds
    offset = number(args.offset)
    with open(args.file, "rb") as handle:
        image = handle.read()
    if offset % SECTOR or not image or len(image) % SECTOR:
        raise ValueError("image must be sector aligned and a whole number of sectors")
    expected = hashlib.md5(image).hexdigest().upper()
    esp = connect(args.port)
    try:
        # write_flash erases only the sectors it writes and checks the chip's MD5 after writing;
        # it raises on a mismatch. Flash parameters are kept as they are.
        cmds.write_flash(esp, [(offset, image)], flash_freq="keep", flash_mode="keep", flash_size="keep")
        actual = esp.flash_md5sum(offset, len(image)).upper()
        return {"ok": actual == expected, "offset": offset, "length": len(image), "md5": actual}
    finally:
        esp._port.close()


def cmd_boot(args):
    import serial
    from esptool import cmds
    seconds = float(args.seconds)
    if not 1 <= seconds <= 120:
        raise ValueError("seconds")
    esp = connect(args.port)
    cmds.reset_chip(esp, "hard-reset")
    esp._port.close()
    deadline = time.monotonic() + seconds
    loaded = healthy = False
    lines = 0
    while time.monotonic() < deadline and not (loaded and healthy):
        try:
            with serial.Serial(args.port, 115200, timeout=0.5) as link:
                while time.monotonic() < deadline and not (loaded and healthy):
                    raw = link.readline()
                    if not raw:
                        continue
                    lines += 1
                    text = raw.decode("utf-8", "replace")
                    loaded = loaded or BOOT_LOADED in text
                    healthy = healthy or BOOT_HEALTHY in text
                    if BOOT_IDENT in text and all(part in text for part in BOOT_IDENT_HEALTHY):
                        loaded = healthy = True
        except (OSError, serial.SerialException):
            time.sleep(0.2)  # the USB port re-enumerates after reset
    return {"ok": True, "loaded": loaded, "healthy": healthy, "lines": lines}


def main(argv):
    parser = argparse.ArgumentParser(prog="lu_flash_helper")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("list")
    for name in ("identify", "digests", "write", "boot"):
        p = sub.add_parser(name)
        p.add_argument("--port", required=True)
        if name == "digests":
            p.add_argument("--spans", required=True)
        if name == "write":
            p.add_argument("--offset", required=True)
            p.add_argument("--file", required=True)
        if name == "boot":
            p.add_argument("--seconds", required=True)
    args = parser.parse_args(argv)
    handler = {"list": cmd_list, "identify": cmd_identify, "digests": cmd_digests,
               "write": cmd_write, "boot": cmd_boot}[args.command]
    try:
        with contextlib.redirect_stdout(sys.stderr):
            result = handler(args)
    except BaseException as error:  # esptool may raise FatalError or call sys.exit
        result = {"ok": False, "error": type(error).__name__ + ": " + str(error)[:300]}
    emit(result)
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
