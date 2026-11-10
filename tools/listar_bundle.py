import sys, UnityPy, collections
env = UnityPy.load(sys.argv[1])
c = collections.Counter()
names = collections.defaultdict(list)
for obj in env.objects:
    t = obj.type.name
    c[t] += 1
    if t in ("Mesh","Texture2D","Material","GameObject","Shader") :
        try:
            d = obj.peek_name() if hasattr(obj,'peek_name') else obj.read().m_Name
        except Exception as e:
            d = "?"
        names[t].append(d)
print(dict(c))
for t in names:
    print("==", t, len(names[t]))
    if t != "GameObject" or len(sys.argv)>2:
        print(sorted(set(names[t])))
