# -*- coding: utf-8 -*-
"""씬/프리팹 YAML의 문자열 필드를 디코딩해 출력한다.

Unity는 비ASCII 문자열을 YAML 이중따옴표 스칼라 + \\uXXXX 이스케이프로 직렬화하므로
사람이 눈으로 읽을 수 없다 — 씬에 굳어버린 옛 대사를 육안으로 못 잡는 이유.

사용:
  python decode_scene_strings.py <파일>                 # 전체 문자열 덤프
  python decode_scene_strings.py <파일> --placeholders  # {0} 같은 자리표시자만
"""
import io
import os
import re
import sys

BACKSLASH = chr(92)
QUOTE = chr(34)
QUOTED = re.compile(r'^(\s*)(-?\s*)([A-Za-z_][A-Za-z0-9_]*):\s*' + QUOTE)
PLAIN = re.compile(r'^\s*-?\s*([A-Za-z_][A-Za-z0-9_]*):\s*(\S.*)$')
PLACEHOLDER = re.compile(r'\{\d+\}')

SIMPLE_ESCAPES = {'n': '\n', 't': '\t', 'r': '\r', '0': '\0', QUOTE: QUOTE, BACKSLASH: BACKSLASH}


def unescape(s):
    out = []
    i = 0
    while i < len(s):
        c = s[i]
        if c == BACKSLASH and i + 1 < len(s):
            n = s[i + 1]
            if n == 'u':
                out.append(chr(int(s[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(SIMPLE_ESCAPES.get(n, n))
            i += 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def _close_at(body):
    """이스케이프되지 않은 종료 따옴표 위치. 없으면 None."""
    j = 0
    while j < len(body):
        if body[j] == BACKSLASH:
            j += 2
            continue
        if body[j] == QUOTE:
            return j
        j += 1
    return None


def iter_strings(path):
    """(줄번호, 키, 디코딩된 값)을 순회한다. 접힌(멀티라인) 스칼라는 공백 1개로 이어붙인다."""
    with io.open(path, 'r', encoding='utf-8') as f:
        lines = f.read().split('\n')
    i = 0
    while i < len(lines):
        line = lines[i]
        m = QUOTED.match(line)
        if m:
            start = i
            body = line[line.index(QUOTE) + 1:]
            while True:
                at = _close_at(body)
                if at is not None:
                    body = body[:at]
                    break
                i += 1
                if i >= len(lines):
                    break
                body = body + ' ' + lines[i].strip()
            yield (start + 1, m.group(3), unescape(body))
        else:
            m2 = PLAIN.match(line)
            if m2 and not m2.group(2).startswith('{'):
                yield (i + 1, m2.group(1), m2.group(2))
        i += 1


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    path = sys.argv[1]
    only_ph = '--placeholders' in sys.argv
    hits = 0
    for ln, key, val in iter_strings(path):
        if only_ph:
            if PLACEHOLDER.search(val):
                hits += 1
                print(u'%s:%d  %s = %s' % (os.path.basename(path), ln, key, val))
        else:
            print(u'%d\t%s\t%s' % (ln, key, val))
    if only_ph:
        print(u'-- 자리표시자 %d건' % hits)
        return 1 if hits else 0
    return 0


if __name__ == '__main__':
    sys.exit(main())
