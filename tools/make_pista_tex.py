# Texturas v3 do PISO da pista do vulcão (sem nada do asfalto original do jogo).
# Gera, para o shader de estrada do jogo (GKHD/SH_ParallaxAlphaCut: _Diffuse, _Normal, _MGAE, _Height):
#   v3_estrada_*  = estrada de cinza vulcânica compactada (trilhas polidas pelos pneus, pedriscos de escória/obsidiana)
#   v3_basalto_*  = calçamento de colunas de basalto (hexágonos irregulares) com lava acesa em algumas frestas
#   v3_meiofio_*  = meio-fio de blocos de basalto alternando com blocos de lava rachada (acesa)
#   v3_ruido.png  = ruído para o corte com alfa (bordas irregulares)
# MGAE: R=metal G=brilho B=oclusão A=emissão.  Normal no formato do jogo: R=255 G=y B=y A=x.
# Rodar da raiz: .venv/bin/python tools/make_pista_tex.py [prévia_dir]
import sys
import numpy as np
from PIL import Image

OUT = "assets/"
PREV = sys.argv[1] if len(sys.argv) > 1 else None


def salvar(arr, nome, tam=None):
    arr = np.clip(arr, 0, 1)
    im = Image.fromarray((arr * 255 + 0.5).astype(np.uint8))
    if tam and im.size[0] != tam:
        im = im.resize((tam, int(round(tam * arr.shape[0] / arr.shape[1]))), Image.LANCZOS)
    im.save(OUT + nome, optimize=True)
    print("ok", nome, im.size)


def pnoise(W, H, beta, seed, ax=1.0, ay=1.0):
    """ruído periódico 1/f^beta (repete sem emenda); ax/ay esticam (ay<1 = riscos ao longo de y)"""
    rng = np.random.default_rng(seed)
    F = np.fft.fft2(rng.standard_normal((H, W)))
    ky = np.fft.fftfreq(H)[:, None] * H * ay
    kx = np.fft.fftfreq(W)[None, :] * W * ax
    k = np.sqrt(kx * kx + ky * ky); k[0, 0] = 1
    F = F / k ** beta; F[0, 0] = 0
    r = np.real(np.fft.ifft2(F))
    r = (r - r.min()) / (r.max() - r.min())
    return r.astype(np.float32)


