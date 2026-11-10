# Selo "OVO EDITION" para a tela inicial (abaixo do logo). Gera assets/selo_ovo_edition.png
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

S = 2                                    # desenha em 2x
W, H = 1800 * S, 330 * S
base = "assets/"
fonte = ImageFont.truetype("/mnt/c/Windows/Fonts/seguibl.ttf", 150 * S)


def arco_texto(txt, fnt, curv):
    """renderiza texto e entorta num arco suave (sorriso)"""
    tmp = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(tmp)
    bb = d.textbbox((0, 0), txt, font=fnt)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    d.text(((W - tw) / 2 - bb[0], (H - th) / 2 - bb[1] - 8 * S), txt, font=fnt, fill=255)
    a = np.array(tmp).astype(float)
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    off = curv * ((xx - W / 2) / (W / 2)) ** 2
    ys = np.clip(yy + off, 0, H - 1).astype(int)
    out = a[ys, xx.astype(int)]
    return out / 255.0


def dilatar(m, r):
    im = Image.fromarray((m * 255).astype(np.uint8))
    return np.array(im.filter(ImageFilter.MaxFilter(r * 2 + 1))).astype(float) / 255.0


def suavizar(m, r):
    return np.array(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(float) / 255.0


letras = arco_texto("OVO EDITION", fonte, -26 * S)
contorno = suavizar(dilatar(letras, 13 * S), 1.2 * S)
contorno2 = suavizar(dilatar(letras, 21 * S), 2 * S)          # aura escura externa
yy, xx = np.mgrid[0:H, 0:W].astype(float)
# preenchimento: creme no alto -> amarelo gema -> laranja embaixo
ys = np.where(letras > 0.02)[0]
y0, y1 = (ys.min(), ys.max()) if len(ys) else (0, H)
t = np.clip((yy - y0) / max(1, y1 - y0), 0, 1)[..., None]
c_top = np.array([1.0, 0.98, 0.88]); c_mid = np.array([1.0, 0.82, 0.22]); c_bot = np.array([0.98, 0.50, 0.06])
fill = np.where(t < 0.45, c_top + (c_mid - c_top) * (t / 0.45), c_mid + (c_bot - c_mid) * ((t - 0.45) / 0.55))
# brilho em faixa no topo de cada letra
brilho = np.clip(1 - np.abs(t[..., 0] - 0.18) / 0.10, 0, 1) * 0.35
fill = fill + brilho[..., None] * (1 - fill)
# sombra interna embaixo (volume)
sombra_in = np.clip(letras - np.roll(letras, -9 * S, axis=0), 0, 1)
fill = fill * (1 - 0.25 * sombra_in[..., None])
img = np.zeros((H, W, 4))
escuro = np.array([0.30, 0.09, 0.04]); escuro2 = np.array([0.16, 0.04, 0.02])
# aura externa + sombra deslocada
somb = np.roll(np.roll(contorno2, 10 * S, axis=0), 6 * S, axis=1)
img[..., :3] = escuro2; img[..., 3] = np.maximum(contorno2 * 0.95, somb * 0.6)
m = contorno
img[..., :3] = img[..., :3] * (1 - m[..., None]) + escuro * m[..., None]; img[..., 3] = np.maximum(img[..., 3], m)
m = letras
img[..., :3] = img[..., :3] * (1 - m[..., None]) + fill * m[..., None]; img[..., 3] = np.maximum(img[..., 3], m)
out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGBA")
# ovos dos dois lados (o do ícone do item), inclinados
ovo = Image.open(base + "item_ovo.png").convert("RGBA")
bb = np.where(letras > 0.02)
xl, xr = bb[1].min(), bb[1].max()
for lado, ang, esc in ((-1, 18, 1.0), (1, -16, 0.86)):
    o = ovo.resize((int(250 * S * esc), int(250 * S * esc)), Image.LANCZOS).rotate(ang, resample=Image.BICUBIC, expand=True)
    cx = (xl - 135 * S) if lado < 0 else (xr + 130 * S)
    cy = H // 2 + (6 * S if lado < 0 else 14 * S)
    out.alpha_composite(o, (int(cx - o.width / 2), int(cy - o.height / 2)))
out = out.crop(out.getbbox())
out = out.resize((out.width // S, out.height // S), Image.LANCZOS)
out.save(base + "selo_ovo_edition.png")
print("ok", out.size)
