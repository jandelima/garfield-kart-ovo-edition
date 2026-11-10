import sys, UnityPy, re
env = UnityPy.load(sys.argv[1]); out=sys.argv[2]; pat=re.compile(sys.argv[3], re.I)
for obj in env.objects:
    if obj.type.name=="Texture2D":
        d=obj.read()
        if pat.search(d.m_Name):
            try:
                im=d.image; im.thumbnail((256,256)); im.save(out+"/"+d.m_Name+".png")
            except Exception as e: print("fail",d.m_Name,e)
    if obj.type.name=="Mesh" and len(sys.argv)>4:
        d=obj.read()
        print("mesh",d.m_Name, getattr(d,'m_VertexCount',None) or len(getattr(d,'m_Vertices',[]) or [])//3)
