# Texturas do item OVO (substitui o diamante):
#   item_ovo.png   ícone do HUD (ovo sem sombra no chão, mesmo desenho do ícone 200cc)
#   ovo_mancha.png mancha de ovo no chão (clara translúcida com borda, gemas, respingos, cascas) — RGBA
#   ovo_casca.png  casca do ovo (creme com pintinhas)
#   ovo_gota.png   gota redonda suave (respingos em voo)
# Rodar da raiz: .venv/bin/python tools/make_ovo_tex.py
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = "assets/"
rng = np.random.default_rng(7)


def sm(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------- ícone do HUD: reaproveita o ovo do 200cc, sem a sombra do chão ----------------
ic = Image.open(OUT + "icon_200cc_tab.png").convert("RGBA")
a = np.array(ic).astype(float)
yy = np.mgrid[0:a.shape[0], 0:a.shape[1]][0]
sombra = (a[..., 3] < 200) & (yy > a.shape[0] * 0.88)
a[sombra, 3] = 0
item = Image.fromarray(a.astype(np.uint8), "RGBA")
bb = item.getbbox()
item = item.crop(bb)
lado = max(item.size) + 16
quad = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
quad.alpha_composite(item, ((lado - item.size[0]) // 2, (lado - item.size[1]) // 2))
quad.resize((256, 256), Image.LANCZOS).save(OUT + "item_ovo.png")
print("ok item_ovo.png")

# ---------------- mancha no chão ----------------
N = 1024
y, x = (np.mgrid[0:N, 0:N].astype(float) + 0.5) / N - 0.5      # -0.5..0.5 ; borda do círculo = 0.5 = raio do efeito
r = np.hypot(x, y); th = np.arctan2(y, x)
borda = 0.405 + 0.0
for k, amp in ((2, 0.012), (3, 0.014), (5, 0.010), (9, 0.006), (15, 0.004)):
    borda = borda + amp * np.sin(k * th + rng.uniform(0, 6.28))
# dedos de respingo (raios de clara que escorrem para fora)
pontas = []
for _ in range(11):
    c = rng.uniform(-np.pi, np.pi); w = rng.uniform(0.035, 0.075); L = rng.uniform(0.025, 0.055)
    d = np.angle(np.exp(1j * (th - c)))
    borda = borda + L * np.exp(-(d / w) ** 2) ** 0.6
    pontas.append((c, 0.405 + L))
borda = np.minimum(borda, 0.47)
clara = sm(0.004, -0.004, r - borda)                          # 1 dentro
# gotas soltas perto da borda
gotas = np.zeros_like(r)
for _ in range(40):
    ang = rng.uniform(-np.pi, np.pi); rad = rng.uniform(0.43, 0.49)
    gx, gy = rad * np.cos(ang), rad * np.sin(ang); gr = rng.uniform(0.004, 0.011)
    gotas = np.maximum(gotas, sm(gr, gr * 0.6, np.hypot(x - gx, y - gy)))
for c, rp in pontas:                                          # gotinha logo além de cada ponta
    rad = min(rp + 0.025, 0.485); gx, gy = rad * np.cos(c), rad * np.sin(c); gr = 0.012
    gotas = np.maximum(gotas, sm(gr, gr * 0.6, np.hypot(x - gx, y - gy)))
clara = np.maximum(clara, gotas)
dist_b = np.clip(borda - r, 0, 1)
aro = np.exp(-dist_b / 0.012)                                 # borda mais grossa (tensão superficial)
brilho_c = np.exp(-((np.hypot(x + 0.10, y + 0.12)) / 0.22) ** 2) * 0.25
alfa = clara * (0.42 + 0.40 * aro + brilho_c)
cor = np.ones((N, N, 3)) * np.array([0.97, 0.94, 0.84])
cor = cor * (0.93 + 0.07 * aro[..., None]) + brilho_c[..., None] * 0.1
# gemas: uma grande + algumas menores + filetes
gemas = [(0.04, -0.03, 0.115), (-0.17, 0.12, 0.045), (0.20, 0.15, 0.034), (0.13, -0.21, 0.028), (-0.10, -0.20, 0.022), (-0.25, -0.05, 0.018)]
gema = np.zeros_like(r); brilho = np.zeros_like(r); sombra_g = np.zeros_like(r)
for gx, gy, gr in gemas:
    ang = np.arctan2(y - gy, x - gx)
    rr = gr * (1 + 0.06 * np.sin(3 * ang + gx * 40) + 0.03 * np.sin(7 * ang + gy * 30))
    dd = np.hypot(x - gx, y - gy)
    m = sm(rr, rr * 0.93, dd)
    gema = np.maximum(gema, m)
    sombra_g = np.maximum(sombra_g, m * sm(0.55, 1.0, dd / rr))
    hx, hy = gx - gr * 0.35, gy - gr * 0.38
    brilho = np.maximum(brilho, np.exp(-(np.hypot(x - hx, (y - hy) * 1.3) / (gr * 0.22)) ** 2))
# filetes de gema escorrendo da grande
for _ in range(0):
    c = rng.uniform(-np.pi, np.pi); L = rng.uniform(0.05, 0.13); w = rng.uniform(0.008, 0.016)
    px0, py0 = 0.04 + 0.11 * np.cos(c), -0.03 + 0.11 * np.sin(c)
    px1, py1 = px0 + L * np.cos(c), py0 + L * np.sin(c)
    vx, vy = px1 - px0, py1 - py0
    t = np.clip(((x - px0) * vx + (y - py0) * vy) / (vx * vx + vy * vy), 0, 1)
    dd = np.hypot(x - (px0 + t * vx), y - (py0 + t * vy))
    gema = np.maximum(gema, sm(w * (1 - 0.6 * t), w * (1 - 0.6 * t) * 0.6, dd) * clara)
c_gema = np.array([1.0, 0.72, 0.10])
c_borda = np.array([0.93, 0.45, 0.04])
cg = c_gema * (1 - sombra_g[..., None]) + c_borda * sombra_g[..., None]
cor = cor * (1 - gema[..., None]) + cg * gema[..., None]
cor = cor + brilho[..., None] * (1 - cor) * 0.95
alfa = np.maximum(alfa, gema)
# cascas quebradas espalhadas
img = Image.fromarray((np.dstack([np.clip(cor, 0, 1), np.clip(alfa, 0, 1)]) * 255).astype(np.uint8), "RGBA")
d = ImageDraw.Draw(img)
for _ in range(14):
    ang = rng.uniform(-np.pi, np.pi); rad = rng.uniform(0.12, 0.42)
    cx, cy = (0.5 + rad * np.cos(ang)) * N, (0.5 + rad * np.sin(ang)) * N
    s = rng.uniform(9, 22)
    pts = [(cx + s * rng.uniform(0.6, 1.2) * np.cos(a0), cy + s * rng.uniform(0.6, 1.2) * np.sin(a0))
           for a0 in np.sort(rng.uniform(0, 6.28, rng.integers(4, 7)))]
    d.polygon(pts, fill=(74, 52, 40, 255))
    pts2 = [(px + (cx - px) * 0.18, py + (cy - py) * 0.18) for px, py in pts]
    d.polygon(pts2, fill=(250, 241, 222, 255))
img = img.filter(ImageFilter.GaussianBlur(0.7))
img.resize((512, 512), Image.LANCZOS).save(OUT + "ovo_mancha.png")
print("ok ovo_mancha.png")

# ---------------- casca ----------------
M = 256
c = np.ones((M, M, 3)) * np.array([0.97, 0.91, 0.78])
for _ in range(90):
    px, py = rng.uniform(0, M, 2); rr = rng.uniform(0.8, 2.6)
    yy2, xx2 = np.mgrid[0:M, 0:M]
    m = sm(rr, rr * 0.5, np.hypot(xx2 - px, yy2 - py))
    c = c * (1 - 0.18 * m[..., None])
Image.fromarray((np.clip(c, 0, 1) * 255).astype(np.uint8)).save(OUT + "ovo_casca.png")
print("ok ovo_casca.png")

# ---------------- gota ----------------
G = 64
yy3, xx3 = (np.mgrid[0:G, 0:G] + 0.5) / G - 0.5
rr3 = np.hypot(xx3, yy3)
al = sm(0.48, 0.38, rr3)
hl = np.exp(-((np.hypot(xx3 + 0.13, yy3 + 0.15)) / 0.09) ** 2)
cc = np.dstack([np.ones_like(al)] * 3) * (0.82 + 0.18 * hl[..., None])
Image.fromarray((np.dstack([cc, al]) * 255).astype(np.uint8), "RGBA").save(OUT + "ovo_gota.png")
print("ok ovo_gota.png")
