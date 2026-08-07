# -*- coding: utf-8 -*-
"""Unity 씬/프리팹 YAML의 계층·컴포넌트를 덤프한다 (에디터 없이).

사용:
    python dump_hierarchy.py <path.unity|path.prefab> [--root <이름>] [--comp] [--pos]

의도: 프리팹 전환 전후로 계층·좌표·정렬순서·컴포넌트 배선을 대조하기 위한 읽기 전용 도구.
"""
import sys
import io
import re
import yaml


def load_docs(path):
    """Unity YAML → {fileID: (classId, bodydict)}"""
    text = io.open(path, "r", encoding="utf-8").read()
    # 문서 헤더: --- !u!<class> &<fileID> [stripped]
    parts = re.split(r"^--- !u!(\d+) &(\d+)(.*)$", text, flags=re.M)
    docs = {}
    # parts[0] = 헤더(%YAML …), 이후 4개씩
    for i in range(1, len(parts), 4):
        cls = int(parts[i])
        fid = int(parts[i + 1])
        body = parts[i + 3]
        # !u! 태그가 본문에 남아 있으면 제거
        body = re.sub(r"!u!\d+\s*", "", body)
        try:
            d = yaml.safe_load(body)
        except Exception as e:  # noqa
            d = {"__parse_error__": str(e)}
        if isinstance(d, dict):
            key = list(d.keys())[0]
            docs[fid] = (cls, d[key] if isinstance(d[key], dict) else d, key)
    return docs


def build(docs):
    go = {}       # fileID -> GameObject dict
    tr = {}       # fileID -> Transform dict
    comps = {}    # fileID -> component doc
    for fid, (cls, body, key) in docs.items():
        if key == "GameObject":
            go[fid] = body
        elif key in ("Transform", "RectTransform"):
            tr[fid] = body
        else:
            comps[fid] = (cls, body, key)
    return go, tr, comps


def go_of_transform(tr_body):
    return tr_body.get("m_GameObject", {}).get("fileID")


def children_of(tr_body):
    return [c.get("fileID") for c in (tr_body.get("m_Children") or [])]


def transform_of_go(go_fid, tr):
    for fid, body in tr.items():
        if go_of_transform(body) == go_fid:
            return fid
    return None


def main():
    path = sys.argv[1]
    root_name = None
    show_comp = "--comp" in sys.argv
    show_pos = "--pos" in sys.argv
    if "--root" in sys.argv:
        root_name = sys.argv[sys.argv.index("--root") + 1]

    docs = load_docs(path)
    go, tr, comps = build(docs)

    # 컴포넌트 fileID -> 스크립트 이름
    def comp_label(fid):
        if fid in tr:
            return "Transform"
        if fid not in comps:
            return "?%d" % fid
        cls, body, key = comps[fid]
        if key == "MonoBehaviour":
            ident = body.get("m_EditorClassIdentifier") or ""
            if ident:
                return ident.split("::")[-1]
            return "MonoBehaviour(%s)" % body.get("m_Script", {}).get("guid", "")[:8]
        return key

    lines = []

    def walk(tr_fid, depth):
        body = tr.get(tr_fid)
        if body is None:
            return
        gfid = go_of_transform(body)
        g = go.get(gfid, {})
        name = g.get("m_Name", "?")
        active = g.get("m_IsActive", 1)
        extra = ""
        if show_pos:
            p = body.get("m_LocalPosition", {})
            extra += " pos=(%.4g,%.4g,%.4g)" % (p.get("x", 0), p.get("y", 0), p.get("z", 0))
            s = body.get("m_LocalScale", {})
            extra += " scale=(%.4g,%.4g,%.4g)" % (s.get("x", 1), s.get("y", 1), s.get("z", 1))
        if show_comp:
            cl = [comp_label(c.get("component", {}).get("fileID"))
                  for c in (g.get("m_Component") or [])]
            cl = [c for c in cl if c != "Transform"]
            if cl:
                extra += "  [%s]" % ", ".join(cl)
            # SpriteRenderer 정렬 순서
            for c in (g.get("m_Component") or []):
                cf = c.get("component", {}).get("fileID")
                if cf in comps and comps[cf][2] == "SpriteRenderer":
                    b = comps[cf][1]
                    extra += " sort=%s/%s" % (b.get("m_SortingLayer", 0), b.get("m_SortingOrder", 0))
        lines.append("%s%s%s%s" % ("  " * depth, name,
                                   "" if active else " (inactive)", extra))
        for c in children_of(body):
            walk(c, depth + 1)

    # 루트 = 부모 없는 Transform
    roots = [fid for fid, b in tr.items()
             if not (b.get("m_Father") or {}).get("fileID")]
    roots.sort(key=lambda f: go.get(go_of_transform(tr[f]), {}).get("m_Name", ""))
    for r in roots:
        gname = go.get(go_of_transform(tr[r]), {}).get("m_Name", "?")
        if root_name and gname != root_name:
            continue
        walk(r, 0)

    out = "\n".join(lines)
    sys.stdout.buffer.write(out.encode("utf-8"))
    sys.stdout.buffer.write(b"\n")


if __name__ == "__main__":
    main()
