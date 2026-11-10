import UnityPy, sys
env = UnityPy.load(sys.argv[1] + "/globalgamemanagers")
for o in env.objects:
    if o.type.name == "TagManager":
        t = o.read_typetree()
        for i, l in enumerate(t["layers"]):
            if l: print(i, l)
