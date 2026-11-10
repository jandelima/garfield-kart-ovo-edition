# Monta o trailer a partir dos quadros gravados (cine/clips/*), legendas com alfa (cine/legendas/*) e áudio do jogo.
# Edição descrita em tools/trailer_edl.py (lista CORTES, LEGENDAS, MUSICA, SFX).
#   .venv/bin/python tools/montar_trailer.py [saida.mp4]
import os, sys, json, subprocess, shutil, importlib.util

FPS = 60
spec = importlib.util.spec_from_file_location("edl", "tools/trailer_edl.py")
edl = importlib.util.module_from_spec(spec); spec.loader.exec_module(edl)
SAIDA = sys.argv[1] if len(sys.argv) > 1 else "cine/GarfieldKart_OvoEdition_trailer.mp4"
TMP = "cine/tmp/"
os.makedirs(TMP, exist_ok=True)


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(" ".join(cmd)[:2000]); print(r.stderr[-3000:]); sys.exit(1)


# 1) cada corte vira um vídeo (frames JPG -> h264 sem perdas visíveis)
segs = []
for i, c in enumerate(edl.CORTES):
    clip, ini, n = c["clip"], c["ini"], c["n"]
    disp = len([x for x in os.listdir("cine/%s" % clip) if x.endswith(".jpg")]) - ini
    if n > disp: print("AVISO: %s tem só %d quadros a partir de %d (pedido %d)" % (clip, disp, ini, n)); sys.exit(1)
    f = TMP + "seg%02d.mp4" % i
    vf = ["scale=1920:1080:flags=lanczos", "format=yuv420p"]
    if c.get("vf"): vf.insert(0, c["vf"])
    chave = "%s|%d|%d|%s" % (clip, ini, n, c.get("vf", ""))
    if not (os.path.exists(f) and os.path.exists(f + ".key") and open(f + ".key").read() == chave):
      run(["ffmpeg", "-y", "-v", "error", "-framerate", str(FPS), "-start_number", str(ini), "-i", "cine/%s/%%05d.jpg" % clip,
         "-frames:v", str(n), "-vf", ",".join(vf), "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-r", str(FPS), f])
      open(f + ".key", "w").write(chave)
    segs.append((f, n, c.get("trans", "corte"), c.get("tdur", 0)))
    print("corte", i, clip, ini, n, flush=True)

# 2) junta os cortes (xfade nas transições)
inputs = []
for f, n, t, d in segs: inputs += ["-i", f]
filt = []
for i in range(len(segs)):
    filt.append("[%d:v]setpts=PTS-STARTPTS,fps=%d,format=yuv420p[s%d]" % (i, FPS, i))
atual = "[s0]"; dur = segs[0][1] / FPS
for i in range(1, len(segs)):
    f, n, t, d = segs[i]
    if t == "corte" or d <= 0:
        t, d = "fade", 1.0 / FPS          # corte seco = transição de 1 quadro
    off = dur - d
    filt.append("%s[s%d]xfade=transition=%s:duration=%.4f:offset=%.4f[v%d]" % (atual, i, t, d, off, i))
    dur = off + n / FPS
    atual = "[v%d]" % i
# fade de entrada e saída
filt.append("%sfade=t=in:st=0:d=0.6,fade=t=out:st=%.3f:d=1.2[vbase]" % (atual, dur - 1.2))
# 3) legendas (PNG com alfa) por cima
info = json.load(open("cine/legendas/info.json"))
vin = len(segs)
atual = "[vbase]"
for j, lg in enumerate(edl.LEGENDAS):
    nome = lg["nome"]; li = info[nome]
    inputs += ["-framerate", str(FPS), "-i", "cine/legendas/%s/%%05d.png" % nome]
    t0 = lg["t"]
    escala = lg.get("escala", 1.0)
    w = int(li["w"] * escala) // 2 * 2; h = int(li["h"] * escala) // 2 * 2
    x = lg.get("x", (1920 - w) // 2); y = lg.get("y", 760)
    if "cy" in lg: y = int(lg["cy"] - h / 2)
    filt.append("[%d:v]scale=%d:%d,setpts=PTS-STARTPTS+%.3f/TB[l%d]" % (vin, w, h, t0, j))
    filt.append("%s[l%d]overlay=x=%d:y=%d:eof_action=pass:enable='between(t,%.3f,%.3f)'[c%d]" % (atual, j, x, y, t0, t0 + li["quadros"] / FPS, j))
    atual = "[c%d]" % j
    vin += 1
filt.append("%sformat=yuv420p[vout]" % atual)

# 4) áudio: músicas e efeitos com atraso, fade e volume
ains = []
afilt = []
k = 0
for m in edl.MUSICA + edl.SFX:
    ains += ["-i", m["arq"]]
    idx = vin + k
    parts = ["atrim=start=%.3f:duration=%.3f" % (m.get("de", 0), m["dur"]), "asetpts=PTS-STARTPTS"]
    if m.get("fin", 0) > 0: parts.append("afade=t=in:st=0:d=%.3f" % m["fin"])
    if m.get("fout", 0) > 0: parts.append("afade=t=out:st=%.3f:d=%.3f" % (m["dur"] - m["fout"], m["fout"]))
    parts.append("volume=%.3f" % m.get("vol", 1.0))
    parts.append("aresample=48000,aformat=channel_layouts=stereo")
    parts.append("adelay=%d|%d" % (int(m["t"] * 1000), int(m["t"] * 1000)))
    afilt.append("[%d:a]%s[a%d]" % (idx, ",".join(parts), k))
    k += 1
afilt.append("%samix=inputs=%d:normalize=0:dropout_transition=0,alimiter=limit=0.95,atrim=duration=%.3f,afade=t=out:st=%.3f:d=1.2[aout]"
             % ("".join("[a%d]" % i for i in range(k)), k, dur, dur - 1.2))
cmd = ["ffmpeg", "-y", "-v", "error"] + inputs + ains + ["-filter_complex", ";".join(filt + afilt), "-map", "[vout]", "-map", "[aout]",
       "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-profile:v", "high", "-pix_fmt", "yuv420p", "-r", str(FPS),
       "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart", "-t", "%.3f" % dur, SAIDA]
run(cmd)
print("ok", SAIDA, "%.1f s" % dur)
