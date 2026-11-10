# Gera assets/icon_200cc.png a partir do ícone 150cc original do jogo.
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
base = 'assets/'
src = Image.open(base + 'orig_150cc.png').convert('RGBA')
S = 4                                   # trabalha em 4x (560px), entrega em 2x (280px)
im = src.resize((140 * S, 140 * S), Image.LANCZOS)
a = np.array(im).astype(float)

# 1) apaga o "150cc": máscara de pixels escuros dentro da área do texto
lum = a[..., :3].mean(-1)
yy, xx = np.mgrid[0:140 * S, 0:140 * S] / S
area = (xx > 36) & (xx < 107) & (yy > 66) & (yy < 117)
mask = area & (lum < 150) & (a[..., 3] > 128)
mask = np.array(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))) > 0
mask &= area
rgb = a[..., :3].copy()
known = ~mask
rgb[mask] = 0
for _ in range(900):                    # difusão: preenche com a cor vizinha da chaleira
    acc = np.zeros_like(rgb); cnt = np.zeros(rgb.shape[:2])
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        r = np.roll(np.roll(rgb, dy, 0), dx, 1); k = np.roll(np.roll(known, dy, 0), dx, 1)
        acc += r * k[..., None]; cnt += k
    fill = mask & (cnt > 0)
    rgb[fill] = acc[fill] / cnt[fill][:, None]
    known = known | fill
a[..., :3] = rgb

# 2) chaleira em brasa: puxa os tons lilás para vermelho/laranja
hsv = np.array(Image.fromarray(a[..., :3].astype(np.uint8)).convert('HSV')).astype(float)
body = (a[..., 3] > 0)
sat_boost = np.clip(hsv[..., 1] * 1.0 + 120, 0, 255)
hsv[..., 0] = np.where(body, 6, hsv[..., 0])                 # matiz vermelho-alaranjado
hsv[..., 1] = np.where(body, np.clip(sat_boost * (0.35 + 0.65 * (1 - hsv[..., 2] / 255)) + 60, 0, 255), hsv[..., 1])
hot = Image.fromarray(hsv.astype(np.uint8), 'HSV').convert('RGB')
a[..., :3] = np.array(hot).astype(float)
# vapor (canto direito superior) continua branco
steam = (xx > 95) & (yy < 50)
a[..., :3] = np.where(steam[..., None], np.array(im)[..., :3], a[..., :3])
out = Image.fromarray(a.astype(np.uint8), 'RGBA')

out.resize((280, 280), Image.LANCZOS).save(base + 'icon_200cc_tab.png')

# 3) escreve "200cc" inclinado como o original, no mesmo espaço do "150cc"
font = ImageFont.truetype('/mnt/c/Windows/Fonts/impact.ttf', 200)
fsmall = ImageFont.truetype('/mnt/c/Windows/Fonts/impact.ttf', 128)
t = Image.new('RGBA', (1400, 400), (0, 0, 0, 0))
d = ImageDraw.Draw(t)
ink = (22, 10, 8, 255)
d.text((20, 300), '200', font=font, fill=ink, anchor='ls')
w = d.textlength('200', font=font)
d.text((20 + w + 6, 300), 'cc', font=fsmall, fill=ink, anchor='ls')
t = t.crop(t.getbbox())
tw, th = 66 * S, 40 * S                  # caixa do texto original (~70 x 42 px)
t = t.resize((tw, th), Image.LANCZOS)
t = t.rotate(11, resample=Image.BICUBIC, expand=True)
cx, cy = 71 * S, 92 * S
out.alpha_composite(t, (int(cx - t.width / 2), int(cy - t.height / 2)))
final = out.resize((280, 280), Image.LANCZOS)
final.save(base + 'icon_200cc.png')
print('ok')
