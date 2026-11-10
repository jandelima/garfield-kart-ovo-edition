# Texturas v2 da pista do vulcão (pintura procedural, tudo repetível sem emenda).
# Rodar da raiz: .venv/bin/python tools/make_vulcao_tex2.py
import numpy as np
from PIL import Image, ImageFilter
SRC = "assets/vulcao_src/"; OUT = "assets/"

def salvar(arr, nome):
    arr = np.clip(arr, 0, 1)
    Image.fromarray((arr * 255 + 0.5).astype(np.uint8)).save(OUT + nome)
    print("ok", nome, arr.shape)

def pnoise(N, beta, seed, M=None):
    """ruído periódico (repete sem emenda) com espectro 1/f^beta, normalizado 0..1"""
    M = M or N
    rng = np.random.default_rng(seed)
    F = np.fft.fft2(rng.standard_normal((M, N)))
    ky = np.fft.fftfreq(M)[:, None] * M; kx = np.fft.fftfreq(N)[None, :] * N
    k = np.sqrt(kx * kx + ky * ky); k[0, 0] = 1
    F = F / k ** beta; F[0, 0] = 0
    r = np.real(np.fft.ifft2(F))
    r = (r - r.min()) / (r.max() - r.min())
    return r.astype(np.float32)

def amostra(img, x, y):
    """bilinear com volta (img 2D ou 3D)"""
    H, W = img.shape[:2]
    x = np.mod(x, W); y = np.mod(y, H)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = x - x0; fy = y - y0
    x1 = (x0 + 1) % W; y1 = (y0 + 1) % H
    if img.ndim == 3: fx = fx[..., None]; fy = fy[..., None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x1] * fx * (1 - fy) +
            img[y1, x0] * (1 - fx) * fy + img[y1, x1] * fx * fy)

def rampa(t, stops):
    """t 0..1 -> cor por paradas [(pos,(r,g,b)),...]"""
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,), np.float32)
    for (p0, c0), (p1, c1) in zip(stops[:-1], stops[1:]):
        m = (t >= p0) & (t <= p1)
        f = ((t - p0) / max(p1 - p0, 1e-6))[..., None]
        out[m] = (np.array(c0) * (1 - f) + np.array(c1) * f)[m]
    return out

def lum_norm(a, lo=2, hi=98):
    L = 0.3 * a[..., 0] + 0.59 * a[..., 1] + 0.11 * a[..., 2]
    p0, p1 = np.percentile(L, lo), np.percentile(L, hi)
    return np.clip((L - p0) / (p1 - p0), 0, 1)

