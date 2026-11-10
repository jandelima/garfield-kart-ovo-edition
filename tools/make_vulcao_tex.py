# Gera as texturas do vulcão: rochas do deserto recoloridas como basalto + lava + fumaça.
import numpy as np
from PIL import Image, ImageFilter
SRC="assets/vulcao_src/"; OUT="assets/"
def basalto(nome, saida, tam=1024, claro=(0.46,0.40,0.38), escuro=(0.07,0.06,0.065), gamma=1.15, contraste=1.25):
    im=Image.open(SRC+nome+".png").convert("RGBA").resize((tam,tam),Image.LANCZOS)
    a=np.asarray(im).astype(np.float32)/255
    L=(0.3*a[...,0]+0.59*a[...,1]+0.11*a[...,2])
    L=(L-L.mean())*contraste+0.5; L=np.clip(L,0,1)**gamma
    c=np.array(escuro)[None,None,:]*(1-L[...,None])+np.array(claro)[None,None,:]*L[...,None]
    out=np.concatenate([c,a[...,3:4]],-1)
    Image.fromarray((np.clip(out,0,1)*255).astype(np.uint8)).save(OUT+saida)
basalto("t_desert_red_rock1_basecolor","v_rocha_placas.png")
basalto("t_desert_rock3_basecolor","v_rocha_camadas.png")
basalto("t_desert_rock1_basecolor","v_rocha1.png")
basalto("t_desert_stone1_D","v_cinza.png",claro=(0.25,0.22,0.22),escuro=(0.07,0.06,0.06),contraste=1.1)
basalto("t_desert_red_rock1_basecolor","v_cinza2.png",claro=(0.22,0.19,0.19),escuro=(0.05,0.045,0.045),contraste=1.4)
basalto("t_desert_stone1_D","v_pedra1.png")
basalto("t_desert_stone2_D","v_pedra2.png")

# ---- lava: placas de crosta (Voronoi no toro, então repete sem emenda) com rachaduras brilhantes
rng=np.random.default_rng(7); N=512; K=38
pts=rng.random((K,2))*N
yy,xx=np.mgrid[0:N,0:N].astype(np.float32)
d=[]
for p in pts:
    dx=np.abs(xx-p[0]); dx=np.minimum(dx,N-dx); dy=np.abs(yy-p[1]); dy=np.minimum(dy,N-dy)
    d.append(np.sqrt(dx*dx+dy*dy))
d=np.sort(np.stack(d),0); borda=d[1]-d[0]           # 0 na fronteira entre placas
def ruido(esc,seed):
    r=np.random.default_rng(seed).random((N//esc,N//esc)).astype(np.float32)
    im=Image.fromarray((r*255).astype(np.uint8)).resize((N,N),Image.BICUBIC)
    return np.asarray(im).astype(np.float32)/255
n=0.5*ruido(16,1)+0.3*ruido(8,2)+0.2*ruido(4,3)
fenda=np.clip(1-(borda+ (n-0.5)*10)/14,0,1)**1.6       # 1 = rachadura
quente=np.clip(fenda*1.3+ (n-0.5)*0.4,0,1)
# paleta cartoon: crosta vinho escuro -> vermelho -> laranja -> amarelo
pal=np.array([[0.20,0.04,0.03],[0.55,0.08,0.03],[0.95,0.33,0.04],[1.0,0.62,0.10],[1.0,0.93,0.45]])
x=quente*(len(pal)-1); i=np.clip(x.astype(int),0,len(pal)-2); f=(x-i)[...,None]
cor=pal[i]*(1-f)+pal[i+1]*f
# brilho no meio das placas (crosta fina) para não ficar chapado
centro=np.clip(1-d[0]/40,0,1)[...,None]
cor=cor+centro*np.array([0.25,0.05,0.0])*(1-quente[...,None])
img=Image.fromarray((np.clip(cor,0,1)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
img.save(OUT+"v_lava.png")
# fumaça: bolinha macia branca com alfa
M=128; yy,xx=np.mgrid[0:M,0:M]; r=np.sqrt((xx-M/2)**2+(yy-M/2)**2)/(M/2)
nn=np.asarray(Image.fromarray((np.random.default_rng(3).random((16,16))*255).astype(np.uint8)).resize((M,M),Image.BICUBIC)).astype(np.float32)/255
al=np.clip(1-r,0,1)**1.5*(0.7+0.3*nn)
fum=np.dstack([np.full((M,M),1.0)]*3+[al])
Image.fromarray((fum*255).astype(np.uint8)).save(OUT+"v_fumaca.png")
print("ok")
# fumaça "couve-flor" de desenho: várias bolas macias juntas, topo mais claro
M=256; rng=np.random.default_rng(12); yy,xx=np.mgrid[0:M,0:M].astype(np.float32)
al=np.zeros((M,M),np.float32); luz=np.zeros((M,M),np.float32)
for k in range(9):
    cx,cy=rng.uniform(0.3,0.7)*M, rng.uniform(0.32,0.68)*M; r=rng.uniform(0.17,0.27)*M
    d=np.sqrt((xx-cx)**2+(yy-cy)**2)/r
    b=np.clip(1-d,0,1); b=np.clip(b*3,0,1)**0.8
    al=np.maximum(al,b)
    luz=np.maximum(luz, b*np.clip(1-(yy-(cy-r*0.4))/(r*1.6),0,1))
borda=np.clip(1-np.sqrt((xx-M/2)**2+(yy-M/2)**2)/(M*0.5),0,1)
al=al*np.clip(borda*3,0,1)
sombra=0.62+0.38*luz
rgb=np.dstack([sombra]*3)
Image.fromarray((np.dstack([rgb,al[...,None]])*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)).save(OUT+"v_fumaca.png")
print("fumaça ok")
