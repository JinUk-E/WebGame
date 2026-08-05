# -*- coding: utf-8 -*-
"""밀실 버티기 — 절차 생성 아트 공용 헬퍼.

전 스프라이트가 공유하는 팔레트/질감/드로잉 유틸.
모든 난수는 random.Random(seed) 인스턴스로 고정 → 재실행 시 동일 결과.
Pillow 전용 (numpy 불필요).
"""
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

# ---------------------------------------------------------------- 경로
REPO_ROOT = Path(__file__).resolve().parents[2]
ART_ROOT = REPO_ROOT / "Assets" / "_Project" / "Art"
FONT_PATH = ART_ROOT / "Fonts" / "Pretendard-Regular.ttf"

# ---------------------------------------------------------------- 팔레트 (아트 디렉션 고정 상수)
NIGHT_DARK = (20, 24, 38)        # #141826 밤 남색(어두움)
NIGHT_LIGHT = (31, 35, 56)       # #1f2338 밤 남색(밝음)
HANJI_BRIGHT = (232, 220, 192)   # #e8dcc0 한지 아이보리(밝은 씬)
HANJI_DIM = (184, 174, 149)      # #b8ae95 한지 아이보리(어두운 씬)
WOOD_DARK = (74, 56, 40)         # #4a3828 나무 갈색(어두움)
WOOD_LIGHT = (107, 81, 56)       # #6b5138 나무 갈색(밝음)
TALISMAN_PAPER = (217, 164, 65)  # #d9a441 부적 황지
TALISMAN_RED = (168, 60, 48)     # #a83c30 주사 붉은 문양
TV_GLOW = (159, 196, 216)        # #9fc4d8 TV 청백광
INK = (18, 14, 11)               # 먹
EMBER_HOT = (255, 176, 84)       # 잔불(밝음)
EMBER = (224, 118, 40)           # 잔불
CHAR = (26, 18, 14)              # 그을음


# ---------------------------------------------------------------- 색 유틸
def mul(c, f):
    """색 밝기 배율 (알파 무시)."""
    return tuple(min(255, max(0, int(v * f))) for v in c[:3])


def mix(a, b, t):
    """두 색 선형 보간."""
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


# ---------------------------------------------------------------- 캔버스/저장
def canvas(w, h, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (w, h), color)


def save(img, rel_path):
    """ART_ROOT 기준 상대 경로로 저장."""
    out = ART_ROOT / rel_path
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out, optimize=True)
    print(f"  {rel_path}  {img.size[0]}x{img.size[1]}")
    return out


