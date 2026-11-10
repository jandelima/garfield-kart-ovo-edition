# Ícone do modo 200cc = OVO (pedido do usuário em 10/11/2026; antes era a chaleira vermelha, tools/make_icon200.py).
# Gera assets/icon_200cc_tab.png (sem texto, aba) e assets/icon_200cc.png (com "200cc"), 280x280, no estilo
# dos ícones do jogo: contorno escuro, sombreado suave, brilho especular.
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

base = 'assets/'
S = 4
T = 140 * S
yy, xx = np.mgrid[0:T, 0:T].astype(float) / S          # coordenadas na escala 140

cx, cy, a, b = 70.0, 75.0, 43.0, 59.0                  # ovo: centro e semi-eixos
inc = np.radians(-8)                                   # leve inclinação, como o "150cc" do original
dx, dy = xx - cx, yy - cy
u = (dx * np.cos(inc) - dy * np.sin(inc)) / a
v = (dx * np.sin(inc) + dy * np.cos(inc)) / b
g = 1.0 / (1.0 + 0.17 * v) ** 2                        # mais largo embaixo, pontudo em cima
r2 = u * u * g + v * v
dentro = r2 <= 1.0

# normal aproximada de um ovo "inflado"
nz = np.sqrt(np.clip(1 - r2, 0, 1))
n = np.dstack([u * np.sqrt(g), v, nz * 1.15])
n /= np.linalg.norm(n, axis=2, keepdims=True)
L = np.array([-0.55, -0.62, 0.56]); L /= np.linalg.norm(L)
dif = np.clip((n * L).sum(2), 0, 1)
t = np.clip(dif, 0, 1) ** 0.85
sombra = np.array([0.74, 0.52, 0.40]); meio = np.array([0.96, 0.88, 0.74]); luz = np.array([1.0, 0.985, 0.94])
cor = np.where(t[..., None] < 0.5,
               sombra + (meio - sombra) * (t[..., None] / 0.5),
               meio + (luz - meio) * ((t[..., None] - 0.5) / 0.5))
# luz rebatida quente embaixo à direita (como a chaleira)
rim = np.clip((n * np.array([0.6, 0.7, 0.2])).sum(2), 0, 1) ** 3 * (1 - nz) ** 0.5
cor = cor + rim[..., None] * np.array([0.30, 0.14, 0.04])
# pintinhas leves de casca
rng = np.random.default_rng(4)
for _ in range(26):
    px, py = rng.uniform(-0.8, 0.8), rng.uniform(-0.75, 0.85)
    if px * px * 1.1 + py * py > 0.8:
        continue
    rr = rng.uniform(0.6, 1.6)
    ex, ey = cx + px * a, cy + py * b
    m = ((xx - ex) ** 2 + (yy - ey) ** 2) < rr * rr
    cor[m] = cor[m] * np.array([0.88, 0.80, 0.72])
# textura de pintura (granulado suave)
gr = np.array(Image.fromarray((rng.random((T // 3, T // 3)) * 255).astype(np.uint8)).resize((T, T), Image.BICUBIC)) / 255.0
cor *= (0.97 + 0.06 * gr)[..., None]
# brilho especular
hx, hy = cx - 0.42 * a, cy - 0.50 * b
esp = np.exp(-(((xx - hx) / 9.0) ** 2 + ((yy - hy) / 13.0) ** 2) * 1.6)
esp2 = np.exp(-(((xx - (hx + 5)) / 3.0) ** 2 + ((yy - (hy + 16)) / 3.0) ** 2) * 2.0)
cor = cor + (esp * 0.55 + esp2 * 0.45)[..., None] * (1 - cor)

# contorno escuro (anel fora do ovo) + sombra no chão
borda = (r2 > 1.0) & (r2 <= 1.0 + 0.105)
contorno = np.array([0.24, 0.12, 0.09])
img = np.zeros((T, T, 4))
chao = np.exp(-(((xx - cx - 4) / 40.0) ** 2 + ((yy - 133.5) / 4.2) ** 2) * 1.2) * 0.55
img[..., :3] = contorno * 0.6
img[..., 3] = chao
img[borda, :3] = contorno; img[borda, 3] = 1
img[dentro, :3] = cor[dentro]; img[dentro, 3] = 1
# linha interna mais escura no lado da sombra (dá volume, igual aos ícones do jogo)
interno = dentro & (r2 > 0.86) & (dif < 0.35)
img[interno, :3] = img[interno, :3] * 0.86
out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGBA')
out = out.filter(ImageFilter.GaussianBlur(0.6 * S / 2))
out.resize((280, 280), Image.LANCZOS).save(base + 'icon_200cc_tab.png')

# "200cc" inclinado, mesma tinta e fonte do ícone anterior
font = ImageFont.truetype('/mnt/c/Windows/Fonts/impact.ttf', 200)
fsmall = ImageFont.truetype('/mnt/c/Windows/Fonts/impact.ttf', 128)
tx = Image.new('RGBA', (1400, 400), (0, 0, 0, 0))
d = ImageDraw.Draw(tx)
ink = (22, 10, 8, 255)
d.text((20, 300), '200', font=font, fill=ink, anchor='ls')
w = d.textlength('200', font=font)
d.text((20 + w + 6, 300), 'cc', font=fsmall, fill=ink, anchor='ls')
tx = tx.crop(tx.getbbox())
tx = tx.resize((64 * S, 37 * S), Image.LANCZOS)
tx = tx.rotate(11, resample=Image.BICUBIC, expand=True)
pcx, pcy = 71 * S, 87 * S
out.alpha_composite(tx, (int(pcx - tx.width / 2), int(pcy - tx.height / 2)))
out.resize((280, 280), Image.LANCZOS).save(base + 'icon_200cc.png')
print('ok')
