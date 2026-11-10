# Edição do trailer "Garfield Kart - Ovo Edition" (60 fps). Usado por tools/montar_trailer.py.
A = "cine/audio/"
FPS = 60.0

# (clip, quadro inicial, nº de quadros, transição de entrada, duração da transição)
_cortes = [
    ("aereo",   "clips2/aereo",     0,   330, "corte", 0),
    ("titulo",  "menu/titulo",      0,   270, "fadewhite", 0.35),
    ("modo200", "menu/modo200",     25,  215, "smoothleft", 0.30),
    ("largada", "clips3/largada",   175, 205, "fadeblack", 0.30),
    ("flanco",  "clips2/flanco",    10,  200, "corte", 0),
    ("lago",    "clips2/salto",     60,  150, "corte", 0),
    ("salto",   "clips4/salto",     40,  230, "corte", 0),
    ("passar",  "clips3/passarela", 20,  150, "corte", 0),
    ("passar2", "clips2/passarela", 15,  130, "corte", 0),
    ("ovohud",  "clips4/ovo1",      170, 130, "fadewhite", 0.25),
    ("acerto",  "clips4/ovo1",      405, 205, "corte", 0),
    ("escorr1", "clips2/canion",    0,   110, "corte", 0),
    ("escorr2", "clips2/ovo1",      515, 89,  "corte", 0),
    ("ggp1",    "menu/ggp1",        20,  200, "fadewhite", 0.35),
    ("ggp2",    "menu/ggp2",        10,  320, "smoothleft", 0.30),
    ("final",   "menu/titulo",      230, 310, "fadeblack", 0.6),
]

CORTES = []
INICIO = {}          # tempo (s) em que cada corte começa no filme
t = 0.0
for i, (nome, clip, ini, n, tr, td) in enumerate(_cortes):
    if i > 0:
        t -= td if tr != "corte" else 1.0 / FPS
    INICIO[nome] = t
    CORTES.append({"clip": clip, "ini": ini, "n": n, "trans": tr, "tdur": td})
    t += n / FPS
DURACAO = t

# legendas: nome, momento (s), posição y (topo da legenda), escala
LEGENDAS = [
    {"nome": "leg_pista",     "t": INICIO["aereo"] + 1.0,   "cy": 840},
    {"nome": "leg_200",       "t": INICIO["modo200"] + 0.7, "cy": 650},
    {"nome": "leg_salto",     "t": INICIO["salto"] + 0.6,   "cy": 840},
    {"nome": "leg_ovo",       "t": INICIO["ovohud"] + 0.1,  "cy": 600},
    {"nome": "leg_acertou",   "t": INICIO["acerto"] + 1.0,  "cy": 840},
    {"nome": "leg_escorrega", "t": INICIO["escorr1"] + 0.2, "cy": 840},
    {"nome": "leg_ggp",       "t": INICIO["ggp1"] + 0.9,    "cy": 600},
    {"nome": "leg_campeao",   "t": INICIO["ggp2"] + 1.6,    "cy": 560},
]

# músicas do jogo
fimA = INICIO["largada"]
fimB = INICIO["ggp1"]
MUSICA = [
    {"arq": A + "root_data__music_menu_snd_01.wav", "t": 0.0, "de": 6.0, "dur": fimA + 0.4, "fin": 0.8, "fout": 0.5, "vol": 0.9},
    {"arq": A + "audio__music_banlieue_snd_03.wav", "t": fimA - 0.15, "de": 34.0, "dur": fimB - fimA + 0.6, "fin": 0.15, "fout": 0.5, "vol": 1.4},
    {"arq": A + "audio__music_win_cup_snd_01.wav", "t": fimB - 0.1, "de": 0.0, "dur": DURACAO - fimB + 0.2, "fin": 0.05, "fout": 1.5, "vol": 0.95},
]

# efeitos
SFX = [
    {"arq": A + "audio__sfx_bonus_granted_01.wav", "t": INICIO["titulo"] + 0.45, "dur": 1.5, "vol": 0.8},
    {"arq": A + "menu_data__sfx_menu_go_01.wav" if False else A + "audio__sfx_bonus_take_01.wav", "t": INICIO["modo200"] + 0.35, "dur": 1.0, "vol": 0.7},
    {"arq": A + "audio__sfx_countdown_go_01.wav", "t": INICIO["largada"] + 0.2, "dur": 1.5, "vol": 0.7},
    {"arq": A + "audio__sfx_boost_01.wav", "t": INICIO["salto"] + 0.1, "dur": 2.0, "vol": 0.6},
    {"arq": A + "audio__sfx_bonus_roulette_01.wav", "t": INICIO["ovohud"], "dur": 1.2, "vol": 0.6},
    {"arq": A + "ovo_estouro.wav", "t": INICIO["acerto"] + (481 - 405) / FPS, "dur": 0.7, "vol": 1.0},
    {"arq": A + "audio__sfx_pie_splatch_01.wav", "t": INICIO["acerto"] + (481 - 405) / FPS, "dur": 1.2, "vol": 0.6},
    {"arq": A + "audio__sfx_bonus_granted_01.wav", "t": INICIO["ggp1"] + 0.85, "dur": 1.5, "vol": 0.7},
]
