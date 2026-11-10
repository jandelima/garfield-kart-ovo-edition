# Recupera legendas com transparência a partir de duas gravações idênticas: sobre preto e sobre branco.
#   alfa = 1 - (branco - preto);  cor = preto / alfa
# Entrada: cine/clips/leg_X_preto/*.jpg e leg_X_branco/*.jpg  ->  saída: cine/legendas/leg_X/%05d.png (RGBA, recortado)
import os, sys, glob, json
import numpy as np
from PIL import Image

base = "cine/menu/"
out = "cine/legendas/"
os.makedirs(out, exist_ok=True)
info = {}
nomes = sorted(set(os.path.basename(d)[:-6] for d in glob.glob(base + "leg_*_preto")))
for nome in nomes:
    fp = sorted(glob.glob(base + nome + "_preto/*.jpg"))
    fb = sorted(glob.glob(base + nome + "_branco/*.jpg"))
    n = min(len(fp), len(fb))
    frames = []
    caixa = None
    for i in range(n):
        p = np.asarray(Image.open(fp[i])).astype(np.float32) / 255
        b = np.asarray(Image.open(fb[i])).astype(np.float32) / 255
        a = np.clip(1 - (b - p).mean(axis=2), 0, 1)
        a = np.where(a < 0.04, 0, a)                         # limpa ruído do JPG
        c = np.clip(p / np.maximum(a[..., None], 1e-3), 0, 1)
        frames.append((c, a))
        ys, xs = np.where(a > 0.05)
        if len(ys):
            bb = (xs.min(), ys.min(), xs.max(), ys.max())
            caixa = bb if caixa is None else (min(caixa[0], bb[0]), min(caixa[1], bb[1]), max(caixa[2], bb[2]), max(caixa[3], bb[3]))
    if caixa is None:
        print("vazia:", nome); continue
    x0, y0, x1, y1 = max(0, caixa[0] - 8), max(0, caixa[1] - 8), caixa[2] + 8, caixa[3] + 8
    d = out + nome + "/"
    os.makedirs(d, exist_ok=True)
    for f in glob.glob(d + "*.png"): os.remove(f)
    # começa no primeiro quadro com texto
    ini = next((i for i, (c, a) in enumerate(frames) if a.max() > 0.05), 0)
    k = 0
    for c, a in frames[ini:]:
        rgba = np.dstack([c, a])[y0:y1, x0:x1]
        Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").save(d + "%05d.png" % k)
        k += 1
    info[nome] = {"x": int(x0), "y": int(y0), "w": int(x1 - x0), "h": int(y1 - y0), "quadros": k}
    print(nome, info[nome], flush=True)
json.dump(info, open(out + "info.json", "w"), indent=1)
