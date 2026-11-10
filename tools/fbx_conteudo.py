import sys, UnityPy, re
env = UnityPy.load(sys.argv[1]); pat=re.compile(sys.argv[2], re.I)
ab=[o for o in env.objects if o.type.name=="AssetBundle"][0].read()
def walk(tr, depth, out):
    go=tr.m_GameObject.read()
    mesh=mat=""
    for c in go.m_Components:
        o=c.read() if hasattr(c,'read') else c.component.read()
        tn=o.object_reader.type.name if hasattr(o,'object_reader') else type(o).__name__
        if tn=="MeshFilter" and o.m_Mesh.m_PathID: mesh=o.m_Mesh.read().m_Name
        if tn=="MeshRenderer":
            ms=[]
            for m in o.m_Materials:
                try: ms.append(m.read().m_Name)
                except Exception: ms.append("ext")
            mat=",".join(ms)
    p=tr.m_LocalPosition; r=tr.m_LocalRotation; s=tr.m_LocalScale
    out.append("  "*depth+"%s mesh=%s mat=%s pos=(%.1f,%.1f,%.1f) rot=(%.2f,%.2f,%.2f,%.2f) scl=(%.2f,%.2f,%.2f)"%(go.m_Name,mesh,mat,p.x,p.y,p.z,r.x,r.y,r.z,r.w,s.x,s.y,s.z))
    for ch in tr.m_Children: walk(ch.read(), depth+1, out)
seen=set()
for path, info in ab.m_Container:
    if not pat.search(path) or path in seen: continue
    seen.add(path)
    a=info.asset
    if a.type.name!="GameObject": continue
    go=a.read(); tr=None
    for c in go.m_Components:
        o=c.read() if hasattr(c,'read') else c.component.read()
        if type(o).__name__=="Transform" or getattr(getattr(o,'object_reader',None),'type',None) and o.object_reader.type.name=="Transform": tr=o
    out=[]; walk(tr,0,out); print("##",path,len(out)); print("\n".join(out[:int(sys.argv[3]) if len(sys.argv)>3 else 40]))
