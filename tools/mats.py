import sys, UnityPy, re
env = UnityPy.load(sys.argv[1]); pat=re.compile(sys.argv[2], re.I)
shaders={}
for obj in env.objects:
    if obj.type.name=="Shader":
        d=obj.read()
        try: shaders[obj.path_id]=d.m_ParsedForm.m_Name
        except Exception: shaders[obj.path_id]=getattr(d,'m_Name','?')
for obj in env.objects:
    if obj.type.name=="Material":
        d=obj.read()
        if not pat.search(d.m_Name): continue
        sh=d.m_Shader; sid=sh.m_PathID; fid=sh.m_FileID
        print("MAT",d.m_Name,"shader file",fid,"id",sid,shaders.get(sid,""))
        sp=d.m_SavedProperties
        for k,v in sp.m_TexEnvs:
            if v.m_Texture.m_PathID: 
                try: tn=v.m_Texture.read().m_Name
                except Exception: tn="ext:%d"%v.m_Texture.m_FileID
                print("   tex",k,tn,tuple(v.m_Scale.__dict__.values()) if hasattr(v.m_Scale,'__dict__') else v.m_Scale)
        print("   floats",[(k,round(v,3)) for k,v in sp.m_Floats][:12])
        print("   colors",[(k,(round(c.r,2),round(c.g,2),round(c.b,2),round(c.a,2))) for k,c in sp.m_Colors][:6])
print(shaders)
