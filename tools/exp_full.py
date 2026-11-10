import sys, UnityPy
env = UnityPy.load(sys.argv[1]); out=sys.argv[2]; want=set(sys.argv[3:])
for obj in env.objects:
    if obj.type.name=="Texture2D":
        d=obj.read()
        if d.m_Name in want:
            d.image.save(out+"/"+d.m_Name+".png"); print(d.m_Name, d.m_Width, d.m_Height, d.m_TextureFormat)
