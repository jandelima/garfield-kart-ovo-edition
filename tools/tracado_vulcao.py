# Protótipo do traçado da pista do vulcão: spline fechada, estatísticas e vista de cima.
import numpy as np, sys
from PIL import Image, ImageDraw
CP = [  # x, z, y  (sentido horário: largada indo para o norte, na direção do vulcão)
 (-182,-40,14), (-188,40,16), (-185,130,16), (-160,190,22), (-110,215,27), (-40,240,31), (40,250,33),
 (120,225,30), (180,208,26), (212,155,21), (200,100,16), (215,45,11), (192,0,7), (168,-50,5),
 (170,-105,3), (125,-145,1), (65,-160,0.5), (5,-165,1), (-55,-160,1), (-115,-140,4), (-160,-100,9),
]
def catmull(cp, n=40):
    P=np.array(cp,float); out=[]
    m=len(P)
    for i in range(m):
        p0,p1,p2,p3=P[(i-1)%m],P[i],P[(i+1)%m],P[(i+2)%m]
        # centrípeta
        def tj(ti,a,b): return ti+np.linalg.norm((b-a)[:2])**0.5
        t0=0; t1=tj(t0,p0,p1); t2=tj(t1,p1,p2); t3=tj(t2,p2,p3)
        for t in np.linspace(t1,t2,n,endpoint=False):
            A1=(t1-t)/(t1-t0)*p0+(t-t0)/(t1-t0)*p1
            A2=(t2-t)/(t2-t1)*p1+(t-t1)/(t2-t1)*p2
            A3=(t3-t)/(t3-t2)*p2+(t-t2)/(t3-t2)*p3
            B1=(t2-t)/(t2-t0)*A1+(t-t0)/(t2-t0)*A2
            B2=(t3-t)/(t3-t1)*A2+(t-t1)/(t3-t1)*A3
            out.append((t2-t)/(t2-t1)*B1+(t-t1)/(t2-t1)*B2)
    return np.array(out)
Q=catmull(CP)
seg=np.linalg.norm(np.diff(np.vstack([Q,Q[:1]])[:,:2],axis=0),axis=1)
s=np.concatenate([[0],np.cumsum(seg)]); total=s[-1]
ss=np.arange(0,total,2.0)
X=np.interp(ss,s,np.append(Q[:,0],Q[0,0])); Z=np.interp(ss,s,np.append(Q[:,1],Q[0,1])); Y=np.interp(ss,s,np.append(Q[:,2],Q[0,2]))
print("total %.0f m, pontos %d"%(total,len(ss)))
# curvatura
P2=np.stack([X,Z],1); n=len(P2)
rmin=1e9; at=0
for i in range(n):
    a,b,c=P2[i-3],P2[i],P2[(i+3)%n]
    ab=np.linalg.norm(b-a); bc=np.linalg.norm(c-b); ca=np.linalg.norm(a-c)
    u=b-a; v=c-a; area=abs(u[0]*v[1]-u[1]*v[0])/2
    R=ab*bc*ca/(4*area) if area>1e-6 else 1e9
    if R<rmin: rmin=R; at=i
print("raio mínimo %.1f m em s=%.0f"%(rmin,ss[at]))
# declive
dy=np.abs(np.diff(np.append(Y,Y[0])))/2.0
print("declive máx %.1f%%"%(dy.max()*100))
# distância mínima entre trechos não vizinhos
dmin=1e9
for i in range(0,n,2):
    d=np.linalg.norm(P2-P2[i],axis=1)
    k=np.abs(np.arange(n)-i); k=np.minimum(k,n-k)
    d[k<40]=1e9
    if d.min()<dmin: dmin=d.min(); wi=(i,d.argmin())
print("distância mínima entre trechos %.1f m (s=%.0f e %.0f)"%(dmin,ss[wi[0]],ss[wi[1]]))
# desenho
W=900; esc=W/800.0
img=Image.new("RGB",(W,W),(30,20,20)); d=ImageDraw.Draw(img)
def tp(x,z): return (W/2+x*esc, W/2-z*esc+100)
d.ellipse([tp(40-380,650+380),tp(40+380,650-380)],outline=(120,60,40))
d.ellipse([tp(-190,-175+75),tp(190,-175-75)],outline=(255,120,0))
for i in range(n):
    c=int(min(255,Y[i]*7)); d.line([tp(X[i],Z[i]),tp(X[(i+1)%n],Z[(i+1)%n])],fill=(80+c//2,200,255-c),width=int(22*esc))
for i,(x,z,y) in enumerate(CP): d.text(tp(x,z),str(i),fill=(255,255,0))
for k in range(0,int(total),100):
    i=k//2; d.text(tp(X[i]+12,Z[i]),"%d"%k,fill=(255,255,255))
img.save(sys.argv[1] if len(sys.argv)>1 else "tracado.png")
Rs=[]
for i in range(n):
    a,b,c=P2[i-4],P2[i],P2[(i+4)%n]
    u=b-a; v=c-a; area=abs(u[0]*v[1]-u[1]*v[0])/2
    Rs.append(np.linalg.norm(b-a)*np.linalg.norm(c-b)*np.linalg.norm(a-c)/(4*area) if area>1e-6 else 1e9)
Rs=np.array(Rs)
for i in range(n):
    if Rs[i]<45 and Rs[i]==Rs[max(0,i-6):i+7].min(): print("  curva s=%d R=%.0f pos=(%.0f,%.0f) y=%.1f"%(ss[i],Rs[i],X[i],Z[i],Y[i]))
for i in range(0,n,25): print("  s=%d y=%.1f"%(ss[i],Y[i]), end="")
print()

for j,(x,z,y) in enumerate(CP):
    i=np.argmin((X-x)**2+(Z-z)**2); print("  CP%d s=%d"%(j,ss[i]))
