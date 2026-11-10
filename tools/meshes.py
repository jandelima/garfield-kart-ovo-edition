import sys, UnityPy, re
env = UnityPy.load(sys.argv[1]); pat=re.compile(sys.argv[2], re.I)
for obj in env.objects:
    if obj.type.name=="Mesh":
        d=obj.read()
        if not pat.search(d.m_Name): continue
        a=d.m_LocalAABB; e=a.m_Extent
        nv = d.m_VertexData.m_VertexCount if hasattr(d,'m_VertexData') else 0
        print("%-28s verts=%6d size=(%.1f, %.1f, %.1f) center=(%.1f,%.1f,%.1f)"%(d.m_Name,nv,2*e.x,2*e.y,2*e.z,a.m_Center.x,a.m_Center.y,a.m_Center.z))
