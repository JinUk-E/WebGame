# -*- coding: utf-8 -*-
"""두 Unity YAML 파일의 같은 이름 서브트리를 구조·컴포넌트·값 단위로 비교한다.

사용:
    python diff_subtree.py <A.unity> <rootA> <B.prefab> <rootB>

경로(Room/Door/Closed) 기준으로 정렬해 비교하므로 자식 순서 차이는 무시한다.
"""
import sys
import io
import re
import yaml

SKIP_KEYS = {
    "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
    "m_PrefabAsset", "m_GameObject", "m_Father", "m_Children", "m_Component",
    "serializedVersion", "m_RootOrder", "m_LocalEulerAnglesHint",
}


def load_docs(path):
    text = io.open(path, "r", encoding="utf-8").read()
    parts = re.split(r"^--- !u!(\d+) &(\d+)(.*)$", text, flags=re.M)
    docs = {}
    for i in range(1, len(parts), 4):
        fid = int(parts[i + 1])
        body = re.sub(r"!u!\d+\s*", "", parts[i + 3])
        try:
            d = yaml.safe_load(body)
        except Exception as e:
            d = {"ParseError": {"err": str(e)}}
        if isinstance(d, dict) and d:
            key = list(d.keys())[0]
            val = d[key] if isinstance(d[key], dict) else {}
            docs[fid] = (key, val)
    return docs


def index(docs):
    go, tr, comps = {}, {}, {}
    for fid, (key, body) in docs.items():
        if key == "GameObject":
            go[fid] = body
        elif key in ("Transform", "RectTransform"):
            tr[fid] = body
        else:
            comps[fid] = (key, body)
    return go, tr, comps


def collect(path, root_name):
    docs = load_docs(path)
    go, tr, comps = index(docs)
    out = {}  # path -> {"go":.., "comps": {label: body}, "tr": body}

    def label(fid):
        if fid in tr:
            return "Transform"
        if fid not in comps:
            return "MISSING#%d" % fid
        key, body = comps[fid]
        if key == "MonoBehaviour":
            ident = body.get("m_EditorClassIdentifier") or ""
            return ident.split("::")[-1] if ident else \
                "MB(%s)" % str(body.get("m_Script", {}).get("guid", ""))[:8]
        return key

    def walk(tr_fid, prefix):
        body = tr[tr_fid]
        gfid = body.get("m_GameObject", {}).get("fileID")
        g = go.get(gfid, {})
        name = g.get("m_Name", "?")
        p = prefix + "/" + name if prefix else name
        cd = {}
        for c in (g.get("m_Component") or []):
            cf = c.get("component", {}).get("fileID")
            lb = label(cf)
            if lb == "Transform":
                continue
            cd.setdefault(lb, []).append(comps.get(cf, ("?", {}))[1])
        out[p] = {"active": g.get("m_IsActive", 1), "tr": body, "comps": cd,
                  "layer": g.get("m_Layer", 0), "tag": g.get("m_TagString", "")}
        for c in (body.get("m_Children") or []):
            walk(c["fileID"], p)

    for fid, b in tr.items():
        if (b.get("m_Father") or {}).get("fileID"):
            continue
        gfid = b.get("m_GameObject", {}).get("fileID")
        if go.get(gfid, {}).get("m_Name") == root_name:
            walk(fid, "")
    return out


def norm(v):
    if isinstance(v, float):
        return round(v, 5)
    if isinstance(v, dict):
        return {k: norm(x) for k, x in sorted(v.items()) if k not in SKIP_KEYS}
    if isinstance(v, list):
        return [norm(x) for x in v]
    return v


def ref_kind(v):
    """참조 dict인지 판별 → ('guid', g) | ('local',) | None"""
    if isinstance(v, dict) and "fileID" in v:
        if v.get("guid"):
            return ("guid", v["guid"], v.get("fileID"))
        return ("local", v["fileID"])
    return None


def main():
    fa, ra, fb, rb = sys.argv[1:5]
    A = collect(fa, ra)
    B = collect(fb, rb)
    ka, kb = set(A), set(B)
    lines = []

    def rel(p, root):
        return p[len(root):] if p.startswith(root) else p

    ma = {rel(p, ra): p for p in ka}
    mb = {rel(p, rb): p for p in kb}
    only_a = sorted(set(ma) - set(mb))
    only_b = sorted(set(mb) - set(ma))
    if only_a:
        lines.append("### A에만 있는 오브젝트 (%d)" % len(only_a))
        lines += ["  + " + p for p in only_a]
    if only_b:
        lines.append("### B에만 있는 오브젝트 (%d)  <-- 유실 위험" % len(only_b))
        lines += ["  - " + p for p in only_b]
    if not only_a and not only_b:
        lines.append("### 계층 구조 동일 (%d 오브젝트)" % len(ma))

    diffs = 0
    for p in sorted(set(ma) & set(mb)):
        a, b = A[ma[p]], B[mb[p]]
        loc = []
        if a["active"] != b["active"]:
            loc.append("active %s -> %s" % (b["active"], a["active"]))
        for fld in ("m_LocalPosition", "m_LocalRotation", "m_LocalScale",
                    "m_AnchoredPosition", "m_SizeDelta"):
            if norm(a["tr"].get(fld)) != norm(b["tr"].get(fld)):
                loc.append("%s %s -> %s" % (fld, norm(b["tr"].get(fld)), norm(a["tr"].get(fld))))
        ca, cb = set(a["comps"]), set(b["comps"])
        for c in sorted(ca - cb):
            loc.append("컴포넌트 추가(A만): %s" % c)
        for c in sorted(cb - ca):
            loc.append("컴포넌트 없음(B만): %s   <-- 유실 위험" % c)
        for c in sorted(ca & cb):
            la, lb = a["comps"][c], b["comps"][c]
            for i in range(min(len(la), len(lb))):
                da, db = norm(la[i]), norm(lb[i])
                keys = set(da) | set(db)
                for k in sorted(keys):
                    va, vb = da.get(k), db.get(k)
                    if va == vb:
                        continue
                    # 로컬 fileID 참조는 파일이 달라 값이 다르므로 '연결/끊김'만 본다
                    ka_, kb_ = ref_kind(va), ref_kind(vb)
                    if ka_ and kb_ and ka_[0] == "local" and kb_[0] == "local":
                        an = ka_[1] != 0
                        bn = kb_[1] != 0
                        if an == bn:
                            continue
                        loc.append("%s.%s 참조 %s -> %s" %
                                   (c, k, "연결" if bn else "null", "연결" if an else "null"))
                        continue
                    loc.append("%s.%s: %s -> %s" % (c, k, vb, va))
        if loc:
            diffs += 1
            lines.append("## %s" % (p or "/"))
            lines += ["    " + x for x in loc]
    lines.append("### 값 차이가 있는 오브젝트: %d" % diffs)
    sys.stdout.buffer.write(("\n".join(lines) + "\n").encode("utf-8"))


if __name__ == "__main__":
    main()
