import sys, UnityPy
env = UnityPy.load(sys.argv[1])
for obj in env.objects:
    if obj.type.name == "AssetBundle":
        ab = obj.read()
        print("bundle:", ab.m_Name, "assets:", len(ab.m_Container))
        for path, info in ab.m_Container:
            print(path)
