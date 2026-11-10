// Pista nova "Oval do Garfield" para Garfield Kart Furious Racing.
// Por enquanto ela "sequestra" a pista Catz in the Hood (cenas E2C1*): quando
// essas cenas carregam, o cenário é trocado por um oval novo e a lógica de
// corrida (largada, checkpoint, caminho de renascimento, itens, turbos,
// caminhos dos bots) é reposicionada no traçado novo.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GKMod
{
    [BepInPlugin("gkmod.pistanova", "GK Pista Nova", "1.0.0")]
    public partial class PistaNova : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static bool Ativa;          // pista nova escolhida
        public static bool Montada;        // cenas atuais já foram convertidas
        public const string CENA = "E2C1"; // pista que é substituída
        public static string NOME { get { return TemVao ? "VULCÃO FERVENTE" : "OVAL DO GARFIELD"; } }

        // ---------- traçado ----------
        public const float RETA = 260f, RAIO = 50f, LARG = 22f, MARGEM = 5f, PASSO = 2f;
        public const float S_LARGADA = 70f;
        public static List<Vector3> P = new List<Vector3>(); // pontos do centro, passo PASSO, laço fechado
        public static float Total;
        static float[] SA;   // distância horizontal acumulada até cada ponto (SA[n] = Total)
        static Transform arcoLargada;

        public static PistaNova instancia;

        private void Awake()
        {
            instancia = this;
            Log = Logger;
            GerarTracado();
            new Harmony("gkmod.pistanova").PatchAll(typeof(PistaNova).Assembly);
            SceneManager.sceneLoaded += AoCarregarCena;
            Logger.LogInfo("Pista nova carregada. Comprimento da volta: " + Total.ToString("0") + " m. Ativa=" + Ativa);
        }

        public static bool Autopiloto
        {
            get { return File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_autodrive")); }
        }

        // ================= TRAÇADO =================
        public static void GerarTracado()
        {
            if (TemaVulcao) GerarTracadoVulcao(); else GerarOval();
            int n = P.Count;
            SA = new float[n + 1];
            for (int i = 0; i < n; i++) SA[i + 1] = SA[i] + D2(P[i], P[(i + 1) % n]);
            Total = SA[n];
        }

        static void GerarOval()
        {
            TemVao = false;
            P.Clear();
            var bruto = new List<Vector3>();
            float h = RETA / 2f;
            int n;
            // reta 1: (-h,-R) -> (h,-R)
            n = Mathf.RoundToInt(RETA / PASSO);
            for (int i = 0; i < n; i++) bruto.Add(new Vector3(-h + RETA * i / n, 0, -RAIO));
            // curva 1: centro (h,0), -90° -> 90°
            n = Mathf.RoundToInt(Mathf.PI * RAIO / PASSO);
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI * i / n;
                bruto.Add(new Vector3(h + RAIO * Mathf.Cos(a), 0, RAIO * Mathf.Sin(a)));
            }
            // reta 2: (h,R) -> (-h,R)
            n = Mathf.RoundToInt(RETA / PASSO);
            for (int i = 0; i < n; i++) bruto.Add(new Vector3(h - RETA * i / n, 0, RAIO));
            // curva 2: centro (-h,0), 90° -> 270°
            n = Mathf.RoundToInt(Mathf.PI * RAIO / PASSO);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI / 2f + Mathf.PI * i / n;
                bruto.Add(new Vector3(-h + RAIO * Mathf.Cos(a), 0, RAIO * Mathf.Sin(a)));
            }
            P = bruto;
        }

        // posição no centro da pista a uma distância s (com volta)
        public static Vector3 Ponto(float s)
        {
            s = Mod(s, Total);
            int lo = 0, hi = P.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (SA[mid] <= s) lo = mid; else hi = mid - 1; }
            float d = SA[lo + 1] - SA[lo];
            return Vector3.Lerp(P[lo], P[(lo + 1) % P.Count], (s - SA[lo]) / Mathf.Max(d, 1e-4f));
        }

        public static Vector3 Direcao(float s)
        {
            Vector3 d = Ponto(s + 1f) - Ponto(s - 1f);
            d.y = 0;
            return d.normalized;
        }

        public static Vector3 Direita(float s) { return Vector3.Cross(Vector3.up, Direcao(s)).normalized; }

        public static float Mod(float a, float m) { float r = a % m; return r < 0 ? r + m : r; }

        // projeta uma posição no traçado novo: devolve s
        public static float Projetar(Vector3 pos)
        {
            float best = float.MaxValue, bestS = 0, acc = 0;
            for (int i = 0; i < P.Count; i++)
            {
                Vector3 a = P[i], b = P[(i + 1) % P.Count];
                Vector3 ab = b - a; ab.y = 0; float len = ab.magnitude;
                Vector3 pa = pos - a; pa.y = 0;
                float t = Mathf.Clamp01(Vector3.Dot(pa, ab) / Mathf.Max(len * len, 1e-4f));
                Vector3 q = a + ab * t; q.y = pos.y;
                float d = (pos - q).sqrMagnitude;
                if (d < best) { best = d; bestS = acc + len * t; }
                acc += len;
            }
            return bestS;
        }

        // ================= CENAS =================
        private void AoCarregarCena(Scene cena, LoadSceneMode modo)
        {
            if (cena.name == "MenuRoot") Montada = false;
            if (!Ativa) return;
            try
            {
                if (cena.name == CENA + "_GRAPH") { if (TemVao) MontarVisualVulcao(cena); else MontarVisual(cena); }
                else if (cena.name == CENA + "_LD") MontarLogica(cena);
                else if (cena.name == CENA)
                {
                    foreach (var r in cena.GetRootGameObjects()) if (r.name.StartsWith("FX_ENV")) r.SetActive(false);
                    if (TemVao) { try { LuzVulcao(cena); } catch (Exception e) { Log.LogError("Erro na luz do vulcão: " + e); } }
                    Montada = true; Log.LogInfo("Pista nova montada.");
                }
            }
            catch (Exception e) { Log.LogError("Erro montando a pista nova (" + cena.name + "): " + e); }
        }

        static GameObject Raiz(Scene cena, string nome)
        {
            var go = new GameObject(nome);
            SceneManager.MoveGameObjectToScene(go, cena);
            return go;
        }

        static Material AcharMaterial(Scene cena, string objeto)
        {
            foreach (var r in cena.GetRootGameObjects())
                foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.name == objeto && mr.sharedMaterial != null) return mr.sharedMaterial;
            Log.LogWarning("Material de '" + objeto + "' não encontrado.");
            return null;
        }

        static Transform Achar(Scene cena, string caminho)
        {
            string[] partes = caminho.Split('/');
            foreach (var r in cena.GetRootGameObjects())
            {
                if (r.name != partes[0]) continue;
                Transform t = r.transform;
                for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
                if (t != null) return t;
            }
            return null;
        }

        // ---------- VISUAL (cena _GRAPH) ----------
        static void MontarVisual(Scene cena)
        {
            var matAsfalto = AcharMaterial(cena, "Tarmac1");
            var matGrama = AcharMaterial(cena, "Ground1");
            var matCerca = AcharMaterial(cena, "Hedge1");
            var matCalcada = AcharMaterial(cena, "Sidewalk1");
            var raiz = Raiz(cena, "GKMod_PistaNova");
            arcoLargada = null;

            // modelos para clonar antes de esconder o cenário antigo
            var arvores = new List<GameObject>();
            var casas = new List<GameObject>();
            foreach (var r in cena.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    var rr = t.GetComponentsInChildren<Renderer>(true);
                    bool fundido = false; foreach (var x in rr) if (x.isPartOfStaticBatch) fundido = true;
                    if (rr.Length == 0) continue;
                    if (t.name.StartsWith("Sub_Tree_Hi") && t.GetComponent<LODGroup>() != null)
                    {
                        if (!fundido && arvores.Count < 6) arvores.Add(t.gameObject);
                        if (arvores.Count == 0) Log.LogInfo("árvore " + t.name + " fundida=" + fundido);
                    }
                    if (t.name.StartsWith("Suburb_House") && !fundido)
                    {
                        if (casas.Count < 8) { casas.Add(t.gameObject); Log.LogInfo("modelo livre p/ clonar: " + t.name); }
                    }
                }

            // esconde o cenário antigo (mantém céu, colinas, avião e coisas bem longe)
            foreach (var r in cena.GetRootGameObjects())
            {
                string n = r.name;
                if (n == "E2C1_Sky" || n == "GKMod_PistaNova" || n.Contains("Lighting")) continue;
                if (n == "World")
                {
                    foreach (Transform c in r.transform)
                    {
                        if (c.name == "Structure" || c.name == "Biplan" || c.name == "Big_Plane_Close") continue;
                        if (c.name == "Prop")
                        {
                            foreach (Transform p in c) if (Plano(p.position).magnitude < 300f) p.gameObject.SetActive(false);
                            continue;
                        }
                        c.gameObject.SetActive(false);
                    }
                    continue;
                }
                if (Plano(r.transform.position).magnitude > 300f && r.GetComponentInChildren<Renderer>(true) != null && r.transform.childCount < 5) continue;
                r.SetActive(false);
            }

            int lAsfalto = LayerMask.NameToLayer("ColMainTarmac");
            int lGrama = LayerMask.NameToLayer("ColGrass");
            int lParede = LayerMask.NameToLayer("ColWall");

            // chão de grama (o shader mistura texturas pela cor dos vértices)
            var grama = Malha(raiz, "Grama", MalhaPlano(900f, 0.04f, -0.03f), matGrama, lGrama);
            Color corGrama = new Color(0f, 1f, 0f, 0f); // canal verde = textura de grama
            var mg = grama.GetComponent<MeshFilter>().sharedMesh;
            var cs = new Color[mg.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = corGrama; mg.colors = cs;
            // asfalto
            Malha(raiz, "Asfalto", MalhaFaixa(-LARG / 2f, LARG / 2f, 0.0f, LARG), matAsfalto, lAsfalto);
            // calçada/acostamento dos dois lados (visual, colide como grama)
            Malha(raiz, "AcostamentoE", MalhaFaixa(-LARG / 2f - MARGEM, -LARG / 2f, -0.01f, 4f), matCalcada, lGrama);
            Malha(raiz, "AcostamentoD", MalhaFaixa(LARG / 2f, LARG / 2f + MARGEM, -0.01f, 4f), matCalcada, lGrama);
            // zebras vermelho/branco
            var matZebra = new Material(Shader.Find("Sprites/Default"));
            Malha(raiz, "ZebraE", MalhaZebra(-LARG / 2f - 1.2f, -LARG / 2f + 0.2f), matZebra, lAsfalto);
            Malha(raiz, "ZebraD", MalhaZebra(LARG / 2f - 0.2f, LARG / 2f + 1.2f), matZebra, lAsfalto);
            // cercas vivas (paredes)
            foreach (float lat in new[] { LARG / 2f + MARGEM + 0.6f, -LARG / 2f - MARGEM - 0.6f })
            {
                var w = Malha(raiz, "Cerca", MalhaParede(lat, 1.2f, 1.7f), matCerca, lParede);
                w.GetComponent<MeshFilter>().sharedMesh = MalhaParedeVisual(lat, 1.2f, 1.7f);
            }

            // árvores por fora da cerca e no gramado do meio
            var rnd = new System.Random(42);
            int k = 0;
            if (arvores.Count > 0)
            {
                for (float s = 0; s < Total; s += 18f)
                {
                    float lado = LARG / 2f + MARGEM + 6f + (float)rnd.NextDouble() * 14f;
                    var pa = Ponto(s) + Direita(s) * -lado;
                    AssentarNoChao(Clonar(arvores[k++ % arvores.Count], raiz, pa, (float)rnd.NextDouble() * 360f), pa);
                }
                for (int i = 0; i < 14; i++)
                {
                    var pos = new Vector3(-RETA / 2f + RETA * (i + 0.5f) / 14f, 0, (float)(rnd.NextDouble() - 0.5) * 30f);
                    AssentarNoChao(Clonar(arvores[k++ % arvores.Count], raiz, pos, (float)rnd.NextDouble() * 360f), pos);
                }
            }
            // casas ao longo das retas, viradas para a pista
            if (casas.Count > 0)
            {
                for (int i = 0; i < 10; i++)
                {
                    bool reta1 = i < 5;
                    float x = -RETA / 2f + 30f + (RETA - 60f) * (i % 5) / 4f;
                    var pos = new Vector3(x, 0, reta1 ? -RAIO - 45f : RAIO + 45f);
                    var c = Clonar(casas[i % casas.Count], raiz, pos, 0);
                    // mantém a inclinação do modelo, gira só no eixo vertical para olhar a pista
                    var e = c.transform.rotation.eulerAngles;
                    c.transform.rotation = Quaternion.Euler(e.x, reta1 ? 0 : 180, e.z);
                    AssentarNoChao(c, pos);
                }
            }
            CriarLargada(raiz, lAsfalto);
            if (arvores.Count == 0) PlantarArvores(raiz, AcharMaterial(cena, "Fence1"), matCerca);
            Log.LogInfo("Visual da pista nova criado (" + raiz.transform.childCount + " objetos).");
        }

        static Vector3 Plano(Vector3 v) { v.y = 0; return v; }

        static Material MatCor(Color c, Texture t)
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color = c;
            if (t != null) m.mainTexture = t;
            return m;
        }

        static Texture2D Xadrez(int cols, int lins)
        {
            var t = new Texture2D(cols, lins, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Repeat;
            for (int x = 0; x < cols; x++) for (int y = 0; y < lins; y++)
                t.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : new Color(0.08f, 0.08f, 0.08f));
            t.Apply();
            return t;
        }

        static GameObject Caixa(GameObject raiz, string nome, Vector3 centro, Vector3 tamanho, Quaternion rot, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nome;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(raiz.transform, false);
            go.transform.position = centro; go.transform.rotation = rot; go.transform.localScale = tamanho;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        // árvores simples: tronco de madeira (textura de cerca) + copa de esferas (textura de cerca viva)
        static void PlantarArvores(GameObject raiz, Material madeira, Material folhas)
        {
            var rnd = new System.Random(7);
            var pontos = new List<Vector3>();
            for (float s = 0; s < Total; s += 16f)
            {
                float lado = LARG / 2f + MARGEM + 7f + (float)rnd.NextDouble() * 22f;
                pontos.Add(Ponto(s) + Direita(s) * lado);           // por fora do oval (a pista vira para a esquerda)
                if (rnd.NextDouble() < 0.45) pontos.Add(Ponto(s + 8f) + Direita(s + 8f) * (lado + 18f + (float)rnd.NextDouble() * 20f));
            }
            for (int i = 0; i < 10; i++)                              // gramado do meio
                pontos.Add(new Vector3(-RETA / 2f + 10f + (RETA - 20f) * (float)rnd.NextDouble(), 0, ((float)rnd.NextDouble() - 0.5f) * (2f * (RAIO - LARG / 2f - MARGEM - 8f))));
            var copas = new[] { new Color(1f, 1f, 1f), new Color(0.85f, 1f, 0.7f), new Color(1f, 0.9f, 0.55f) };
            foreach (var p in pontos)
            {
                float esc = 0.8f + (float)rnd.NextDouble() * 0.7f;
                var arv = new GameObject("Arvore"); arv.transform.SetParent(raiz.transform, false); arv.transform.position = p;
                var tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                UnityEngine.Object.Destroy(tronco.GetComponent<Collider>());
                tronco.transform.SetParent(arv.transform, false);
                tronco.transform.localPosition = new Vector3(0, 2.2f * esc, 0);
                tronco.transform.localScale = new Vector3(0.7f * esc, 2.2f * esc, 0.7f * esc);
                if (madeira != null) tronco.GetComponent<MeshRenderer>().sharedMaterial = madeira;
                int n = 3 + rnd.Next(3);
                for (int k = 0; k < n; k++)
                {
                    var copa = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    UnityEngine.Object.Destroy(copa.GetComponent<Collider>());
                    copa.transform.SetParent(arv.transform, false);
                    float r = (2.6f + (float)rnd.NextDouble() * 1.6f) * esc;
                    copa.transform.localPosition = new Vector3(((float)rnd.NextDouble() - 0.5f) * 2.4f * esc, (5.2f + (float)rnd.NextDouble() * 2.2f) * esc, ((float)rnd.NextDouble() - 0.5f) * 2.4f * esc);
                    copa.transform.localScale = Vector3.one * r * 2f;
                    var mr = copa.GetComponent<MeshRenderer>();
                    if (folhas != null) mr.sharedMaterial = folhas;
                }
            }
            Log.LogInfo("Árvores plantadas: " + pontos.Count);
        }

        // faixa quadriculada no chão + pórtico com faixa quadriculada e balões
        static void CriarLargada(GameObject raiz, int layerAsfalto)
        {
            float s = S_LARGADA;
            Vector3 c = Ponto(s), f = Direcao(s), r = Direita(s);
            var rot = Quaternion.LookRotation(f, Vector3.up);
            // faixa no chão (quad levemente acima do asfalto)
            var tex = Xadrez(16, 2);
            var faixa = Caixa(raiz, "FaixaLargada", c + Vector3.up * 0.03f, new Vector3(LARG, 0.02f, 2.4f), rot, MatCor(Color.white, tex));
            faixa.GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale = new Vector2(1, 1);
            // pórtico
            float meio = LARG / 2f + MARGEM + 2.2f, alt = 7f;
            var vermelho = MatCor(new Color(0.85f, 0.15f, 0.1f), null);
            var amarelo = MatCor(new Color(1f, 0.75f, 0.1f), null);
            Caixa(raiz, "PosteE", c - r * meio + Vector3.up * alt / 2f, new Vector3(1.2f, alt, 1.2f), rot, vermelho);
            Caixa(raiz, "PosteD", c + r * meio + Vector3.up * alt / 2f, new Vector3(1.2f, alt, 1.2f), rot, vermelho);
            Caixa(raiz, "Viga", c + Vector3.up * (alt - 0.3f), new Vector3(meio * 2f + 1.2f, 0.6f, 1.0f), rot, amarelo);
            var placa = Caixa(raiz, "Placa", c + Vector3.up * (alt + 1.0f), new Vector3(meio * 1.4f, 2.0f, 0.4f), rot, MatCor(Color.white, Xadrez(24, 3)));
            // balões
            var cores = new[] { new Color(1f, 0.8f, 0.1f), new Color(0.15f, 0.4f, 0.95f), new Color(0.9f, 0.2f, 0.2f) };
            for (int lado = -1; lado <= 1; lado += 2)
                for (int i = 0; i < 6; i++)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    UnityEngine.Object.Destroy(b.GetComponent<Collider>());
                    b.name = "Balao";
                    b.transform.SetParent(raiz.transform, false);
                    float ang = i * 60f * Mathf.Deg2Rad;
                    b.transform.position = c + r * (lado * meio) + Vector3.up * (alt + 0.6f + (i % 2) * 0.9f) + (r * Mathf.Cos(ang) + f * Mathf.Sin(ang)) * 1.0f;
                    b.transform.localScale = Vector3.one * 1.3f;
                    b.GetComponent<MeshRenderer>().sharedMaterial = MatCor(cores[i % 3], null);
                }
        }

        // coloca o objeto com o CENTRO da geometria no ponto pedido e a base no chão
        static void AssentarNoChao(GameObject go, Vector3 alvo)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 d = new Vector3(alvo.x - b.center.x, -0.15f - b.min.y, alvo.z - b.center.z);
            go.transform.position += d;
        }

        static GameObject Clonar(GameObject modelo, GameObject raiz, Vector3 pos, float yaw)
        {
            var go = (GameObject)UnityEngine.Object.Instantiate(modelo);
            go.transform.SetParent(raiz.transform, true);
            go.SetActive(true);
            go.transform.position = pos;
            var e = modelo.transform.rotation.eulerAngles;
            go.transform.rotation = Quaternion.Euler(e.x, e.y + yaw, e.z);
            return go;
        }

        static GameObject Malha(GameObject raiz, string nome, Mesh m, Material mat, int layer)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(raiz.transform, false);
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            return go;
        }

        static Mesh NovaMalha(List<Vector3> v, List<Vector2> uv, List<int> tri, List<Color> cor)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetUVs(1, uv);
            m.SetTriangles(tri, 0);
            if (cor != null) m.SetColors(cor); else { var c = new List<Color>(); for (int i = 0; i < v.Count; i++) c.Add(Color.white); m.SetColors(c); }
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static Mesh MalhaPlano(float tam, float escalaUv, float y)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            int n = 30; float h = tam / 2f;
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    var p = new Vector3(-h + tam * i / n, y, -h + tam * j / n);
                    v.Add(p); uv.Add(new Vector2(p.x * escalaUv, p.z * escalaUv));
                }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int a = i * (n + 1) + j, b = a + 1, c = a + n + 1, d = c + 1;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
                }
            return NovaMalha(v, uv, tri, null);
        }

        // faixa ao longo do traçado entre dois deslocamentos laterais
        static Mesh MalhaFaixa(float esq, float dir, float y, float uvLargura)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float s = 0;
            int n = P.Count;
            for (int i = 0; i <= n; i++)
            {
                int ii = i % n;
                float si = (i == n) ? Total : s;
                Vector3 c = P[ii]; Vector3 r = Direita(si);
                v.Add(c + r * esq + Vector3.up * y);
                v.Add(c + r * dir + Vector3.up * y);
                uv.Add(new Vector2(0, si / uvLargura));
                uv.Add(new Vector2(1, si / uvLargura));
                if (i < n) s += Vector3.Distance(P[ii], P[(ii + 1) % n]);
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tri.Add(a); tri.Add(a + 2); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3);
            }
            return NovaMalha(v, uv, tri, null);
        }

        static Mesh MalhaZebra(float esq, float dir)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            float s = 0; int n = P.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = P[i], b = P[(i + 1) % n];
                float d = Vector3.Distance(a, b);
                Vector3 ra = Direita(s), rb = Direita(s + d);
                Color c = ((int)(s / 3f)) % 2 == 0 ? new Color(0.85f, 0.1f, 0.1f) : Color.white;
                int k = v.Count;
                v.Add(a + ra * esq + Vector3.up * 0.02f); v.Add(a + ra * dir + Vector3.up * 0.02f);
                v.Add(b + rb * esq + Vector3.up * 0.02f); v.Add(b + rb * dir + Vector3.up * 0.02f);
                for (int q = 0; q < 4; q++) { cor.Add(c); uv.Add(Vector2.zero); }
                tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 3);
                s += d;
            }
            return NovaMalha(v, uv, tri, cor);
        }

        // parede (caixa contínua) num deslocamento lateral
        static Mesh MalhaParede(float lateral, float espessura, float altura)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float[] offs = { lateral - espessura / 2f, lateral + espessura / 2f };
            float s = 0; int n = P.Count;
            // 4 linhas de vértices por ponto: base-esq, topo-esq, topo-dir, base-dir
            for (int i = 0; i <= n; i++)
            {
                int ii = i % n; float si = (i == n) ? Total : s;
                Vector3 c = P[ii]; Vector3 r = Direita(si);
                v.Add(c + r * offs[0]); v.Add(c + r * offs[0] + Vector3.up * altura);
                v.Add(c + r * offs[1] + Vector3.up * altura); v.Add(c + r * offs[1]);
                float u = si / 3f;
                uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, 1)); uv.Add(new Vector2(u, 1.4f)); uv.Add(new Vector2(u, 0));
                if (i < n) s += Vector3.Distance(P[ii], P[(ii + 1) % n]);
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 4, b = a + 4;
                // face esquerda (0-1), topo (1-2), face direita (2-3), nos dois sentidos
                int[,] faces = { { 0, 1 }, { 1, 2 }, { 2, 3 } };
                for (int f = 0; f < 3; f++)
                {
                    int p0 = a + faces[f, 0], p1 = a + faces[f, 1], q0 = b + faces[f, 0], q1 = b + faces[f, 1];
                    tri.Add(p0); tri.Add(q0); tri.Add(p1); tri.Add(p1); tri.Add(q0); tri.Add(q1);
                    tri.Add(p0); tri.Add(p1); tri.Add(q0); tri.Add(p1); tri.Add(q1); tri.Add(q0);
                }
            }
            return NovaMalha(v, uv, tri, null);
        }

        static void Quad(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
        {
            int k = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0) { tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k); tri.Add(k + 2); tri.Add(k + 3); }
            else { tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k); tri.Add(k + 3); tri.Add(k + 2); }
        }

        // parede só para desenhar: lados e topo com vértices próprios (normais certas)
        static Mesh MalhaParedeVisual(float lateral, float espessura, float altura)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float e0 = lateral - espessura / 2f, e1 = lateral + espessura / 2f;
            float s = 0; int n = P.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = P[i], b = P[(i + 1) % n];
                float d = Vector3.Distance(a, b);
                Vector3 ra = Direita(s), rb = Direita(s + d);
                Vector3 up = Vector3.up * altura;
                float u0 = s / 3f, u1 = (s + d) / 3f;
                // lado "esquerdo" (normal -direita)
                Quad(v, uv, tri, a + ra * e0, b + rb * e0, b + rb * e0 + up, a + ra * e0 + up,
                     new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1), -ra);
                // lado "direito" (normal +direita)
                Quad(v, uv, tri, a + ra * e1, b + rb * e1, b + rb * e1 + up, a + ra * e1 + up,
                     new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1), ra);
                // topo
                Quad(v, uv, tri, a + ra * e0 + up, b + rb * e0 + up, b + rb * e1 + up, a + ra * e1 + up,
                     new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 0.4f), new Vector2(u0, 0.4f), Vector3.up);
                s += d;
            }
            return NovaMalha(v, uv, tri, null);
        }

        // ---------- LÓGICA (cena _LD) ----------
        class Antigo
        {
            public List<Vector3> pts = new List<Vector3>();
            public float Proj(Vector3 pos, out float lat, out Vector3 tan, out float altura)
            {
                float best = float.MaxValue, bestS = 0, acc = 0; lat = 0; tan = Vector3.forward; altura = 0;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector3 a = pts[i], b = pts[i + 1], ab = b - a; float len = ab.magnitude;
                    if (len < 1e-3f) continue;
                    float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / (len * len));
                    Vector3 q = a + ab * t;
                    float d = (Plano(pos) - Plano(q)).sqrMagnitude;
                    if (d < best)
                    {
                        best = d; bestS = acc + len * t;
                        tan = Plano(ab).normalized;
                        Vector3 r = Vector3.Cross(Vector3.up, tan);
                        lat = Vector3.Dot(Plano(pos - q), r);
                        altura = pos.y - q.y;
                    }
                    acc += len;
                }
                return bestS;
            }
        }

        static Antigo LerTracadoAntigo(RcMultiPath mp)
        {
            var ant = new Antigo();
            var first = mp.m_pFirstSection.GetComponent<RcMultiPathSection>();
            var visto = new HashSet<RcMultiPathSection>();
            var sec = first;
            while (sec != null && !visto.Contains(sec))
            {
                visto.Add(sec);
                foreach (Transform t in sec.transform) ant.pts.Add(t.position);
                sec = (sec.m_pAfterBranches != null && sec.m_pAfterBranches.Length > 0) ? sec.m_pAfterBranches[0] : null;
            }
            return ant;
        }

        // move um objeto para a distância s da pista nova mantendo deslocamento
        // lateral, altura e ângulo relativos ao traçado antigo
        static void Realocar(Antigo ant, Transform t, float s, bool manterLateral)
        {
            float lat, altura; Vector3 tan;
            ant.Proj(t.position, out lat, out tan, out altura);
            Vector3 nd = Direcao(s);
            float giro = Vector3.SignedAngle(tan, nd, Vector3.up);
            Vector3 pos = Ponto(s) + Direita(s) * (manterLateral ? lat : 0f) + Vector3.up * Mathf.Clamp(altura, 0f, 3f);
            Quaternion rot = Quaternion.AngleAxis(giro, Vector3.up) * t.rotation;
            t.SetPositionAndRotation(pos, rot);
        }

        static void MontarLogica(Scene cena)
        {
            var mp = UnityEngine.Object.FindObjectOfType<RcMultiPath>();
            if (mp == null) { Log.LogError("RcMultiPath não encontrado"); return; }
            var ant = LerTracadoAntigo(mp);
            Log.LogInfo("Traçado antigo lido: " + ant.pts.Count + " pontos.");

            // linha de largada + arco com balões (cena GRAPH) + grid
            var start = GameObject.Find("Start");
            GameObject arco = null;
            Vector3 arcoLocalPos = Vector3.zero; Quaternion arcoLocalRot = Quaternion.identity;
            if (start != null && arco != null)
            {
                arcoLocalPos = start.transform.InverseTransformPoint(arco.transform.position);
                arcoLocalRot = Quaternion.Inverse(start.transform.rotation) * arco.transform.rotation;
            }
            if (start != null)
            {
                Realocar(ant, start.transform, S_LARGADA, false);
                // deixa o portal em pé e alinhado com a pista
                start.transform.rotation = Quaternion.LookRotation(Direcao(S_LARGADA), Vector3.up);
                start.transform.position = Ponto(S_LARGADA) + Vector3.up * 1f;
                // grid: 8 posições em zigue-zague atrás da linha
                for (int i = 0; i < start.transform.childCount; i++)
                {
                    var c = start.transform.GetChild(i);
                    float atras = 6f + i * 3.5f;
                    float lado = (i % 2 == 0 ? -1 : 1) * 4.5f;
                    float s = S_LARGADA - atras;
                    c.position = Ponto(s) + Direita(s) * lado + Vector3.up * 1f;
                    c.rotation = Quaternion.LookRotation(Direcao(s), Vector3.up);
                }
            }
            if (start != null && arco != null)
            {
                arco.transform.position = start.transform.TransformPoint(arcoLocalPos);
                arco.transform.rotation = start.transform.rotation * arcoLocalRot;
                // encosta no chão
                var p = arco.transform.position; p.y = 0; arco.transform.position = p;
                foreach (Transform c in arco.transform)
                    Log.LogInfo("arco: " + c.name + " ativo=" + c.gameObject.activeInHierarchy + " pos=" + c.position.ToString("F1") + " rot=" + c.rotation.eulerAngles.ToString("F0") +
                        (c.GetComponent<Renderer>() != null ? " bounds=" + c.GetComponent<Renderer>().bounds.ToString() : ""));
                Log.LogInfo("largada: " + start.transform.position.ToString("F1") + " rot=" + start.transform.rotation.eulerAngles.ToString("F0"));
            }


            // checkpoint na metade da volta
            var cp = GameObject.Find("CheckPoint0");
            if (cp != null)
            {
                float s = S_LARGADA + Total / 2f;
                cp.transform.rotation = Quaternion.LookRotation(Direcao(s), Vector3.up);
                cp.transform.position = Ponto(s) + Vector3.up * 1f;
            }

            // caixas de item: grupos espalhados pela volta, em linha atravessando a pista
            float[] sItens = TemVao ? new[] { 230f, 520f, 850f, 1165f } : new[] { 160f, 340f, 560f, 760f };
            string[] grupos = { "BONUS1", "BONUS2", "BONUS3", "BONUS3b" };
            for (int g = 0; g < grupos.Length; g++)
            {
                var grp = GameObject.Find(grupos[g]);
                if (grp == null) continue;
                int n = grp.transform.childCount;
                float s = sItens[g];
                for (int i = 0; i < n; i++)
                {
                    float lado = -LARG / 2f + 2.5f + (LARG - 5f) * (n == 1 ? 0.5f : (float)i / (n - 1));
                    grp.transform.GetChild(i).position = Ponto(s) + Direita(s) * lado + Vector3.up * 1.0f;
                }
            }

            // turbos no meio das retas
            var boosts = GameObject.Find("BOOSTS");
            if (boosts != null)
            {
                float[] sB = TemVao ? new[] { S_SALTO - RAMPA - 14f, 420f } : new[] { 210f, 520f };
                for (int i = 0; i < boosts.transform.childCount; i++)
                    Realocar(ant, boosts.transform.GetChild(i), sB[i % sB.Length], false);
            }

            // peças de quebra-cabeça (contariam para a pista original): fora
            var puzzle = GameObject.Find("PUZZLE"); if (puzzle != null) puzzle.SetActive(false);

            // colisões antigas: só fica o "buraco mortal" bem lá embaixo
            var col = GameObject.Find("E2C1Col");
            if (col != null)
                foreach (Transform c in col.transform)
                    if (c.name != "ALLColKillHoleBIG") c.gameObject.SetActive(false);

            // caminho de renascimento/posições: 2 seções (metade da volta cada)
            RefazerMultiPath(mp);
            Log.LogInfo("Lógica da pista nova posicionada.");
        }

        static bool pularVao;
        static void TrocarPontos(Transform caminho, GameObject modelo, float sIni, float sFim, float passo, float lateral, float largura)
        {
            var guarda = UnityEngine.Object.Instantiate(modelo);
            guarda.SetActive(false);
            for (int i = caminho.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(caminho.GetChild(i).gameObject);
            int n = Mathf.Max(2, Mathf.RoundToInt((sFim - sIni) / passo));
            int k = 0;
            for (int i = 0; i <= n; i++)
            {
                float s = sIni + (sFim - sIni) * i / n;
                if (pularVao && NoVao(Mod(s, Total))) continue;   // nada de ponto de renascer no ar
                var p = (GameObject)UnityEngine.Object.Instantiate(guarda, caminho, false);
                p.SetActive(true);
                p.name = "Point_" + (k++);
                p.transform.position = Ponto(s) + Direita(s) * lateral + Vector3.up * 0.5f;
                p.transform.rotation = Quaternion.LookRotation(Direcao(s), Vector3.up);
                var cp = p.GetComponent<GkPathControlPoint>();
                if (cp != null) { cp.m_LeftWidth = largura + lateral; cp.m_RightWidth = largura - lateral; cp.m_MaxSpeedFactor = 100; }
            }
            UnityEngine.Object.DestroyImmediate(guarda);
        }

        static void RefazerMultiPath(RcMultiPath mp)
        {
            var secoes = mp.GetComponentsInChildren<RcMultiPathSection>(true);
            var a = mp.m_pFirstSection.GetComponent<RcMultiPathSection>();
            RcMultiPathSection b = null;
            foreach (var s in secoes) if (s != a) { b = s; break; }
            var modelo = a.transform.GetChild(0).gameObject;
            var modeloCopia = UnityEngine.Object.Instantiate(modelo);
            modeloCopia.SetActive(false);
            foreach (var s in secoes) if (s != a && s != b) UnityEngine.Object.DestroyImmediate(s.gameObject);
            float meio = S_LARGADA + Total / 2f;
            pularVao = true;
            TrocarPontos(a.transform, modeloCopia, S_LARGADA, meio, 4f, 0, LARG / 2f + MARGEM);
            TrocarPontos(b.transform, modeloCopia, meio, S_LARGADA + Total, 4f, 0, LARG / 2f + MARGEM);
            pularVao = false;
            UnityEngine.Object.DestroyImmediate(modeloCopia);
            a.m_pAfterBranches = new[] { b }; a.m_pBeforeBranches = new[] { b };
            b.m_pAfterBranches = new[] { a }; b.m_pBeforeBranches = new[] { a };
            Log.LogInfo("Caminho de renascimento refeito: " + a.transform.childCount + " + " + b.transform.childCount + " pontos.");
        }

        // ---------- bots ----------
        public static void RefazerCaminhosIA(AIPathHandler h)
        {
            int idx = 0;
            float[] laterais = { 0f, -3f, 3f, -5f, 5f, -1.5f, 1.5f };
            for (int i = h.transform.childCount - 1; i >= 0; i--)
            {
                var c = h.transform.GetChild(i);
                var pt = c.GetComponent<PathType>();
                if (pt == null) continue;
                if (pt.Type == E_PathType.SHORTCUT || c.childCount == 0) { UnityEngine.Object.DestroyImmediate(c.gameObject); continue; }
            }
            for (int i = 0; i < h.transform.childCount; i++)
            {
                var c = h.transform.GetChild(i);
                if (c.GetComponent<PathType>() == null || c.childCount == 0) continue;
                var modelo = UnityEngine.Object.Instantiate(c.GetChild(0).gameObject);
                modelo.SetActive(false);
                // caminho em laço: não repete o ponto final
                float lat = laterais[idx++ % laterais.Length];
                TrocarPontos(c, modelo, S_LARGADA, S_LARGADA + Total - 5f, 5f, lat, LARG / 2f);
                UnityEngine.Object.DestroyImmediate(modelo);
            }
            Log.LogInfo("Caminhos dos bots refeitos: " + idx + " caminhos.");
        }
    }

    [HarmonyPatch(typeof(AIPathHandler), "Start")]
    static class Patch_AIStart
    {
        static void Prefix(AIPathHandler __instance)
        {
            if (!PistaNova.Ativa || !PistaNova.Montada) return;
            try { PistaNova.RefazerCaminhosIA(__instance); }
            catch (Exception e) { PistaNova.Log.LogError("Caminhos IA: " + e); }
        }
    }

    // ---------- piloto automático de TESTE (só com BepInEx/gkmod_autodrive) ----------
    [HarmonyPatch(typeof(InputManager), "GetAction")]
    static class Patch_AutoDrive
    {
        static Kart jogador;
        static float proximoLog, proximoSalto, proximaFotoSalto; static int fotosSalto;

        static void Postfix(int localHumanPlayerId, EAction _Action, ref float __result)
        {
            if (!PistaNova.Ativa || !PistaNova.Montada || localHumanPlayerId != 0) return;
            if (_Action != EAction.Accelerate && _Action != EAction.Steer) return;
            if (!PistaNova.Autopiloto) return;
            var gm = Singleton<GameManager>.Instance.GameMode;
            if (gm == null || gm.GameState != E_GameState.Race) return;
            if (jogador == null)
                foreach (var k in UnityEngine.Object.FindObjectsOfType<Kart>())
                    if (k.GetControlType() == RcVehicle.ControlType.Human) { jogador = k; break; }
            if (jogador == null) return;
            if (_Action == EAction.Accelerate)
            {
                __result = 1f;
                // teste de queda: chega devagar na rampa para cair na lava
                if (PistaNova.TemVao && File.Exists(Path.Combine(BepInEx.Paths.BepInExRootPath, "gkmod_cair")))
                {
                    float sc = PistaNova.Projetar(jogador.transform.position);
                    if (sc > PistaNova.S_SALTO - PistaNova.RAMPA - 60f && sc < PistaNova.S_SALTO + 2f)
                    {
                        var ph2 = jogador.GetComponentInChildren<RcVehiclePhysic>();
                        __result = (ph2 != null && ph2.GetLocalSpeedKPH() > 35f) ? -0.6f : 0.6f;
                    }
                }
                return;
            }
            Vector3 pos = jogador.transform.position;
            float s = PistaNova.Projetar(pos);
            if (PistaNova.TemVao && s > PistaNova.S_SALTO + 2f && s < PistaNova.S_SALTO + 5f && !jogador.IsOnGround() && Time.unscaledTime > proximaFotoSalto)
            {
                proximaFotoSalto = Time.unscaledTime + 10f; fotosSalto++;
                ScreenCapture.CaptureScreenshot(Path.Combine(BepInEx.Paths.BepInExRootPath, "gkmod_foto_salto" + fotosSalto + ".png"));
            }
            if (PistaNova.TemVao && s > PistaNova.S_SALTO - 6f && s < PistaNova.S_SALTO + PistaNova.VAO + 14f && Time.unscaledTime > proximoSalto)
            {
                proximoSalto = Time.unscaledTime + 0.08f;
                var ph = jogador.GetComponentInChildren<RcVehiclePhysic>();
                PistaNova.Log.LogInfo(string.Format("[salto] s={0:0.0} y={1:0.00} pista.y={2:0.00} vel={3:0} chao={4}", s, pos.y, PistaNova.Ponto(s).y,
                    ph != null ? ph.GetLocalSpeedKPH() : 0, jogador.IsOnGround()));
            }
            Vector3 alvo = PistaNova.Ponto(s + 16f);
            Vector3 fwd = jogador.transform.forward; fwd.y = 0;
            Vector3 para = alvo - pos; para.y = 0;
            float ang = Vector3.SignedAngle(fwd, para, Vector3.up);
            __result = Mathf.Clamp(ang / 20f, -1f, 1f);
            if (Time.unscaledTime > proximoLog)
            {
                proximoLog = Time.unscaledTime + 2f;
                RcVehicleRaceStats st = null;
                foreach (var x in UnityEngine.Object.FindObjectsOfType<RcVehicleRaceStats>()) if (x.GetVehicle() == jogador) st = x;
                var phys = jogador.GetComponentInChildren<RcVehiclePhysic>();
                PistaNova.Log.LogInfo(string.Format("[auto] s={0:0}/{1:0} pos={2} vel={3:0} km/h volta={4} pos.corrida={5} fim={6} ang={7:0}",
                    s, PistaNova.Total, pos.ToString("F0"), phys != null ? phys.GetLocalSpeedKPH() : 0,
                    st != null ? st.GetLogicNbLap() : -1, st != null ? st.GetRank() + 1 : -1, st != null && st.IsRaceEnded(), ang));
            }
        }
    }

    [HarmonyPatch(typeof(RcVehicle), "Kill")]
    static class Patch_LogMorte
    {
        static void Prefix(RcVehicle __instance, float respawnDelay)
        {
            if (!PistaNova.Ativa || !PistaNova.Autopiloto) return;
            PistaNova.Log.LogInfo("[morte] " + __instance.name + " humano=" + (__instance.GetControlType() == RcVehicle.ControlType.Human) +
                " pos=" + __instance.transform.position.ToString("F1") + " s=" + PistaNova.Projetar(__instance.transform.position).ToString("0") + " atraso=" + respawnDelay);
        }
    }

    // ---------- menu: card "Pista Nova" na escolha de pista (Corrida Única, offline) ----------
    [HarmonyPatch(typeof(MenuHDTrackSelection), "Enter")]
    static class Patch_MenuPistas
    {
        static BetterButton card;
        public static bool Escolhendo;
        static Sprite imagem;

        static object Campo(object o, string nome) { var f = AccessTools.Field(o.GetType(), nome); return f == null ? null : f.GetValue(o); }

        static Sprite Imagem()
        {
            if (imagem != null) return imagem;
            using (var st = typeof(PistaNova).Assembly.GetManifestResourceStream("card_pista.png"))
            {
                if (st == null) return null;
                var bytes = new byte[st.Length]; st.Read(bytes, 0, bytes.Length);
                var t = new Texture2D(2, 2, TextureFormat.ARGB32, false); t.LoadImage(bytes);
                UnityEngine.Object.DontDestroyOnLoad(t);
                imagem = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return imagem;
        }

        static void Postfix(MenuHDTrackSelection __instance)
        {
            try
            {
                PistaNova.Ativa = false; // escolhendo de novo
                var cfg = Singleton<GameConfigurator>.Instance;
                bool mostrar = !GkNetMgr.Instance.IsConnected && cfg.GameModeType == E_GameModeType.SINGLE;
                var arr = Campo(Campo(__instance, "m_buttons"), "m_internalArray") as BetterButton[];
                if (arr == null || arr.Length < 5) { PistaNova.Log.LogWarning("cards de pista não encontrados"); return; }
                if (card == null) Criar(__instance, arr);
                if (card != null) card.gameObject.SetActive(mostrar);
            }
            catch (Exception e) { PistaNova.Log.LogError("Card da pista nova: " + e); }
        }

        static void Criar(MenuHDTrackSelection menu, BetterButton[] arr)
        {
            var modelo = arr[0]; var aleatorio = arr[4];
            var go = (GameObject)UnityEngine.Object.Instantiate(modelo.gameObject, modelo.transform.parent, false);
            go.name = "Card_PistaNova";
            go.transform.SetSiblingIndex(aleatorio.transform.GetSiblingIndex() + 1);
            if (go.transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
            {
                var r3 = (RectTransform)arr[3].transform; var r4 = (RectTransform)aleatorio.transform;
                ((RectTransform)go.transform).anchoredPosition = r4.anchoredPosition + (r4.anchoredPosition - r3.anchoredPosition);
            }
            card = go.GetComponent<BetterButton>();
            card.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            card.SelectAction = new BetterButton.ButtonSelectedEvent();

            var item = go.GetComponent<HD_TrackSelection_Item>();
            if (item != null)
            {
                var bg = Campo(item, "m_background") as UnityEngine.UI.Image;
                if (bg != null && bg.sprite != null) PistaNova.Log.LogInfo("card: fundo '" + bg.sprite.name + "' " + bg.sprite.rect + " rect=" + ((RectTransform)bg.transform).rect);
                if (Imagem() != null) item.ChangeBackground(Imagem());
                foreach (var nome in new[] { "m_puzzleContainer", "m_timeTrialContainer", "m_vote" })
                {
                    var g = Campo(item, nome) as GameObject; if (g != null) g.SetActive(false);
                }
                UnityEngine.Object.Destroy(item); // o jogo não mexe mais neste card
            }
            var lbl = go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (lbl != null) { lbl.gameObject.SetActive(true); lbl.text = "NOVA!"; }

            // navegação: ? <-> Pista Nova
            var nav = aleatorio.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.Explicit; nav.selectOnRight = card; aleatorio.navigation = nav;
            var nav2 = new UnityEngine.UI.Navigation(); nav2.mode = UnityEngine.UI.Navigation.Mode.Explicit; nav2.selectOnLeft = aleatorio; card.navigation = nav2;

            card.SelectAction.AddListener(() =>
            {
                var txt = Campo(menu, "m_textCupCircuit") as TMPro.TextMeshProUGUI;
                if (txt != null) txt.text = "PISTA NOVA - " + PistaNova.NOME;
            });
            card.onClick.AddListener(() =>
            {
                PistaNova.Log.LogInfo("Pista nova escolhida no menu.");
                Escolhendo = true;
                try
                {
                    menu.OnSelectChampionship(0);       // Copa Lasanha: a 1ª pista é a E2C1
                    PistaNova.GerarTracado();            // vulcão ou oval, conforme o arquivo gkmod_vulcao
                    PistaNova.Ativa = true;
                    menu.OnSubmitTrack(0);
                }
                finally { Escolhendo = false; }
            });
            var sb = new System.Text.StringBuilder();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                sb.Append(t.name).Append(':');
                foreach (var c in t.GetComponents<Component>()) if (!(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
                sb.Append(" | ");
            }
            PistaNova.Log.LogInfo("Card criado: " + sb);
        }
    }

    // escolher qualquer outra pista desliga a pista nova
    [HarmonyPatch(typeof(MenuHDTrackSelection), "OnSubmitTrack")]
    static class Patch_SubmitTrack
    {
        static void Prefix() { if (!Patch_MenuPistas.Escolhendo) PistaNova.Ativa = false; }
    }

    // nome da pista em todas as telas
    [HarmonyPatch(typeof(ChampionShipData), "TracksName", MethodType.Getter)]
    static class Patch_NomePista
    {
        static void Postfix(ChampionShipData __instance, ref string[] __result)
        {
            if (!PistaNova.Ativa || __result == null || __result.Length == 0 || __instance.Index != 0) return;
            var copia = (string[])__result.Clone();
            copia[0] = PistaNova.NOME;
            __result = copia;
        }
    }
}
