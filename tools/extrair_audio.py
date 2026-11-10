# Extrai todos os AudioClips dos pacotes do jogo para cine/audio (wav/ogg como vierem)
import sys, os, UnityPy
W="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/Garfield Kart Furious Racing_Data/StreamingAssets/Windows/"
out="cine/audio/"
for b in sorted(os.listdir(W)):
    p=W+b
    if not os.path.isfile(p) or b.endswith(".manifest"): continue
    try: env=UnityPy.load(p)
    except Exception as e: continue
    for obj in env.objects:
        if obj.type.name!="AudioClip": continue
        try:
            d=obj.read()
            for nome,dados in d.samples.items():
                f=out+b+"__"+nome
                if not f.endswith(".wav"): f+=".wav"
                open(f,"wb").write(dados)
        except Exception as e:
            print("falhou",b,getattr(d,'m_Name','?'),e)
    print("pacote",b,"ok",flush=True)
