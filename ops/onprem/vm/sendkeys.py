#!/usr/bin/env python3
"""Type text into the VM console through the qemu HMP monitor (sendkey), for console-only recovery.

  sendkeys.py MONITOR_SOCK --text 'dir'          type literal text (then Enter with --enter)
  sendkeys.py MONITOR_SOCK --env FILE VAR        type the value of VAR from an env file (not echoed)
  sendkeys.py MONITOR_SOCK --key ctrl-alt-delete send one key combo
"""
import argparse, socket, time

SHIFTED = {'!': '1', '@': '2', '#': '3', '$': '4', '%': '5', '^': '6', '&': '7', '*': '8', '(': '9', ')': '0',
           '_': 'minus', '+': 'equal', '{': 'bracket_left', '}': 'bracket_right', ':': 'semicolon', '"': 'apostrophe',
           '<': 'comma', '>': 'dot', '?': 'slash', '|': 'backslash', '~': 'grave_accent'}
PLAIN = {' ': 'spc', '-': 'minus', '=': 'equal', '[': 'bracket_left', ']': 'bracket_right', ';': 'semicolon',
         "'": 'apostrophe', ',': 'comma', '.': 'dot', '/': 'slash', '\\': 'backslash', '`': 'grave_accent'}


def keyname(ch):
    if ch.isalpha():
        return f"shift-{ch.lower()}" if ch.isupper() else ch
    if ch.isdigit():
        return ch
    if ch in SHIFTED:
        return f"shift-{SHIFTED[ch]}"
    if ch in PLAIN:
        return PLAIN[ch]
    raise ValueError(f"no key for {ch!r}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sock")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--text")
    g.add_argument("--env", nargs=2, metavar=("FILE", "VAR"))
    g.add_argument("--key")
    ap.add_argument("--enter", action="store_true")
    a = ap.parse_args()
    if a.env:
        val = next(l.split("=", 1)[1].strip() for l in open(a.env[0]) if l.startswith(a.env[1] + "="))
        keys = [keyname(c) for c in val]
    elif a.text is not None:
        keys = [keyname(c) for c in a.text]
    else:
        keys = [a.key]
    if a.enter:
        keys.append("ret")
    s = socket.socket(socket.AF_UNIX)
    s.connect(a.sock)
    s.settimeout(2)
    try:
        s.recv(4096)
    except socket.timeout:
        pass
    for k in keys:
        s.sendall(f"sendkey {k}\n".encode())
        time.sleep(0.15)
    time.sleep(0.5)
    s.close()
    print(f"sent {len(keys)} keys")


if __name__ == "__main__":
    main()