def downscale(img, factor):
    """슈퍼샘플 캔버스 → 최종 크기 (안티앨리어싱)."""
    w, h = img.size
    return img.resize((w // factor, h // factor), Image.LANCZOS)


# ---------------------------------------------------------------- 그라디언트
def vgrad(w, h, top, bottom):
    """세로 그라디언트 RGB 이미지."""
    g = Image.linear_gradient("L").resize((w, h))
    t = Image.new("RGB", (w, h), top)
    b = Image.new("RGB", (w, h), bottom)
    return Image.composite(b, t, g)


def hgrad(w, h, left, right):
    g = Image.linear_gradient("L").rotate(90, expand=True).resize((w, h))
    l = Image.new("RGB", (w, h), left)
    r = Image.new("RGB", (w, h), right)
    return Image.composite(l, r, g)


def radial_mask(w, h, inner=0, outer=255):
    """중심 inner → 가장자리 outer 값의 L 마스크."""
    g = Image.radial_gradient("L").resize((w, h))
    if (inner, outer) != (0, 255):
        lut = [int(inner + (outer - inner) * (v / 255.0)) for v in range(256)]
        g = g.point(lut)
    return g


# ---------------------------------------------------------------- 질감
def apply_grain(img, opacity=0.08, sigma=32, blur=0):
    """미세 노이즈 질감 오버레이 (알파 보존)."""
    w, h = img.size
    n = Image.effect_noise((w, h), sigma).convert("L")
    if blur:
        n = n.filter(ImageFilter.GaussianBlur(blur))
    n_rgb = Image.merge("RGB", (n, n, n))
    base = img.convert("RGB")
    ov = ImageChops.overlay(base, n_rgb)
    out = Image.blend(base, ov, opacity).convert("RGBA")
    out.putalpha(img.getchannel("A"))
    return out


def streak_noise(w, h, sigma=40, axis="v", cell=16):
    """한 축으로 늘린 노이즈 → 나뭇결/섬유결 L 이미지."""
    if axis == "v":
        n = Image.effect_noise((w, max(1, h // cell)), sigma)
    else:
        n = Image.effect_noise((max(1, w // cell), h), sigma)
    return n.resize((w, h), Image.BILINEAR).convert("L")


def apply_streaks(img, opacity=0.10, axis="v", cell=16, sigma=40, blur=1):
    """결 질감 오버레이 (알파 보존)."""
    w, h = img.size
    n = streak_noise(w, h, sigma=sigma, axis=axis, cell=cell)
    if blur:
        n = n.filter(ImageFilter.GaussianBlur(blur))
    n_rgb = Image.merge("RGB", (n, n, n))
    base = img.convert("RGB")
    ov = ImageChops.overlay(base, n_rgb)
    out = Image.blend(base, ov, opacity).convert("RGBA")
    out.putalpha(img.getchannel("A"))
    return out


def add_stains(img, rng, count, color, alpha=(10, 26), radius=(24, 80), blur_f=0.5, region=None):
    """얼룩 — 블러 처리된 타원 몇 개."""
    w, h = img.size
    x0, y0, x1, y1 = region or (0, 0, w, h)
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        r = rng.randint(*radius)
        cx = rng.randint(x0, x1)
        cy = rng.randint(y0, y1)
        a = rng.randint(*alpha)
        ry = int(r * rng.uniform(0.5, 0.9))
        d.ellipse([cx - r, cy - ry, cx + r, cy + ry], fill=rgba(color, a))
    layer = layer.filter(ImageFilter.GaussianBlur(int(radius[1] * blur_f)))
    return Image.alpha_composite(img, layer)


def contact_shadow(img, box, alpha=70, blur=8):
    """소품 바닥 접지 그림자 (타원). img 밑에 깔린 새 캔버스를 반환."""
    base = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(base)
    d.ellipse(box, fill=(0, 0, 0, alpha))
    base = base.filter(ImageFilter.GaussianBlur(blur))
    return Image.alpha_composite(base, img)


def overlay(img):
    """반투명 도형용 임시 레이어 + 드로우.

    주의: ImageDraw는 RGBA 베이스에 반투명 색을 '알파 블렌드'가 아니라 '픽셀 교체'로
    그린다 (mode="RGBA" 블렌드는 RGB 베이스에서만 동작). 반투명 프리미티브는 반드시
    이 레이어에 그린 뒤 merge()로 합성할 것.
    """
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    return layer, ImageDraw.Draw(layer)


def merge(img, layer, blur=0):
    if blur:
        layer = layer.filter(ImageFilter.GaussianBlur(blur))
    return Image.alpha_composite(img, layer)


# ---------------------------------------------------------------- 마스크/도형
def shift_mask(mask, dx, dy):
    """랩어라운드 없는 마스크 이동."""
    out = Image.new("L", mask.size, 0)
    out.paste(mask, (dx, dy))
    return out


def rim_from_mask(mask, dx=0, dy=0, blur=4):
    """실루엣 마스크에서 (dx,dy) 방향 림 밴드 추출."""
    rim = ImageChops.subtract(mask, shift_mask(mask, dx, dy))
    if blur:
        rim = rim.filter(ImageFilter.GaussianBlur(blur))
    return rim


def cubic_bezier(p0, p1, p2, p3, n=24):
    """3차 베지어 샘플 점 목록."""
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        x = u**3 * p0[0] + 3 * u**2 * t * p1[0] + 3 * u * t**2 * p2[0] + t**3 * p3[0]
        y = u**3 * p0[1] + 3 * u**2 * t * p1[1] + 3 * u * t**2 * p2[1] + t**3 * p3[1]
        pts.append((x, y))
    return pts


def wobble_rect_poly(rng, box, amp=3.0, step=18):
    """가장자리가 미세하게 흔들리는 사각 폴리곤 (찢긴 종이 느낌)."""
    x0, y0, x1, y1 = box
    pts = []

    def edge(ax, ay, bx, by):
        length = math.hypot(bx - ax, by - ay)
        n = max(2, int(length / step))
        nx, ny = (by - ay) / length, -(bx - ax) / length  # 법선
        for i in range(n):
            t = i / n
            px, py = ax + (bx - ax) * t, ay + (by - ay) * t
            w = rng.uniform(-amp, amp)
            pts.append((px + nx * w, py + ny * w))

    edge(x0, y0, x1, y0)
    edge(x1, y0, x1, y1)
    edge(x1, y1, x0, y1)
    edge(x0, y1, x0, y0)
    return pts


def blob_poly(rng, cx, cy, rx, ry, irregularity=0.12, n=28):
    """가장자리가 불규칙한 타원 블롭 폴리곤 (소금 더미 등)."""
    pts = []
    # 저주파 변조 (각도 기반 사인 합)
    phases = [rng.uniform(0, math.tau) for _ in range(3)]
    amps = [rng.uniform(0.4, 1.0) * irregularity / (k + 1) for k in range(3)]
    for i in range(n):
        a = math.tau * i / n
        m = 1.0
        for k in range(3):
            m += amps[k] * math.sin((k + 2) * a + phases[k])
        pts.append((cx + math.cos(a) * rx * m, cy + math.sin(a) * ry * m))
    return pts


def brush_border(layer_draw, box, rng, width=8, jitter=2.5, color=INK, alpha=255, step=6):
    """먹 붓 테두리 — 경로를 따라 원 스탬프. 중간 변은 저지터(9-slice 안전)."""
    x0, y0, x1, y1 = box

    def stroke(ax, ay, bx, by, amp):
        length = math.hypot(bx - ax, by - ay)
        n = max(2, int(length / step))
        nx, ny = (by - ay) / length, -(bx - ax) / length
        for i in range(n + 1):
            t = i / n
            px, py = ax + (bx - ax) * t, ay + (by - ay) * t
            w = rng.uniform(-amp, amp)
            r = width / 2 * rng.uniform(0.75, 1.15)
            px += nx * w
            py += ny * w
            layer_draw.ellipse([px - r, py - r, px + r, py + r], fill=rgba(color, alpha))

    stroke(x0, y0, x1, y0, jitter)
    stroke(x1, y0, x1, y1, jitter)
    stroke(x1, y1, x0, y1, jitter)
    stroke(x0, y1, x0, y0, jitter)


def clip_to(layer, mask):
    """레이어 알파를 마스크로 클리핑."""
    a = ImageChops.multiply(layer.getchannel("A"), mask)
    layer.putalpha(a)
    return layer