def sm(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def borrar(h, raio):
    """borrão gaussiano periódico via FFT"""
    H, W = h.shape
    ky = np.fft.fftfreq(H)[:, None]; kx = np.fft.fftfreq(W)[None, :]
    g = np.exp(-2 * (np.pi * raio) ** 2 * (kx * kx + ky * ky))
    return np.real(np.fft.ifft2(np.fft.fft2(h) * g)).astype(np.float32)


def normal_de(h, forca):
    """altura -> normal (formato do jogo). linha 0 da imagem = v=1, então y invertido"""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * forca
    dy = (np.roll(h, 1, 0) - np.roll(h, -1, 0)) * 0.5 * forca
    n = np.dstack([-dx, -dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    x = n[..., 0] * 0.5 + 0.5; y = n[..., 1] * 0.5 + 0.5
    return np.dstack([np.ones_like(x), y, y, x]), n


def ao_de(h, raio, forte):
    cav = borrar(h, raio) - h
    return np.clip(1 - np.maximum(cav, 0) * forte, 0.25, 1)


def previa(cor, n, emis, nome):
    """render rápido iluminado (sol baixo e quente + céu) para eu conferir a textura"""
    if not PREV:
        return
    L = np.array([0.55, 0.45, 0.70]); L /= np.linalg.norm(L)
    d = np.clip((n * L).sum(2), 0, 1)[..., None]
    img = cor * (d * np.array([1.0, 0.78, 0.62]) * 1.25 + np.array([0.30, 0.24, 0.30]) * (0.5 + 0.5 * n[..., 2:3]))
    img = img + emis[..., None] * np.array([1.0, 0.45, 0.1]) * 1.6
    img = np.clip(img, 0, 1) ** (1 / 1.25)
    Image.fromarray((img * 255).astype(np.uint8)).save(PREV + "/prev_" + nome + ".png")


def carimbo(alvo, idmap, hval, cx, cy, rx, ry, ang, alt, ident):
    """pedrisco elíptico (com volta) — fica o mais alto"""
    H, W = alvo.shape
    R = int(max(rx, ry)) + 2
    ys, xs = np.mgrid[-R:R + 1, -R:R + 1].astype(np.float32)
    ys += cy - int(cy); xs += cx - int(cx)
    ca, sa = np.cos(ang), np.sin(ang)
    u = (xs * ca + ys * sa) / rx; v = (-xs * sa + ys * ca) / ry
    q = u * u + v * v
    perfil = np.where(q < 1, np.sqrt(np.clip(1 - q, 0, 1)), 0) * alt
    yy = (np.arange(-R, R + 1) + int(cy)) % H; xx = (np.arange(-R, R + 1) + int(cx)) % W
    sub = alvo[np.ix_(yy, xx)]
    novo = hval[np.ix_(yy, xx)] + perfil
    m = (q < 1) & (novo > sub)
    sub[m] = novo[m]
    alvo[np.ix_(yy, xx)] = sub
    ids = idmap[np.ix_(yy, xx)]; ids[m] = ident; idmap[np.ix_(yy, xx)] = ids


# =====================================================================================
# 1) ESTRADA DE CINZA COMPACTADA  (u = atravessa a pista, v = ao longo; 1 ladrilho = 11 m x 11 m)
# =====================================================================================
N = 2048
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
torto = (pnoise(N, N, 2.2, 11, 1, 0.25) - 0.5) * N * 0.09
trilha = np.exp(-((xx + torto - N / 2) / (0.21 * N)) ** 2)          # faixa polida no meio do ladrilho
trilha = np.clip(trilha * 1.15 - 0.08, 0, 1)
grande = pnoise(N, N, 1.9, 12)
medio = pnoise(N, N, 1.3, 13)
fino = pnoise(N, N, 0.6, 14)
risco = pnoise(N, N, 1.1, 15, 1.0, 0.07)                               # marcas de pneu ao longo da pista

h = 0.42 + (grande - 0.5) * 0.18 + (medio - 0.5) * 0.12 + (fino - 0.5) * 0.10 * (1 - 0.6 * trilha)
h -= 0.07 * trilha
pedra_id = np.zeros((N, N), np.int32)
hb = h.copy()
rng = np.random.default_rng(21)
tipos = [0]
k = 0
while k < 8500:
    cx, cy = rng.uniform(0, N, 2)
    t = trilha[int(cy), int(cx)]
    if rng.random() > 1 - 0.85 * t:          # menos pedra onde os pneus passam
        continue
    r = np.exp(rng.uniform(np.log(2.2), np.log(10.5)))
    if rng.random() < 0.04:
        r *= 1.8
    alt = r / 10.5 * (0.30 if t < 0.5 else 0.16)
    tp = rng.choice(3, p=[0.58, 0.28, 0.14])
    tipos.append(tp)
    carimbo(h, pedra_id, hb, cx, cy, r, r * rng.uniform(0.55, 1.0), rng.uniform(0, np.pi), alt, len(tipos) - 1)
    k += 1
tipos = np.array(tipos)
tp = tipos[pedra_id]
eh_pedra = pedra_id > 0

solto = np.array([0.150, 0.140, 0.140]); batido = np.array([0.265, 0.248, 0.242])
mist = np.clip(trilha * 0.9 + (medio - 0.5) * 0.5, 0, 1)[..., None]
cor = solto * (1 - mist) + batido * mist
cor = cor * (0.82 + 0.36 * grande[..., None])
ferrugem = sm(0.62, 0.85, pnoise(N, N, 1.7, 16))[..., None] * 0.18
cor = cor * (1 - ferrugem) + ferrugem * np.array([0.30, 0.17, 0.12])
grao = pnoise(N, N, 0.0, 17)
cor *= (0.86 + 0.28 * grao)[..., None]
cor *= (1 - 0.16 * trilha * sm(0.45, 0.8, risco))[..., None]          # borracha
cor += (sm(0.80, 0.97, grao) * 0.10)[..., None] * np.array([0.9, 0.85, 0.8])  # grãos claros
pal = np.array([[0, 0, 0], [0.065, 0.061, 0.066], [0.215, 0.120, 0.092], [0.27, 0.26, 0.255]], np.float32)
pc = pal[np.where(eh_pedra, tp + 1, 0)] * (0.72 + 0.56 * fino[..., None])
pc = pc * 0.75 + cor * 0.25
borda_p = np.clip((h - hb) * 30, 0, 1)[..., None]
cor = np.where(eh_pedra[..., None], pc * (0.7 + 0.3 * borda_p), cor)
ao = ao_de(h, 6, 9)
cor *= ao[..., None]
nmap, nvec = normal_de(h, 220)
brilho = 0.14 + 0.16 * trilha + np.where(eh_pedra & (tp == 0), 0.5, 0) - 0.08 * ferrugem[..., 0]
mgae = np.dstack([np.zeros_like(h), np.clip(brilho, 0, 1), ao, np.zeros_like(h)])
salvar(cor, "v3_estrada_cor.png")
salvar(nmap, "v3_estrada_nrm.png")
salvar(mgae, "v3_estrada_mgae.png", 1024)
salvar(h, "v3_estrada_alt.png", 1024)
previa(cor, nvec, np.zeros_like(h), "estrada")

# =====================================================================================
# 2) CALÇAMENTO DE BASALTO COLUNAR (hexágonos irregulares) + lava nas frestas
# =====================================================================================
C, R = 24, 28                         # 24 colunas x 28 linhas no ladrilho de 11 m (pedras de ~46 cm)
cw, rh = N / C, N / R
rng = np.random.default_rng(33)
P = np.zeros((R, C, 2), np.float32)
for r in range(R):
    for c in range(C):
        P[r, c, 0] = (c + 0.5 + 0.5 * (r % 2) + rng.uniform(-0.16, 0.16)) * cw
        P[r, c, 1] = (r + 0.5 + rng.uniform(-0.16, 0.16)) * rh
d1 = np.full((N, N), 1e9, np.float32); d2 = d1.copy()
i1 = np.zeros((N, N), np.int32); i2 = i1.copy()
p1 = np.zeros((N, N, 2), np.float32); p2 = p1.copy()
r0 = np.floor(yy / rh).astype(int); c0 = np.floor(xx / cw).astype(int)
for dr in range(-2, 3):
    for dc in range(-2, 3):
        rr = r0 + dr; cc = c0 + dc
        pr = rr % R; pcn = cc % C
        px = P[pr, pcn, 0] + (cc - pcn) * cw * C / C * 1.0
        px = P[pr, pcn, 0] + np.floor_divide(cc, C) * N
        py = P[pr, pcn, 1] + np.floor_divide(rr, R) * N
        d = np.sqrt((xx - px) ** 2 + (yy - py) ** 2)
        ident = pr * C + pcn
        m1 = d < d1
        m2 = (~m1) & (d < d2) & (ident != i1)
        # desce o 1º para 2º onde há novo 1º
        d2 = np.where(m1, d1, np.where(m2, d, d2)); i2 = np.where(m1, i1, np.where(m2, ident, i2))
        p2 = np.where(m1[..., None], p1, np.where(m2[..., None], np.dstack([px, py]), p2))
        d1 = np.where(m1, d, d1); i1 = np.where(m1, ident, i1)
        p1 = np.where(m1[..., None], np.dstack([px, py]), p1)
# distância até a fresta (bissetriz entre os dois centros mais próximos)
dv = p2 - p1
e = (d2 ** 2 - d1 ** 2) / (2 * np.maximum(np.linalg.norm(dv, axis=2), 1e-3))
ncel = R * C
cr = np.random.default_rng(34)
cel_alt = cr.uniform(-0.05, 0.05, ncel); cel_tom = cr.uniform(0.80, 1.18, ncel)
cel_tilt = cr.normal(0, 0.035, (ncel, 2)); cel_tipo = cr.choice(3, ncel, p=[0.55, 0.25, 0.20])
a_, b_ = np.minimum(i1, i2), np.maximum(i1, i2)
hsh = ((a_ * 73856093) ^ (b_ * 19349663)) % 997 / 997.0
acesa = hsh < 0.10                     # 17% das frestas com lava

ruid_g = pnoise(N, N, 1.2, 35); ruid_m = pnoise(N, N, 1.5, 36); ruid_f = pnoise(N, N, 0.5, 37)
lascas = pnoise(N, N, 1.1, 38)
g = 2.0 + 1.4 * ruid_g                 # meia-fresta
bev = 8 + 4 * ruid_m
prof = sm(g, g + bev, e)
prof = 1 - (1 - prof) ** 2.2
lasca = sm(0.66, 0.80, lascas) * sm(g + bev + 7, g, e)    # quinas lascadas
prof *= 1 - 0.55 * lasca
rel = np.dstack([xx, yy]) - p1
topo = 0.74 + cel_alt[i1] + (rel[..., 0] * cel_tilt[i1, 0] + rel[..., 1] * cel_tilt[i1, 1]) / cw \
    - 0.05 * (d1 / (0.55 * cw)) ** 2 + (ruid_f - 0.5) * 0.045 + (ruid_m - 0.5) * 0.05
poros = sm(0.78, 0.86, pnoise(N, N, 0.2, 39)) * 0.05        # vesículas do basalto
topo -= poros
fundo = 0.10 + (ruid_f - 0.5) * 0.06
hp = fundo + (topo - fundo) * prof

# cor
tons = np.array([[0.165, 0.160, 0.172], [0.190, 0.165, 0.152], [0.142, 0.142, 0.150]], np.float32)
corp = tons[cel_tipo[i1]] * cel_tom[i1][..., None]
corp *= (0.84 + 0.32 * ruid_m)[..., None]
corp *= (0.90 + 0.20 * ruid_f)[..., None]
olivina = sm(0.82, 0.95, pnoise(N, N, 0.1, 40))[..., None]
corp = corp * (1 - olivina * 0.5) + olivina * 0.5 * np.array([0.26, 0.27, 0.20])
gasto = (1 - prof) * (e > g)                               # beira gasta, mais clara/empoeirada
corp = corp + gasto[..., None] * 0.06
cinza = np.array([0.30, 0.285, 0.275]) * (0.75 + 0.5 * ruid_m[..., None])
fresta = (1 - sm(g - 1.0, g + 2.5, e))[..., None]
corp = corp * (1 - fresta) + cinza * fresta
po = sm(0.35, 0.05, hp)[..., None] * 0.5
corp = corp * (1 - po) + po * cinza
trilha_b = np.clip(np.exp(-((xx + torto - N / 2) / (0.21 * N)) ** 2) * 1.15 - 0.08, 0, 1)
corp *= (1 + 0.07 * trilha_b * prof)[..., None]
corp *= (1 - 0.12 * trilha_b * sm(0.5, 0.85, risco) * prof)[..., None]
# lava nas frestas acesas: brilho varia ao longo da fresta; pedra em volta esquenta (avermelha)
var_l = 0.45 + 0.55 * sm(0.25, 0.75, pnoise(N, N, 1.6, 41))
fogo = acesa * np.exp(-np.maximum(e - 0.5, 0) / (g * 0.9 + 0.8)) * var_l
fogo = np.where(e < g + 3, fogo, fogo * np.exp(-(e - g - 3) / 2))
calor = acesa * np.exp(-e / 12) * var_l
corp = corp * (1 - calor[..., None] * 0.35) + calor[..., None] * 0.35 * np.array([0.30, 0.10, 0.05])
lava = np.array([1.0, 0.43, 0.08]) * (0.85 + 0.3 * ruid_f[..., None])
corp = corp * (1 - fogo[..., None]) + lava * fogo[..., None]
aob = ao_de(hp, 3, 7) * (0.62 + 0.38 * prof)
corp_ao = corp * np.where(fogo[..., None] > 0.3, 1, aob[..., None])
nmapb, nvecb = normal_de(hp, 140)
brilho_b = (0.26 + 0.18 * trilha_b) * prof + 0.04
mgaeb = np.dstack([np.zeros_like(hp), np.clip(brilho_b * (1 - fogo), 0, 1), np.maximum(aob, fogo), np.clip(fogo * 1.15, 0, 1)])
salvar(corp_ao, "v3_basalto_cor.png")
salvar(nmapb, "v3_basalto_nrm.png")
salvar(mgaeb, "v3_basalto_mgae.png", 1024)
salvar(hp, "v3_basalto_alt.png", 1024)
previa(corp_ao, nvecb, fogo, "basalto")

# =====================================================================================
# 3) MEIO-FIO: bloco de basalto + bloco de lava rachada (u = 1,4 m de largura, v = 3 m)
# =====================================================================================
W2, H2 = 512, 1024
y2, x2 = np.mgrid[0:H2, 0:W2].astype(np.float32)
bloco = (y2 >= H2 / 2).astype(int)                 # 0 = basalto, 1 = lava
yl = np.mod(y2, H2 / 2)
dist_b = np.minimum.reduce([yl, H2 / 2 - yl, x2 + 40, W2 - x2])      # lados da pista ficam abertos em u=0
n1 = pnoise(W2, H2, 1.3, 51); n2 = pnoise(W2, H2, 0.5, 52); n3 = pnoise(W2, H2, 1.6, 53)
jun = 4 + 3 * n1
chanfro = sm(jun, jun + 26, dist_b)
chanfro = 1 - (1 - chanfro) ** 2
h2 = 0.12 + (0.80 + (n3 - 0.5) * 0.10 + (n2 - 0.5) * 0.05 - 0.10 * (x2 / W2) ** 2 - 0.12 * (1 - x2 / W2) ** 6) * chanfro
# rachaduras de Voronoi só no bloco de lava
rng = np.random.default_rng(55)
pts = np.dstack([rng.uniform(0, W2, 40), rng.uniform(H2 / 2, H2, 40)])[0]
D = np.sort(np.stack([np.sqrt((x2 - px) ** 2 + (y2 - py) ** 2) for px, py in pts]), axis=0)
rach = (D[1] - D[0]) * 0.5
fenda = np.exp(-rach / (2.6 + 2.5 * n1)) * bloco * chanfro
h2 -= 0.20 * sm(0.15, 0.6, fenda)
c_bas = np.array([0.11, 0.105, 0.112]) * (0.8 + 0.4 * n3[..., None]) * (0.9 + 0.2 * n2[..., None])
c_cro = np.array([0.10, 0.07, 0.065]) * (0.8 + 0.4 * n3[..., None])
cor2 = np.where(bloco[..., None] == 1, c_cro, c_bas)
borda_q = (1 - chanfro)[..., None] * (dist_b[..., None] > jun[..., None])
cor2 = cor2 + borda_q * 0.05
brilho_lava = sm(0.12, 0.75, fenda) * (0.55 + 0.45 * n3)
brasa = bloco * chanfro * sm(0.55, 0.9, pnoise(W2, H2, 1.8, 54)) * 0.35        # crosta ainda quente em manchas
emis2 = np.clip(brilho_lava + brasa * (1 - brilho_lava), 0, 1)
cor2 = cor2 * (1 - emis2[..., None]) + emis2[..., None] * np.array([1.0, 0.40, 0.07])
junta = (1 - sm(jun - 1, jun + 2, dist_b))[..., None]
cor2 = cor2 * (1 - junta) + junta * np.array([0.05, 0.045, 0.045])
ao2 = ao_de(h2, 4, 6) * (0.65 + 0.35 * chanfro)
cor2 *= np.where(emis2[..., None] > 0.25, 1, ao2[..., None])
nm2, nv2 = normal_de(h2, 120)
mg2 = np.dstack([np.zeros_like(h2), np.clip(0.30 * chanfro * (1 - emis2), 0, 1), np.maximum(ao2, emis2), emis2])
salvar(cor2, "v3_meiofio_cor.png")
salvar(nm2, "v3_meiofio_nrm.png")
salvar(mg2, "v3_meiofio_mgae.png")
salvar(h2, "v3_meiofio_alt.png")
previa(cor2, nv2, emis2, "meiofio")

# ruído para o corte com alfa
salvar(pnoise(512, 512, 1.4, 61), "v3_ruido.png")