def fonte(nome, tam):
    im = Image.open(SRC + nome + ".png").convert("RGB").resize((tam, tam), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255

# ---------------- rochas e chão (a partir das pinturas do deserto) ----------------
T = 1024
yy, xx = np.mgrid[0:T, 0:T].astype(np.float32)
var = pnoise(T, 1.6, 5)  # variação de cor em mancha grande

# basalto em facetas (paredes, pedras): cinza-azulado frio nas luzes, marrom quente nas sombras
L = lum_norm(fonte("t_desert_red_rock1_basecolor", T))
L = np.clip(0.25 + 0.6 * L, 0, 1)
c = rampa(L, [(0, (0.08, 0.07, 0.08)), (0.35, (0.17, 0.14, 0.15)), (0.7, (0.30, 0.26, 0.27)), (1, (0.42, 0.38, 0.40))])
c *= (0.88 + 0.24 * var)[..., None]
salvar(c, "v2_rocha_facetas.png")

# basalto em estratos (penhascos, triplanar): faixas de tufo avermelhado entre camadas escuras
a = fonte("t_desert_rock3_basecolor", T)
L = lum_norm(a)
banda = pnoise(T, 2.2, 9)
c = rampa(L, [(0, (0.05, 0.04, 0.05)), (0.3, (0.15, 0.11, 0.11)), (0.62, (0.30, 0.22, 0.20)), (0.85, (0.45, 0.33, 0.28)), (1, (0.62, 0.50, 0.45))])
ox = np.clip((banda - 0.55) * 4, 0, 1)[..., None] * np.clip(L * 1.4, 0, 1)[..., None]
c = c * (1 - 0.45 * ox) + ox * 0.45 * np.array([0.50, 0.20, 0.12])
c *= (0.78 + 0.2 * var)[..., None]
c = np.rot90(c)   # o triplanar do jogo deita a textura: gira para os estratos ficarem na horizontal
salvar(np.ascontiguousarray(c), "v2_rocha_estratos.png")

# rocha sem direção (para o triplanar das encostas): facetas da rocha 1 do deserto, basalto escuro
L = lum_norm(fonte("t_desert_rock1_basecolor", T))
c = rampa(L, [(0, (0.06, 0.05, 0.055)), (0.35, (0.14, 0.11, 0.115)), (0.7, (0.26, 0.21, 0.21)), (1, (0.40, 0.33, 0.32))])
ox = np.clip((pnoise(T, 2.0, 91) - 0.6) * 4, 0, 1)[..., None]
c = c * (1 - 0.35 * ox) + ox * 0.35 * np.array([0.42, 0.17, 0.10])
c *= (0.85 + 0.25 * var)[..., None]
salvar(c, "v2_rocha_encosta.png")

# cinza vulcânica (chão aberto): estrias do vento da areia 2, quase preto-acastanhado
L = lum_norm(fonte("t_desert_sand2_basecolor", T))
c = rampa(L, [(0, (0.07, 0.06, 0.06)), (0.5, (0.15, 0.13, 0.13)), (1, (0.27, 0.24, 0.24))])
c *= (0.85 + 0.3 * var)[..., None]
salvar(c, "v2_cinza_areia.png")

# escória (chão com pedrinhas): areia 1 tem seixos -> pedrinhas de escória avermelhada
a = np.rot90(fonte("t_desert_sand2_basecolor", T))
L = lum_norm(a) * 0.7 + 0.3 * pnoise(T, 1.2, 77)
c = rampa(L, [(0, (0.05, 0.035, 0.035)), (0.4, (0.11, 0.075, 0.07)), (0.75, (0.17, 0.12, 0.11)), (1, (0.25, 0.17, 0.15))])
c *= (0.85 + 0.3 * var)[..., None]
salvar(c, "v2_escoria.png")

# pedra do acostamento: pedra 2 escurecida
L = lum_norm(fonte("t_desert_stone2_D", T))
c = rampa(L, [(0, (0.08, 0.07, 0.08)), (0.5, (0.22, 0.20, 0.21)), (1, (0.42, 0.38, 0.40))])
salvar(c, "v2_pedra.png")

# ---------------- lava ----------------
# magma (camada de baixo): correntes quentes esticadas no sentido do fluxo (v)
N = 512
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
w1 = pnoise(N, 2.0, 21); w2 = pnoise(N, 2.0, 22)
base = pnoise(N, 1.7, 23)
# estica na direção v: amostra o ruído com x comprimido
wx = xx + (w1 - 0.5) * 90; wy = yy * 0.35 + (w2 - 0.5) * 40
cor = amostra(base, wx, wy)
fil = amostra(pnoise(N, 1.2, 24), xx * 2 + (w2 - 0.5) * 60, yy * 0.5)
t = np.clip(0.55 * cor + 0.45 * fil, 0, 1)
t = np.clip((t - 0.25) / 0.6, 0, 1)
magma = rampa(t, [(0, (0.45, 0.04, 0.01)), (0.3, (0.85, 0.16, 0.02)), (0.55, (1.0, 0.42, 0.04)), (0.8, (1.0, 0.72, 0.18)), (1, (1.0, 0.93, 0.55))])
salvar(magma, "v2_lava_magma.png")

# crosta (camada de cima, RGBA): placas escuras de Voronoi periódico, bordas incandescentes, alfa 0 nas fendas
N = 1024
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
rng = np.random.default_rng(31)
K = 115
pts = rng.random((K, 2)) * N
wa = pnoise(N, 2.1, 32); wb = pnoise(N, 2.1, 33)
qx = xx + (wa - 0.5) * 70; qy = yy + (wb - 0.5) * 70
d = []
for p in pts:
    dx = np.abs(qx - p[0]); dx = np.minimum(np.mod(dx, N), N - np.mod(dx, N))
    dy = np.abs(qy - p[1]); dy = np.minimum(np.mod(dy, N), N - np.mod(dy, N))
    d.append(np.sqrt(dx * dx + dy * dy))
d = np.stack(d); idx = np.argsort(d, 0)
d0 = np.take_along_axis(d, idx[0:1], 0)[0]; d1 = np.take_along_axis(d, idx[1:2], 0)[0]
borda = d1 - d0                          # distância à fenda (px)
larg = 4 + 10 * pnoise(N, 1.8, 34)      # fendas de largura variável
fino = pnoise(N, 0.9, 35)
alfa = np.clip((borda - larg * 0.5) / (larg * 0.6), 0, 1)
# algumas placas "finas" (avermelhadas, ainda quentes)
quente_placa = rng.random(K)[idx[0]] < 0.18
# relevo: borda elevada clara (luz de cima à esquerda)
gy, gx = np.gradient(np.clip(borda / 18, 0, 1))
relevo = np.clip(-(gx + gy) * 6, -1, 1)
rugas = amostra(pnoise(N, 1.3, 36), xx * 1.0, yy * 1.0)
cbase = rampa(np.clip(0.45 * rugas + 0.35 * fino + 0.2, 0, 1),
              [(0, (0.05, 0.035, 0.035)), (0.5, (0.11, 0.08, 0.08)), (1, (0.20, 0.15, 0.15))])
cbase += relevo[..., None] * 0.07
# brilho perto da fenda
perto = np.clip(1 - (borda - larg * 0.5) / (larg * 1.8 + 6), 0, 1) ** 2
cbase = cbase * (1 - perto[..., None]) + perto[..., None] * np.array([0.75, 0.16, 0.03])
cbase[quente_placa] = cbase[quente_placa] * 0.6 + np.array([0.32, 0.06, 0.02]) * (0.6 + 0.4 * rugas[quente_placa, None])
alfa = np.where(quente_placa, alfa * 0.82, alfa)
salvar(np.dstack([cbase, alfa]), "v2_lava_crosta.png")

# ---------------- fumaça (bola de cinza pintada) ----------------
M = 256
yy, xx = np.mgrid[0:M, 0:M].astype(np.float32)
rng = np.random.default_rng(12)
al = np.zeros((M, M), np.float32); luz = np.zeros((M, M), np.float32)
for k in range(11):
    cx, cy = rng.uniform(0.28, 0.72) * M, rng.uniform(0.3, 0.7) * M; r = rng.uniform(0.15, 0.26) * M
    dd = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
    b = np.clip((1 - dd) * 3.5, 0, 1) ** 0.7
    al = np.maximum(al, b)
    luz = np.maximum(luz, b * np.clip(1 - (yy - (cy - r * 0.5)) / (r * 1.5), 0, 1))
nn = pnoise(M, 1.5, 13)
borda = np.clip(1 - np.sqrt((xx - M / 2) ** 2 + (yy - M / 2) ** 2) / (M * 0.5), 0, 1)
al = al * np.clip(borda * 3, 0, 1) * (0.85 + 0.15 * nn)
v = 0.55 + 0.45 * luz
img = np.dstack([v, v, v, al])
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.0)).save(OUT + "v2_fumaca.png")
print("ok v2_fumaca.png")

# ---------------- céu (faixa: u = volta inteira, v = do horizonte (-8°) ao zênite) ----------------
W, H = 2048, 1024
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
el = (1 - yy / (H - 1)) * 98 - 8            # elevação em graus (linha 0 = topo = 90°)
az = xx / W * 360.0                          # azimute (0 = +z, cresce para +x)
def ang(a, b):
    d = np.abs(a - b) % 360; return np.minimum(d, 360 - d)
t = np.clip(el / 90, 0, 1)
ceu = rampa(t ** 0.55, [(0, (1.0, 0.62, 0.26)), (0.12, (0.95, 0.36, 0.16)), (0.3, (0.62, 0.15, 0.14)),
                        (0.55, (0.30, 0.07, 0.12)), (0.8, (0.13, 0.04, 0.09)), (1, (0.06, 0.025, 0.06))])
# brilho do pôr do sol (oeste-noroeste) e do vulcão (norte)
SOL = 300.0; VUL = 12.0
gs = np.exp(-(ang(az, SOL) / 38) ** 2) * np.exp(-np.clip(el, 0, 90) / 14)
gv = np.exp(-(ang(az, VUL) / 30) ** 2) * np.exp(-np.clip(el, 0, 90) / 20)
longe = np.clip(1 - np.exp(-(ang(az, SOL) / 70) ** 2), 0, 1) * np.exp(-np.clip(el, 0, 90) / 10)
ceu = ceu * (1 - 0.45 * longe[..., None]) + longe[..., None] * 0.45 * np.array([0.55, 0.17, 0.12])
ceu += gs[..., None] * np.array([0.55, 0.38, 0.12]) + gv[..., None] * np.array([0.45, 0.10, 0.02])
abaixo = el < 0
ceu[abaixo] = ceu[abaixo] * 0.9
# nuvens de cinza: ruído periódico na horizontal, achatado perto do horizonte, recortado (estilo desenho)
n1 = pnoise(W, 1.9, 41, M=H); n2 = pnoise(W, 1.4, 42, M=H)
# desenha em coordenadas que achatam perto do horizonte
yc = (np.clip(el, 0, 90) / 90) ** 0.6 * H
dens = amostra(n1 * 0.7 + n2 * 0.3, xx, yc * 0.9)
faixa = np.exp(-((el - 13) / 9) ** 2) * 0.9 + np.exp(-((el - 34) / 12) ** 2) * 0.7 + np.exp(-((el - 4) / 4) ** 2) * 0.6
nuvem = np.clip((dens * faixa - 0.26) * 7.0, 0, 1)
# coluna de cinza do vulcão espalhando no alto (bigorna)
pl = np.exp(-(ang(az, VUL) / (8 + np.clip(el, 0, 90) * 0.9)) ** 2) * np.clip((el - 8) / 20, 0, 1) * np.exp(-((el - 40) / 22) ** 2)
nuvem = np.clip(nuvem + np.clip((pl * (0.6 + 0.6 * dens) - 0.35) * 3, 0, 1), 0, 1)
# borda macia + luz de baixo (lava/pôr do sol): parte de baixo da nuvem mais clara e laranja
ny = np.roll(nuvem, -10, 0)                  # nuvem um pouco acima -> esta linha é "base"
base = np.clip(nuvem - ny * 0.9, 0, 1)
calor = np.clip(gs * 1.6 + gv * 1.4 + 0.25 * np.exp(-np.clip(el, 0, 90) / 12), 0, 1)
corNuvem = rampa(np.clip(0.35 + 0.65 * (1 - t), 0, 1), [(0, (0.08, 0.04, 0.07)), (0.5, (0.22, 0.09, 0.11)), (1, (0.40, 0.15, 0.12))])
corNuvem = corNuvem + base[..., None] * calor[..., None] * np.array([1.0, 0.45, 0.12]) * 0.9
ceu = ceu * (1 - nuvem[..., None] * 0.92) + corNuvem * nuvem[..., None] * 0.92
# estrelas fracas no alto
rng = np.random.default_rng(55)
st = np.zeros((H, W), np.float32)
for _ in range(900):
    x, y = rng.integers(0, W), rng.integers(0, H // 3)
    st[y, x] = rng.uniform(0.3, 1.0)
st = np.asarray(Image.fromarray((st * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))).astype(np.float32) / 255 * 2.2
st *= np.clip((el - 35) / 30, 0, 1) * (1 - nuvem)
ceu += st[..., None] * np.array([1.0, 0.9, 0.85])
salvar(ceu, "v2_ceu.png")
