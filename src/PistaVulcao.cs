// Pista do vulcão "MONTE LASANHA" (v5). Traçado próprio com subidas e descidas,
// terreno esculpido (planalto, cânion, lago de lava com passarela, salto sobre a
// lava), vulcão com erosão e rios de lava, lava em duas camadas (magma + crosta),
// céu pintado próprio. Usa peças e materiais originais do jogo (rochas e shaders
// do deserto) com texturas recoloridas.
// Liga com o arquivo BepInEx/gkmod_vulcao.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GKMod
{
    public class RolarTextura : MonoBehaviour
    {
        public Material m;
        public Vector2 vel;
        public float pulso, freq = 1.7f;
        Color baseCor = Color.white;
        void Start() { if (m != null) baseCor = m.color; }
        void Update()
        {
            if (m == null) return;
            m.mainTextureOffset += vel * Time.deltaTime;
            if (pulso > 0)
            {
                float k = 1f + pulso * Mathf.Sin(Time.time * freq);
                m.color = new Color(baseCor.r * k, baseCor.g * k, baseCor.b * k, baseCor.a);
            }
        }
    }

    // fumaça feita de quads que sempre olham para a câmera que está desenhando
    public class Bolha : MonoBehaviour
    {
        public float giro;
        void OnWillRenderObject()
        {
            var c = Camera.current;
            if (c != null) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position, Vector3.up) * Quaternion.Euler(0, 0, giro);
        }
    }

    public class ColunaFumaca : MonoBehaviour
    {
        public Material mat;
        public int n = 30;
        public float vida = 28f, subida = 8f, raio = 30f, tamIni = 45f, tamFim = 170f;
        public Color corBaixo = new Color(1f, 0.5f, 0.25f), corAlto = new Color(0.30f, 0.25f, 0.25f);
        public float alfa = 0.8f, mistura = 4f;
        public Vector3 vento = new Vector3(3f, 0, 0.5f);
        Transform[] t; float[] fase; Vector3[] desvio; MaterialPropertyBlock mpb; MeshRenderer[] mr;

        void Start()
        {
            var q = new Mesh();
            q.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            q.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            q.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            q.RecalculateBounds(); q.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            t = new Transform[n]; fase = new float[n]; desvio = new Vector3[n]; mr = new MeshRenderer[n];
            mpb = new MaterialPropertyBlock();
            var rnd = new System.Random(n * 7 + (int)raio);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Bolha"); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = q;
                mr[i] = go.AddComponent<MeshRenderer>(); mr[i].sharedMaterial = mat;
                go.AddComponent<Bolha>().giro = (float)rnd.NextDouble() * 360f;
                t[i] = go.transform; fase[i] = (float)i / n;
                desvio[i] = new Vector3((float)(rnd.NextDouble() - 0.5), 0, (float)(rnd.NextDouble() - 0.5)) * raio;
            }
        }

        void Update()
        {
            if (t == null) return;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Repeat(fase[i] + Time.time / vida, 1f);
                float idade = a * vida;
                t[i].position = transform.position + desvio[i] * (1f + a) + Vector3.up * (subida * idade) + vento * idade;
                float tam = Mathf.Lerp(tamIni, tamFim, Mathf.Sqrt(a));
                t[i].localScale = new Vector3(tam, tam, tam);
                float al = alfa * Mathf.Clamp01(a / 0.06f) * Mathf.Clamp01((1f - a) / 0.6f);
                var c = Color.Lerp(corBaixo, corAlto, Mathf.Clamp01(a * mistura)); c.a = al;
                mpb.SetColor("_Color", c);
                mpb.SetColor("_TintColor", c);
                mr[i].SetPropertyBlock(mpb);
            }
        }
    }

    // cúpula do céu: acompanha a câmera que vai desenhar (como um skybox)
    public class CeuSegue : MonoBehaviour
    {
        void OnEnable() { Camera.onPreCull += Seguir; }
        void OnDisable() { Camera.onPreCull -= Seguir; }
        void Seguir(Camera c)
        {
            if (c != null && this != null && (c.cullingMask & (1 << gameObject.layer)) != 0) transform.position = c.transform.position;
        }
    }

    public partial class PistaNova
    {
        public static bool TemaVulcao
        {
            get { return File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_vulcao")); }
        }

        // ================= TRAÇADO =================
        // pontos de controle x, z, y (sentido horário: a largada aponta para o vulcão)
        static readonly float[] CPV = {
            -182,-40,14, -188,40,16, -185,130,16, -160,190,22, -110,215,27, -40,240,31, 40,250,33,
            120,225,30, 180,208,26, 212,155,21, 200,100,16, 215,45,11, 192,0,7, 168,-50,5,
            170,-105,3, 125,-145,1, 65,-160,0.5f, 5,-165,1, -55,-160,1, -115,-140,4, -160,-100,9 };
        // salto sobre a lava: rampa até S_SALTO, vão de VAO metros, pouso mais baixo
        public const float S_SALTO = 1100f, VAO = 12f, RAMPA = 34f, RAMPA_ALT = 2.6f;
        public static bool TemVao;

        static float Salto(float s)
        {
            float a = S_SALTO - RAMPA;
            if (s >= a && s <= S_SALTO) { float t = (s - a) / RAMPA; return RAMPA_ALT * t * t; }
            if (s > S_SALTO && s < S_SALTO + VAO) return RAMPA_ALT * (1f - (s - S_SALTO) / VAO);
            return 0f;
        }

        public static bool NoVao(float s) { return TemVao && s > S_SALTO + 0.3f && s < S_SALTO + VAO - 0.3f; }
        public static bool NaZonaDoSalto(float s) { return TemVao && s > S_SALTO - RAMPA - 8f && s < S_SALTO + VAO + 3f; }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t0, float t1, float t2, float t3, float t)
        {
            Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        static float D2(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        static void GerarTracadoVulcao()
        {
            int m = CPV.Length / 3;
            var cp = new Vector3[m];
            for (int i = 0; i < m; i++) cp[i] = new Vector3(CPV[3 * i], CPV[3 * i + 2], CPV[3 * i + 1]);
            var denso = new List<Vector3>();
            for (int i = 0; i < m; i++)
            {
                Vector3 p0 = cp[(i - 1 + m) % m], p1 = cp[i], p2 = cp[(i + 1) % m], p3 = cp[(i + 2) % m];
                float t0 = 0, t1 = t0 + Mathf.Sqrt(D2(p0, p1)), t2 = t1 + Mathf.Sqrt(D2(p1, p2)), t3 = t2 + Mathf.Sqrt(D2(p2, p3));
                for (int k = 0; k < 80; k++) denso.Add(CatmullRom(p0, p1, p2, p3, t0, t1, t2, t3, Mathf.Lerp(t1, t2, k / 80f)));
            }
            denso.Add(denso[0]);
            // reamostra a cada PASSO metros (distância horizontal)
            var res = new List<Vector3>();
            float alvo = 0, acc = 0;
            for (int i = 0; i + 1 < denso.Count; i++)
            {
                float d = D2(denso[i], denso[i + 1]);
                while (alvo <= acc + d && d > 1e-5f)
                {
                    res.Add(Vector3.Lerp(denso[i], denso[i + 1], (alvo - acc) / d));
                    alvo += PASSO;
                }
                acc += d;
            }
            if (D2(res[res.Count - 1], res[0]) < PASSO * 0.5f) res.RemoveAt(res.Count - 1);
            for (int i = 0; i < res.Count; i++) { var p = res[i]; p.y += Salto(i * PASSO); res[i] = p; }
            P = res;
            TemVao = true;
        }

        // ================= ÍNDICE ESPACIAL DO TRAÇADO =================
        const float GC = 20f, G0 = -700f; const int GN = 70;
        static List<int>[] grade;

        static void IndexarTracado()
        {
            grade = new List<int>[GN * GN];
            for (int i = 0; i < P.Count; i++)
            {
                int cx = (int)((P[i].x - G0) / GC), cz = (int)((P[i].z - G0) / GC);
                if (cx < 0 || cz < 0 || cx >= GN || cz >= GN) continue;
                var l = grade[cx * GN + cz]; if (l == null) grade[cx * GN + cz] = l = new List<int>();
                l.Add(i);
            }
        }

        // distância horizontal ao centro da pista; devolve s e altura da pista no ponto mais perto
        static bool PertoPista(float x, float z, float raio, out float dist, out float s, out float y)
        {
            dist = 1e9f; s = 0; y = 0;
            int cx = (int)((x - G0) / GC), cz = (int)((z - G0) / GC), rc = Mathf.CeilToInt(raio / GC) + 1;
            int n = P.Count; bool achou = false;
            for (int a = cx - rc; a <= cx + rc; a++)
                for (int b = cz - rc; b <= cz + rc; b++)
                {
                    if (a < 0 || b < 0 || a >= GN || b >= GN) continue;
                    var l = grade[a * GN + b]; if (l == null) continue;
                    foreach (int i in l)
                        for (int q = -1; q <= 0; q++)
                        {
                            int i0 = (i + q + n) % n, i1 = (i0 + 1) % n;
                            Vector3 p0 = P[i0], p1 = P[i1];
                            float ex = p1.x - p0.x, ez = p1.z - p0.z, L2 = ex * ex + ez * ez;
                            float t = Mathf.Clamp01(((x - p0.x) * ex + (z - p0.z) * ez) / Mathf.Max(L2, 1e-6f));
                            float qx = p0.x + ex * t - x, qz = p0.z + ez * t - z;
                            float d = Mathf.Sqrt(qx * qx + qz * qz);
                            if (d < dist) { dist = d; s = (i0 + t) * PASSO; y = Mathf.Lerp(p0.y, p1.y, t); achou = true; }
                        }
                }
            return achou && dist <= raio;
        }

        // ================= TERRENO =================
        static readonly Vector3 VC = new Vector3(60f, 0f, 680f);   // vulcão
        const float VRB = 420f, VH = 245f, VRC = 88f, VCRAT = 58f; // raio base, altura, raio cratera, fundo
        const float APRON = 46f;
        const float LAVA_Y = -3f, FUNDO_LAGO = -10f;
        const float FAIXA_PLANA = 17.5f;
        static readonly float TH_BRECHA = -90f * Mathf.Deg2Rad;    // cratera aberta para o sul (para a pista)
        static readonly float[] FLUXOS = { -90f, -68f, -114f, -46f, -136f }; // graus
        static readonly float[] FLUXO_FIM = { 365f, 330f, 345f, 300f, 310f };
        // rio de lava que desce do planalto (cachoeira) até o lago
        static readonly Vector3[] RIO = { new Vector3(8, 0, 40), new Vector3(10, 0, 16), new Vector3(12, 0, -4), new Vector3(15, 0, -22),
            new Vector3(19, 0, -48), new Vector3(23, 0, -75), new Vector3(24, 0, -100) };
        static readonly Vector3 MESA = new Vector3(10f, 0f, 62f);

        static float Sm(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float Ruido(float x, float z, float esc) { return Mathf.PerlinNoise(x * esc + 31.7f, z * esc + 77.1f); }
        static float Fbm(float x, float z, int oct)
        {
            float a = 0.5f, f = 1f, s = 0, n = 0;
            for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 13.1f * i + 3.3f, z * f + 7.7f * i + 1.9f); n += a; a *= 0.5f; f *= 2.03f; }
            return s / n;
        }
        static float Crista(float x, float z, int oct)
        {
            float a = 0.5f, f = 1f, s = 0, n = 0;
            for (int i = 0; i < oct; i++) { float r = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f + 5.1f * i, z * f + 9.3f * i) * 2f - 1f); s += a * r * r; n += a; a *= 0.5f; f *= 2.1f; }
            return s / n;
        }
        static float AngDif(float a, float b) { float d = Mathf.Repeat(a - b + Mathf.PI, Mathf.PI * 2f) - Mathf.PI; return d; }

        static float Elipse(float x, float z, float cx, float cz, float rx, float rz)
        {
            float dx = (x - cx) / rx, dz = (z - cz) / rz;
            float e = dx * dx + dz * dz + (Ruido(x, z, 0.018f) - 0.5f) * 0.45f;
            return 1f - Sm(0.6f, 1.12f, e);
        }

        static float DistSeg(float x, float z, Vector3 a, Vector3 b, out float t)
        {
            float ex = b.x - a.x, ez = b.z - a.z, L2 = ex * ex + ez * ez;
            t = Mathf.Clamp01(((x - a.x) * ex + (z - a.z) * ez) / Mathf.Max(L2, 1e-6f));
            float qx = a.x + ex * t - x, qz = a.z + ez * t - z;
            return Mathf.Sqrt(qx * qx + qz * qz);
        }

        static float DistRio(float x, float z, out float u)
        {
            float best = 1e9f; u = 0; float acc = 0;
            for (int i = 0; i + 1 < RIO.Length; i++)
            {
                float t; float d = DistSeg(x, z, RIO[i], RIO[i + 1], out t);
                float len = D2(RIO[i], RIO[i + 1]);
                if (d < best) { best = d; u = acc + len * t; }
                acc += len;
            }
            return best;
        }

        static float FluxoTheta(int k, float r) { return FLUXOS[k] * Mathf.Deg2Rad + 0.06f * Mathf.Sin(r * 0.021f + k * 1.7f) + 0.03f * Mathf.Sin(r * 0.05f + k); }
        static float FluxoLarg(int k, float r) { float u = Mathf.Clamp01((r - VRC) / (VRB - VRC)); return 5f + 12f * u + 2f * Mathf.Sin(r * 0.08f + k); }

        static float Cone(float r, float th)
        {
            if (r >= VRB) return 0f;
            float brecha = Mathf.Exp(-Mathf.Pow(AngDif(th, TH_BRECHA) / 0.16f, 2f));
            float hRim = VH * (1f + 0.025f * Mathf.Sin(th * 5f + 1.3f) + 0.016f * Mathf.Sin(th * 11f + 0.4f) + 0.01f * Mathf.Sin(th * 23f)) - 26f * brecha;
            if (r < VRC)
            {
                float t = r / VRC;
                return hRim - VCRAT * (1f - t * t * t) + (Ruido(r * Mathf.Cos(th), r * Mathf.Sin(th), 0.05f) - 0.5f) * 4f * t;
            }
            float k = (r - VRC) / (VRB - VRC);
            float h = hRim * Mathf.Pow(1f - k, 2.05f) * (1f + 0.35f * k * (1f - k));
            // erosão: sulcos radiais que serpenteiam
            float wp = (Mathf.PerlinNoise(Mathf.Cos(th) * 2.3f + r * 0.0035f + 4f, Mathf.Sin(th) * 2.3f + 8f) - 0.5f) * 5f;
            float g = Mathf.Abs(Mathf.Sin(th * 21f + wp));
            float g2 = Mathf.Abs(Mathf.Sin(th * 47f + wp * 1.7f + r * 0.01f));
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.65f));
            h += (Mathf.Sqrt(g) - 0.62f) * 22f * env + (Mathf.Sqrt(g2) - 0.6f) * 6f * env;
            h += (Fbm(r * Mathf.Cos(th) * 0.012f, r * Mathf.Sin(th) * 0.012f, 3) - 0.5f) * 16f * env;
            // canais dos rios de lava
            for (int f = 0; f < FLUXOS.Length; f++)
            {
                if (r > FLUXO_FIM[f] + 10f) continue;
                float dl = Mathf.Abs(AngDif(th, FluxoTheta(f, r))) * r;
                float w = FluxoLarg(f, r);
                if (dl < w * 1.8f) h -= 4.5f * (1f - Sm(w * 0.45f, w * 1.8f, dl)) * Sm(VRC, VRC + 25f, r) * (1f - Sm(FLUXO_FIM[f] - 15f, FLUXO_FIM[f] + 10f, r));
            }
            return h;
        }

        static float AlturaNatural(float x, float z)
        {
            float h = (Fbm(x * 0.0042f, z * 0.0042f, 4) - 0.5f) * 26f;
            h += (Mathf.PerlinNoise(x * 0.04f + 5.3f, z * 0.04f + 1.7f) - 0.5f) * 2.4f;
            float dx = x - VC.x, dz = z - VC.z, rv = Mathf.Sqrt(dx * dx + dz * dz);
            h += APRON * (1f - Sm(280f, 980f, rv));
            if (rv < VRB) h += Cone(rv, Mathf.Atan2(dz, dx));
            // poças de lava no pé dos rios do vulcão
            // anel de montanhas ao longe
            float mx = x, mz = z - 80f, R = Mathf.Sqrt(mx * mx + mz * mz);
            float anel = Sm(560f, 1150f, R);
            if (anel > 0f) h += anel * (40f + 230f * Crista(x * 0.0035f + 10f, z * 0.0035f + 4f, 4)) * (0.6f + 0.4f * anel);
            // planalto (mesa) no meio da pista, com bacia de lava em cima
            float rm = D2(new Vector3(x, 0, z), MESA) + (Ruido(x, z, 0.018f) - 0.5f) * 40f + (Ruido(x + 70f, z, 0.06f) - 0.5f) * 10f;
            float mesa = 1f - Sm(50f, 74f, rm);
            mesa = Mathf.Max(mesa * 0.55f, mesa * mesa);    // degrau no meio da encosta
            if (mesa > 0f)
            {
                float topo = 36f + (Fbm(x * 0.03f, z * 0.03f, 2) - 0.5f) * 6f;
                topo -= 5f * (1f - Sm(10f, 22f, D2(new Vector3(x, 0, z), RIO[0])));
                h += mesa * topo;
            }
            // paredes do cânion (leste) e morro entre o cânion e o planalto
            float tt;
            float dc = DistSeg(x, z, new Vector3(290, 0, -40), new Vector3(278, 0, 230), out tt);
            h += 52f * (1f - Sm(25f, 85f, dc)) * (0.75f + 0.5f * Fbm(x * 0.02f, z * 0.02f, 2));
            float dm = DistSeg(x, z, new Vector3(130, 0, 40), new Vector3(140, 0, 150), out tt);
            h += 34f * (1f - Sm(20f, 55f, dm)) * (0.7f + 0.6f * Fbm(x * 0.025f + 3f, z * 0.025f, 2));
            // morros atrás da reta de largada (lado de dentro)
            float dl2 = DistSeg(x, z, new Vector3(-110, 0, -20), new Vector3(-95, 0, 140), out tt);
            h += 18f * (1f - Sm(15f, 45f, dl2));
            // rio do planalto até o lago: sulco
            float u; float dr = DistRio(x, z, out u);
            if (dr < 16f && u > 20f) h -= 3.5f * (1f - Sm(4f, 16f, dr));
            // lagos de lava: grande ao sul (com a passarela) e no vale a oeste
            float lk = Mathf.Max(Elipse(x, z, 0f, -168f, 205f, 92f), Elipse(x, z, -345f, 50f, 105f, 175f));
            lk = Mathf.Max(lk, 0.9f * Elipse(x, z, 24f, -105f, 18f, 30f));
            h = Mathf.Lerp(h, FUNDO_LAGO, lk);
            return h;
        }

        // largura da rampa de terreno ao lado da pista (curta = barranco/penhasco)
        static float Barranco(float s)
        {
            if (s > 1000f && s < 1210f) return 9f;          // passarela sobre o lago
            if (s > 640f && s < 800f) return 11f;           // cânion
            if (s < 175f || s > 1340f) return 12f;          // reta da largada (vale de lava a oeste)
            return 24f;
        }

        static float Altura(float x, float z)
        {
            float h = AlturaNatural(x, z);
            float d, s, y;
            if (PertoPista(x, z, 70f, out d, out s, out y))
            {
                float w = 1f - Sm(FAIXA_PLANA, FAIXA_PLANA + Barranco(s), d);
                if (NoVao(s)) w = 0f;
                h = Mathf.Lerp(h, y - 0.32f, w);
            }
            return h;
        }

        static Color CorTerreno(float x, float y, float z, Vector3 n, float dPista)
        {
            float inc = 1f - n.y;
            float rocha = Sm(0.22f, 0.42f, inc + (Ruido(x, z, 0.05f) - 0.5f) * 0.12f);
            float cascalho = Sm(0.42f, 0.68f, Ruido(x + 300f, z, 0.012f) + Ruido(x, z, 0.06f) * 0.25f);
            cascalho = Mathf.Max(cascalho, 1f - Sm(20f, 34f, dPista)) * (1f - rocha);
            if (y < LAVA_Y + 2.5f) { rocha = Mathf.Max(rocha, 0.6f); cascalho *= 0.4f; }   // margem de basalto junto da lava
            float cinza = Mathf.Max(0f, 1f - rocha - cascalho);
            return new Color(rocha, cinza, cascalho, 0f);
        }

        static Mesh MalhaTerreno(float x0, float z0, float x1, float z1, float passo, bool externo)
        {
            int nx = Mathf.RoundToInt((x1 - x0) / passo) + 1, nz = Mathf.RoundToInt((z1 - z0) / passo) + 1;
            var v = new List<Vector3>(nx * nz); var uv = new List<Vector2>(nx * nz); var tri = new List<int>();
            var dp = new float[nx * nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = x0 + i * passo, z = z0 + j * passo;
                    float h;
                    float dd = 1e9f;
                    bool dentro = x > TX0 + passo && x < TX1 - passo && z > TZ0 + passo && z < TZ1 - passo;
                    if (externo && dentro) h = AlturaNatural(x, z) - 3f;
                    else
                    {
                        h = AlturaNatural(x, z);
                        float d, s, y;
                        if (!externo && PertoPista(x, z, 70f, out d, out s, out y))
                        {
                            dd = d;
                            float w = 1f - Sm(FAIXA_PLANA, FAIXA_PLANA + Barranco(s), d);
                            if (NoVao(s)) w = 0f;
                            h = Mathf.Lerp(h, y - 0.32f, w);
                        }
                    }
                    // debaixo do cone (que tem malha própria) afunda um pouco
                    float rv = D2(new Vector3(x, 0, z), VC);
                    if (rv < VRB) h -= Mathf.Clamp((VRB - rv) * 0.3f, 0f, 10f);
                    v.Add(new Vector3(x, h, z)); uv.Add(new Vector2(x / 900f + 0.5f, z / 900f + 0.5f));
                    dp[i * nz + j] = dd;
                }
            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                {
                    if (externo)
                    {
                        float cx = x0 + (i + 0.5f) * passo, cz = z0 + (j + 0.5f) * passo;
                        if (cx > TX0 + passo * 1.5f && cx < TX1 - passo * 1.5f && cz > TZ0 + passo * 1.5f && cz < TZ1 - passo * 1.5f) continue;
                    }
                    int a = i * nz + j, b = a + 1, c = a + nz, d = c + 1;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
                }
            var m = NovaMalha(v, uv, tri, null);
            var nr = m.normals;
            if (nr.Length > 0 && nr[nr.Length / 2].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); nr = m.normals; }
            var cor = new Color[v.Count];
            for (int i = 0; i < v.Count; i++) cor[i] = CorTerreno(v[i].x, v[i].y, v[i].z, nr[i], dp[i]);
            m.colors = cor;
            return m;
        }
        const float TX0 = -440f, TX1 = 440f, TZ0 = -400f, TZ1 = 480f;

        // cone do vulcão com malha radial (mais detalhe perto da cratera)
        static Mesh MalhaCone()
        {
            int NA = 360, NR = 150;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int j = 0; j <= NR; j++)
            {
                float r = VRB * Mathf.Pow((float)j / NR, 1.12f);
                for (int i = 0; i <= NA; i++)
                {
                    float th = Mathf.PI * 2f * i / NA;
                    float x = VC.x + Mathf.Cos(th) * r, z = VC.z + Mathf.Sin(th) * r;
                    float h = Altura(x, z);
                    v.Add(new Vector3(x, h, z)); uv.Add(new Vector2(x / 900f + 0.5f, z / 900f + 0.5f));
                }
            }
            for (int j = 0; j < NR; j++)
                for (int i = 0; i < NA; i++)
                {
                    int a = j * (NA + 1) + i, b = a + 1, c = a + NA + 1, d = c + 1;
                    tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
                }
            var m = NovaMalha(v, uv, tri, null);
            var nr = m.normals; int meio = (NR / 2) * (NA + 1);
            if (nr[meio].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); nr = m.normals; }
            // emenda: normais iguais nas duas pontas do anel
            for (int j = 0; j <= NR; j++) { int a = j * (NA + 1), b = a + NA; var n = (nr[a] + nr[b]).normalized; nr[a] = nr[b] = n; }
            m.normals = nr;
            var cor = new Color[v.Count];
            for (int j = 0; j <= NR; j++)
                for (int i = 0; i <= NA; i++)
                {
                    int k = j * (NA + 1) + i; var p = v[k];
                    float r = VRB * Mathf.Pow((float)j / NR, 1.12f), th = Mathf.PI * 2f * i / NA;
                    var c = CorTerreno(p.x, p.y, p.z, nr[k], 1e9f);
                    // perto do topo: mais rocha nas cristas, fuligem escura (cascalho) na cratera
                    float u = Mathf.Clamp01((r - VRC) / (VRB - VRC));
                    float cristas = Mathf.Sqrt(Mathf.Abs(Mathf.Sin(th * 21f + (Mathf.PerlinNoise(Mathf.Cos(th) * 2.3f + r * 0.0035f + 4f, Mathf.Sin(th) * 2.3f + 8f) - 0.5f) * 5f)));
                    float rr = Mathf.Max(c.r, Sm(0.8f, 0.97f, cristas) * (1f - u) * 0.9f);
                    if (r < VRC * 1.05f) rr = Mathf.Max(rr, 0.7f);
                    float gg = Mathf.Max(0f, 1f - rr - c.b);
                    cor[k] = new Color(rr, gg, Mathf.Max(0f, 1f - rr - gg), 0f);
                }
            m.colors = cor;
            return m;
        }

        // ================= RECURSOS DO JOGO =================
        const string DMESH = "assets/3dasset/desert/mesh/";
        static AssetBundle deserto;
        static bool desertoMeu;
        static Dictionary<string, Texture2D> texturas = new Dictionary<string, Texture2D>();
        static Material matRocha, matChao, matMagma, matCrosta, matMagmaRio, matCrostaRio, matQueda, matAditivo;
        static Mesh pedraBaixa; static Quaternion rotPedraBaixa;

        static AssetBundle Deserto()
        {
            if (deserto != null) return deserto;
            foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
                if (b.name == "environment4") { deserto = b; desertoMeu = false; return b; }
            string p = Path.Combine(Application.streamingAssetsPath, "Windows/environment4");
            float t0 = Time.realtimeSinceStartup;
            deserto = AssetBundle.LoadFromFile(p);
            desertoMeu = deserto != null;
            Log.LogInfo("Pacote do deserto carregado em " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s: " + (deserto != null));
            return deserto;
        }

        static void SoltarDeserto()
        {
            // libera o pacote mas mantém o que já foi usado; a pista do deserto original continua carregando
            if (deserto != null && desertoMeu) deserto.Unload(false);
            deserto = null; desertoMeu = false;
        }

        static T Carregar<T>(string caminho) where T : UnityEngine.Object
        {
            var b = Deserto();
            if (b == null) return null;
            var o = b.LoadAsset<T>(caminho);
            if (o == null) Log.LogWarning("Não achei " + caminho);
            return o;
        }

        static Texture2D Tex(string nome)
        {
            Texture2D t;
            if (texturas.TryGetValue(nome, out t) && t != null) return t;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(nome))
            {
                var buf = new byte[s.Length]; s.Read(buf, 0, buf.Length);
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                t.LoadImage(buf);
            }
            t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 8; t.filterMode = FilterMode.Trilinear;
            texturas[nome] = t;
            return t;
        }

        static Dictionary<string, Mesh> MalhasDe(string fbx, out Dictionary<string, Quaternion> rots)
        {
            var d = new Dictionary<string, Mesh>();
            rots = new Dictionary<string, Quaternion>();
            var go = Carregar<GameObject>(DMESH + fbx);
            if (go == null) return d;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !d.ContainsKey(mf.sharedMesh.name))
                {
                    d[mf.sharedMesh.name] = mf.sharedMesh;
                    rots[mf.sharedMesh.name] = mf.transform.localRotation;
                }
            return d;
        }

        static Shader AcharShader(string nome)
        {
            var s = Shader.Find(nome);
            if (s != null) return s;
            foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>()) if (sh.name == nome) return sh;
            return null;
        }

        static Material MatSprite(Texture t, Color c, int fila)
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = t; m.color = c; m.renderQueue = fila;
            return m;
        }

        static Material MatAditivo(Texture t, Color c)
        {
            var sh = AcharShader("Legacy Shaders/Particles/Additive") ?? AcharShader("Mobile/Particles/Additive");
            if (sh == null) return MatSprite(t, c, 3100);
            var m = new Material(sh);
            m.mainTexture = t;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
            m.renderQueue = 3100;
            return m;
        }

        static RolarTextura Rolar(GameObject raiz, Material m, Vector2 vel, float pulso)
        {
            var r = raiz.AddComponent<RolarTextura>(); r.m = m; r.vel = vel; r.pulso = pulso; return r;
        }

        // ================= MONTAGEM =================
        static void MontarVisualVulcao(Scene cena)
        {
            float t0 = Time.realtimeSinceStartup;
            // esconde o cenário antigo inteiro (inclusive o céu)
            foreach (var r in cena.GetRootGameObjects())
                if (!r.name.Contains("Lighting")) r.SetActive(false);
            var raiz = Raiz(cena, "GKMod_PistaNova");
            IndexarTracado();

            var matAsfaltoBase = AcharMaterial(cena, "Tarmac1");
            int lAsfalto = LayerMask.NameToLayer("ColMainTarmac");
            int lGrama = LayerMask.NameToLayer("ColGrass");
            int lParede = LayerMask.NameToLayer("ColWall");
            int lMorte = LayerMask.NameToLayer("ColKill");

            // ---- materiais ----
            var baseRocha = Carregar<Material>(DMESH + "materials/t_desert_red_rock1.mat");
            matRocha = baseRocha != null ? new Material(baseRocha) : MatCor(new Color(0.25f, 0.22f, 0.22f), null);
            matRocha.SetTexture("_MainTex", Tex("v2_rocha_facetas.png"));
            matRocha.SetColor("_FresnelTeint", new Color(1f, 0.40f, 0.12f, 1f));
            matRocha.SetFloat("_IsFresnel", 1f); matRocha.EnableKeyword("_ISFRESNEL_ON");
            matRocha.SetFloat("_FresnelIntensity", 1.3f);
            matRocha.SetColor("_Color", new Color(0.58f, 0.52f, 0.53f, 1f));
            var baseChao = Carregar<Material>(DMESH + "materials/m_desert_e4c1_01.mat");
            matChao = baseChao != null ? new Material(baseChao) : matRocha;
            if (baseChao != null)
            {
                matChao.SetTexture("_T_Diffuse_VR", Tex("v2_rocha_encosta.png"));
                matChao.SetTexture("_T_DiffuseVG", Tex("v2_cinza_areia.png"));
                matChao.SetTexture("_T_DiffuseVB", Tex("v2_escoria.png"));
                matChao.SetFloat("_IsTriplanarVR", 1f); matChao.EnableKeyword("_ISTRIPLANARVR_ON");
                Log.LogInfo("material chão: shader=" + matChao.shader.name + " keywords=" + string.Join(",", matChao.shaderKeywords) +
                    " tilling VR=" + matChao.GetFloat("_TillingVR") + " VG=" + matChao.GetFloat("_TillingVG") + " VN=" + matChao.GetFloat("_TillingVN"));
            }
            matMagma = MatSprite(Tex("v2_lava_magma.png"), new Color(1.45f, 1.15f, 1.0f, 1f), 2990);
            matCrosta = MatSprite(Tex("v2_lava_crosta.png"), new Color(1f, 1f, 1f, 1f), 2991);
            matMagmaRio = MatSprite(Tex("v2_lava_magma.png"), new Color(1.5f, 1.2f, 1.0f, 1f), 2992);
            matCrostaRio = MatSprite(Tex("v2_lava_crosta.png"), new Color(1f, 1f, 1f, 1f), 2993);
            matQueda = MatSprite(Tex("v2_lava_magma.png"), new Color(1.7f, 1.3f, 1.05f, 1f), 2994);
            matAditivo = MatAditivo(Tex("v2_fumaca.png"), new Color(1f, 0.45f, 0.12f, 0.5f));
            Rolar(raiz, matMagma, new Vector2(0.004f, 0.010f), 0.07f);
            Rolar(raiz, matCrosta, new Vector2(0.0015f, 0.004f), 0f);
            Rolar(raiz, matMagmaRio, new Vector2(0f, -0.22f), 0.06f);
            Rolar(raiz, matCrostaRio, new Vector2(0f, -0.11f), 0f);
            Rolar(raiz, matQueda, new Vector2(0f, -0.9f), 0.08f).freq = 3f;

            // ---- pista ----
            // piso próprio (nada do asfalto do jogo): estrada de cinza compactada na volta toda,
            // calçamento de basalto com lava nas frestas na largada e na passarela do lago, meio-fio só nas curvas
            var baseEstrada = Carregar<Material>(DMESH + "materials/m_e4c1_asphalt1.mat");
            var basePav = Carregar<Material>(DMESH + "materials/m_desert_pavement_road1.mat");
            var matEstrada = MatPiso(baseEstrada, "v3_estrada", 0.006f, new Color(1f, 1f, 1f, 0f));
            var matBasalto = MatPiso(basePav ?? baseEstrada, "v3_basalto", 0.007f, new Color(1f, 1f, 1f, 0f));
            var matMeioFio = MatPiso(basePav ?? baseEstrada, "v3_meiofio", 0.009f, new Color(1f, 1f, 1f, 0f));
            float sA = S_SALTO + VAO, sB = S_SALTO + Total;   // pista vai do pouso até a beira da rampa
            float L2 = LARG / 2f;
            // bordas com alfa 0 → o shader corta com ruído e a estrada "se desfaz" no cascalho do acostamento
            Malha(raiz, "Estrada", FaixaCols(new[] { -L2, -L2 + 1.3f, L2 - 1.3f, L2 }, new[] { 0f, 1f, 1f, 0f }, 0f, L2, sA, sB, 0f, 0f), matEstrada, lAsfalto);
            Malha(raiz, "AcostamentoE", Faixa(-L2 - MARGEM, -L2 + 1.5f, -0.02f, 0, sA, sB, true), matChao, lGrama);
            Malha(raiz, "AcostamentoD", Faixa(L2 - 1.5f, L2 + MARGEM, -0.02f, 0, sA, sB, true), matChao, lGrama);
            var bordas = new[] { -L2 - 0.6f, -L2 + 0.4f, L2 - 0.4f, L2 + 0.6f };
            var bAlfa = new[] { 0f, 1f, 1f, 0f };
            // praça da largada (passa da volta) e passarela do lago (os dois lados do vão)
            Desenho(raiz, "CalcadaLargada", FaixaCols(bordas, bAlfa, 0.025f, L2, 1288f, Total + S_LARGADA + 45f, 9f, 9f), matBasalto);
            Desenho(raiz, "CalcadaRampa", FaixaCols(bordas, bAlfa, 0.025f, L2, 978f, S_SALTO, 9f, 0f), matBasalto);
            Desenho(raiz, "CalcadaPouso", FaixaCols(bordas, bAlfa, 0.025f, L2, S_SALTO + VAO, 1240f, 0f, 9f), matBasalto);
            int nMeio = 0;
            foreach (var tr in TrechosDeCurva(sA, sB))
            {
                Malha(raiz, "MeioFioE", MeioFio(-L2 + 0.25f, -L2 - 1.25f, tr.x, tr.y), matMeioFio, lAsfalto);
                Malha(raiz, "MeioFioD", MeioFio(L2 - 0.25f, L2 + 1.25f, tr.x, tr.y), matMeioFio, lAsfalto);
                nMeio++;
            }
            Log.LogInfo("meio-fio em " + nMeio + " curvas; material estrada: " + (baseEstrada != null ? baseEstrada.shader.name : "nulo") +
                " / calçada: " + (basePav != null ? basePav.shader.name : "nulo"));
            // muros invisíveis (colisão); mais altos no salto para ninguém escapar pelo lado
            float muro = LARG / 2f + MARGEM + 0.6f;
            foreach (float lat in new[] { muro, -muro })
            {
                var w = new GameObject("Muro"); w.transform.SetParent(raiz.transform, false); w.layer = lParede;
                w.AddComponent<MeshCollider>().sharedMesh = MuroColisao(lat, 1.2f);
            }
            // faces de pedra do vão (borda da rampa e do pouso) e tábua de largada da rampa
            FacesDoVao(raiz);
            CriarLargada(raiz, lAsfalto);
            // faixa quadriculada colada no calçamento (a caixa reta do oval atravessava as pedras)
            foreach (var t in raiz.GetComponentsInChildren<Transform>())
                if (t.name == "FaixaLargada")
                {
                    t.position = Vector3.zero; t.rotation = Quaternion.identity; t.localScale = Vector3.one;
                    t.GetComponent<MeshFilter>().sharedMesh = FaixaXadrez(S_LARGADA - 1.2f, S_LARGADA + 1.2f, 0.06f);
                    t.GetComponent<MeshRenderer>().sharedMaterial.color = new Color(0.72f, 0.70f, 0.70f);
                }

            // ---- terreno ----
            float t1 = Time.realtimeSinceStartup;
            Desenho(raiz, "Terreno", MalhaTerreno(TX0, TZ0, TX1, TZ1, 4f, false), matChao);
            Desenho(raiz, "TerrenoLonge", MalhaTerreno(-2600f, -2400f, 2600f, 2800f, 40f, true), matChao);
            Desenho(raiz, "Vulcao", MalhaCone(), matChao);
            Log.LogInfo("terreno em " + (Time.realtimeSinceStartup - t1).ToString("0.00") + " s");

            // ---- lava ----
            Lava(raiz, "LagoSul", -290f, -330f, 290f, -20f, LAVA_Y);
            Lava(raiz, "LagoOeste", -520f, -200f, -170f, 300f, LAVA_Y);
            Lava(raiz, "PocaRio", 0f, -140f, 50f, -70f, LAVA_Y);
            // morte na lava: caixa logo abaixo da superfície
            foreach (var cx in new[] { new Vector4(0f, -175f, 580f, 310f), new Vector4(-345f, 50f, 350f, 500f) })
            {
                var k = new GameObject("LavaMata"); k.transform.SetParent(raiz.transform, false); k.layer = lMorte;
                var bc = k.AddComponent<BoxCollider>();
                k.transform.position = new Vector3(cx.x, LAVA_Y - 2.3f, cx.y);
                bc.size = new Vector3(cx.z, 4f, cx.w);
            }
            RioDoPlanalto(raiz);
            FluxosDoVulcao(raiz);

            // ---- rochas e enfeites ----
            Enfeitar(raiz);

            // ---- céu, fumaça, brasas ----
            Ceu(raiz);
            Fumaca(raiz);
            Brasas(raiz, new Vector3(0, LAVA_Y + 0.5f, -168f), new Vector3(380f, 160f, 1f), 70f);
            Brasas(raiz, new Vector3(S_SALTO_POS().x, LAVA_Y + 0.5f, S_SALTO_POS().z), new Vector3(30f, 30f, 1f), 30f);
            // tudo o que eu criei ignora as sondas de luz assadas da cena original (céu azul) e usa a luz ambiente do vulcão
            foreach (var rd in raiz.GetComponentsInChildren<Renderer>(true))
            {
                rd.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                rd.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            SoltarDeserto();
            Log.LogInfo("Pista do vulcão montada em " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s.");
        }

        static Vector3 S_SALTO_POS() { return Ponto(S_SALTO + VAO * 0.5f); }

        // faixa entre dois deslocamentos laterais, de sIni a sFim (passa da volta); uvLarg 0 = UV do mundo
        static Mesh Faixa(float esq, float dir, float y, float uvLarg, float sIni, float sFim, bool comCor)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            int n = Mathf.Max(2, Mathf.CeilToInt((sFim - sIni) / PASSO));
            for (int i = 0; i <= n; i++)
            {
                float s = sIni + (sFim - sIni) * i / n;
                Vector3 c = Ponto(s), r = Direita(s);
                Vector3 a = c + r * esq + Vector3.up * y, b = c + r * dir + Vector3.up * y;
                v.Add(a); v.Add(b);
                if (uvLarg > 0) { uv.Add(new Vector2(0, s / uvLarg)); uv.Add(new Vector2((dir - esq) / uvLarg, s / uvLarg)); }
                else { uv.Add(new Vector2(a.x / 900f + 0.5f, a.z / 900f + 0.5f)); uv.Add(new Vector2(b.x / 900f + 0.5f, b.z / 900f + 0.5f)); }
                if (comCor)
                {
                    float g = Ruido(a.x, a.z, 0.05f);
                    cor.Add(new Color(0f, 1f - g * 0.6f, g * 0.6f, 0f)); cor.Add(new Color(0f, 1f - g * 0.6f, g * 0.6f, 0f));
                    // acostamento: cascalho com manchas de cinza (os mesmos canais do terreno)
                    cor[cor.Count - 1] = new Color(0f, 0.25f, 0.75f, 0f); cor[cor.Count - 2] = new Color(0f, 0.25f, 0.75f, 0f);
                }
                else { cor.Add(Color.white); cor.Add(Color.white); }
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tri.Add(a); tri.Add(a + 2); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3);
            }
            var m = NovaMalha(v, uv, tri, cor);
            var nr = m.normals; if (nr.Length > 0 && nr[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            return m;
        }

        static Mesh Zebra(float esq, float dir, float sIni, float sFim)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            for (float s = sIni; s < sFim - 0.01f; s += PASSO)
            {
                float s2 = Mathf.Min(s + PASSO, sFim);
                Vector3 a = Ponto(s), b = Ponto(s2), ra = Direita(s), rb = Direita(s2);
                Color c = ((int)(Mod(s, Total) / 3f)) % 2 == 0 ? new Color(0.85f, 0.1f, 0.1f) : Color.white;
                int k = v.Count;
                v.Add(a + ra * esq + Vector3.up * 0.02f); v.Add(a + ra * dir + Vector3.up * 0.02f);
                v.Add(b + rb * esq + Vector3.up * 0.02f); v.Add(b + rb * dir + Vector3.up * 0.02f);
                for (int q = 0; q < 4; q++) { cor.Add(c); uv.Add(Vector2.zero); }
                tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 3);
            }
            var m = NovaMalha(v, uv, tri, cor);
            var nr = m.normals; if (nr.Length > 0 && nr[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            return m;
        }

        static Mesh FaixaXadrez(float s0, float s1, float y)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            int nl = 12, ns = 3;
            for (int i = 0; i <= ns; i++)
            {
                float s = s0 + (s1 - s0) * i / ns;
                Vector3 c = Ponto(s), r = Direita(s);
                for (int j = 0; j <= nl; j++)
                {
                    float lat = -LARG / 2f + LARG * j / nl;
                    v.Add(c + r * lat + Vector3.up * y);
                    uv.Add(new Vector2((float)j / nl, (float)i / ns));
                }
            }
            for (int i = 0; i < ns; i++)
                for (int j = 0; j < nl; j++)
                {
                    int a = i * (nl + 1) + j, b = a + nl + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1); tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = NovaMalha(v, uv, tri, null);
            var nr = m.normals; if (nr.Length > 0 && nr[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            return m;
        }

        // reflexo do ambiente: céu vermelho-escuro do vulcão (o da cena original é azul e tingia pedras e piso)
        static Cubemap reflexo;
        static Cubemap Reflexo()
        {
            if (reflexo != null) return reflexo;
            int n = 32;
            reflexo = new Cubemap(n, TextureFormat.RGBA32, true);
            Color ceu = new Color(0.30f, 0.12f, 0.10f), horiz = new Color(0.42f, 0.17f, 0.11f), chao = new Color(0.10f, 0.06f, 0.05f);
            foreach (CubemapFace f in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float uc = 2f * (i + 0.5f) / n - 1f, vc = 2f * (j + 0.5f) / n - 1f;
                        float y = f == CubemapFace.PositiveY ? 1f / Mathf.Sqrt(1 + uc * uc + vc * vc) :
                                  f == CubemapFace.NegativeY ? -1f / Mathf.Sqrt(1 + uc * uc + vc * vc) :
                                  -vc / Mathf.Sqrt(1 + uc * uc + vc * vc);
                        Color c = y >= 0 ? Color.Lerp(horiz, ceu, Mathf.Pow(y, 0.6f)) : Color.Lerp(horiz, chao, Mathf.Pow(-y, 0.4f));
                        reflexo.SetPixel(f, i, j, c);
                    }
            reflexo.Apply(true);
            return reflexo;
        }

        // textura de dados (normal, MGAE, altura): linear, comprimida
        static Texture2D TexLin(string nome)
        {
            Texture2D t;
            if (texturas.TryGetValue(nome, out t) && t != null) return t;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(nome))
            {
                var buf = new byte[s.Length]; s.Read(buf, 0, buf.Length);
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
                t.LoadImage(buf);
            }
            t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 8; t.filterMode = FilterMode.Trilinear;
            t.Compress(true); t.Apply(true, true);
            texturas[nome] = t;
            return t;
        }

        // material de piso a partir do shader de estrada do jogo (GKHD/SH_ParallaxAlphaCut), com texturas próprias
        static Material MatPiso(Material b, string pref, float parallax, Color tinta)
        {
            if (b == null) return MatCor(new Color(0.22f, 0.2f, 0.2f), Tex(pref + "_cor.png"));
            var m = new Material(b);
            var cor = Tex(pref + "_cor.png");
            m.SetTexture("_Diffuse", cor);
            m.SetTexture("_Normal", TexLin(pref + "_nrm.png"));
            m.SetTexture("_MGAE", TexLin(pref + "_mgae.png"));
            m.SetTexture("_Height", TexLin(pref + "_alt.png"));
            m.SetTexture("_T_AlphaNoise", TexLin("v3_ruido.png"));
            foreach (var p in new[] { "_Diffuse", "_Normal", "_MGAE", "_Height" }) { m.SetTextureScale(p, Vector2.one); m.SetTextureOffset(p, Vector2.zero); }
            m.SetFloat("_Tiling", 1f);
            m.SetFloat("_ParallaxScale", parallax);
            m.SetFloat("_Parallax_RefPlane_Value", 0.5f);
            m.SetColor("_Color0", tinta);   // _Color0 = tinta do difuso nesse shader
            m.SetColor("_Color", Color.white);
            return m;
        }

        // faixa com várias colunas laterais; alfa do vértice = alfa da coluna × rampa nas pontas (fIni/fFim metros)
        static Mesh FaixaCols(float[] lats, float[] alfas, float y, float uvLarg, float sIni, float sFim, float fIni, float fFim)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            int n = Mathf.Max(2, Mathf.CeilToInt((sFim - sIni) / PASSO)), k = lats.Length;
            for (int i = 0; i <= n; i++)
            {
                float s = sIni + (sFim - sIni) * i / n;
                Vector3 c = Ponto(s), r = Direita(s);
                float aS = 1f;
                if (fIni > 0) aS = Mathf.Min(aS, (s - sIni) / fIni);
                if (fFim > 0) aS = Mathf.Min(aS, (sFim - s) / fFim);
                for (int j = 0; j < k; j++)
                {
                    v.Add(c + r * lats[j] + Vector3.up * y);
                    uv.Add(new Vector2((lats[j] + LARG / 2f) / uvLarg, s / uvLarg));
                    cor.Add(new Color(1f, 1f, 1f, Mathf.Clamp01(alfas[j] * aS)));
                }
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < k - 1; j++)
                {
                    int a = i * k + j, b = a + k;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1);
                    tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = NovaMalha(v, uv, tri, cor);
            var nr = m.normals; if (nr.Length > 0 && nr[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateTangents(); }
            return m;
        }

        // trechos de curva fechada (raio < 85 m), alargados 10 m para cada lado; fora da zona do salto
        static List<Vector2> TrechosDeCurva(float sIni, float sFim)
        {
            var res = new List<Vector2>();
            int n = Mathf.CeilToInt((sFim - sIni) / PASSO);
            var marca = new bool[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = sIni + i * PASSO;
                float graus = Vector3.Angle(Direcao(s - 6f), Direcao(s + 6f));
                if (graus / 12f > 57.3f / 85f)
                    for (int d = -5; d <= 5; d++) { int q = i + d; if (q >= 0 && q <= n) marca[q] = true; }
            }
            for (int i = 0; i <= n; i++)
            {
                float s = sIni + i * PASSO;
                if (NaZonaDoSalto(Mod(s, Total)) || Mod(s, Total) > 975f && Mod(s, Total) < 1245f) marca[i] = false;
            }
            int ini = -1;
            for (int i = 0; i <= n + 1; i++)
            {
                bool m = i <= n && marca[i];
                if (m && ini < 0) ini = i;
                if (!m && ini >= 0)
                {
                    if (i - ini >= 5) res.Add(new Vector2(sIni + ini * PASSO, sIni + (i - 1) * PASSO));
                    ini = -1;
                }
            }
            return res;
        }

        // meio-fio: u=0 no lado da pista (sem chanfro), u=1 no lado de fora; v = 3 m por par de blocos
        static Mesh MeioFio(float dentro, float fora, float sIni, float sFim)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            int n = Mathf.Max(2, Mathf.CeilToInt((sFim - sIni) / 1f));
            for (int i = 0; i <= n; i++)
            {
                float s = sIni + (sFim - sIni) * i / n;
                Vector3 c = Ponto(s), r = Direita(s);
                v.Add(c + r * dentro + Vector3.up * 0.03f); v.Add(c + r * fora + Vector3.up * 0.06f);
                float vv = (s - sIni) / 3f;
                uv.Add(new Vector2(0f, vv)); uv.Add(new Vector2(1f, vv));
                float a = Mathf.Clamp01(Mathf.Min(s - sIni, sFim - s) / 1.5f);
                cor.Add(new Color(1, 1, 1, a)); cor.Add(new Color(1, 1, 1, a));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tri.Add(a); tri.Add(a + 2); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3);
            }
            var m = NovaMalha(v, uv, tri, cor);
            var nr = m.normals; if (nr.Length > 0 && nr[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateTangents(); }
            return m;
        }

        // muro de colisão contínuo (dois lados, os dois sentidos); 6 m de altura no salto
        static Mesh MuroColisao(float lateral, float esp)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float[] offs = { lateral - esp / 2f, lateral + esp / 2f };
            int n = P.Count;
            for (int i = 0; i <= n; i++)
            {
                float s = (i % n) * PASSO;
                Vector3 c = P[i % n], r = Direita(s);
                float alt = NaZonaDoSalto(s) ? 6.5f : 1.8f;
                v.Add(c + r * offs[0] + Vector3.down * 0.5f); v.Add(c + r * offs[0] + Vector3.up * alt);
                v.Add(c + r * offs[1] + Vector3.up * alt); v.Add(c + r * offs[1] + Vector3.down * 0.5f);
                for (int q = 0; q < 4; q++) uv.Add(Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 4, b = a + 4;
                int[,] faces = { { 0, 1 }, { 1, 2 }, { 2, 3 } };
                for (int f = 0; f < 3; f++)
                {
                    int p0 = a + faces[f, 0], p1 = a + faces[f, 1], q0 = b + faces[f, 0], q1 = b + faces[f, 1];
                    tri.Add(p0); tri.Add(q0); tri.Add(p1); tri.Add(p1); tri.Add(q0); tri.Add(q1);
                    tri.Add(p0); tri.Add(p1); tri.Add(q0); tri.Add(p1); tri.Add(q1); tri.Add(q0);
                }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        // paredões de pedra na beira da rampa e do pouso + listras de aviso
        static void FacesDoVao(GameObject raiz)
        {
            float meia = LARG / 2f + MARGEM;
            foreach (float s in new[] { S_SALTO, S_SALTO + VAO })
            {
                Vector3 c = Ponto(s), r = Direita(s), f = Direcao(s);
                float topo = c.y, base_ = LAVA_Y - 1.5f;
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                int n = 12;
                for (int i = 0; i <= n; i++)
                {
                    float lat = -meia + 2f * meia * i / n;
                    float irreg = (Ruido(lat * 3f, s, 0.3f) - 0.5f) * 1.2f;
                    Vector3 p = c + r * lat + f * irreg;
                    v.Add(new Vector3(p.x, topo - 0.05f, p.z)); v.Add(new Vector3(p.x, base_, p.z) + f * (s == S_SALTO ? -1.5f : 1.5f));
                    uv.Add(new Vector2(lat / 8f, topo / 8f)); uv.Add(new Vector2(lat / 8f, base_ / 8f));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = i * 2;
                    tri.Add(a); tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 2); tri.Add(a + 1); tri.Add(a + 3);
                    tri.Add(a); tri.Add(a + 2); tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3); tri.Add(a + 1);
                }
                Desenho(raiz, "FaceVao", NovaMalha(v, uv, tri, null), matRocha);
            }
            // listras amarelas/pretas na beira da rampa
            var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Repeat;
            for (int i = 0; i < 8; i++) tex.SetPixel(i, 0, i % 2 == 0 ? new Color(1f, 0.78f, 0.05f) : new Color(0.08f, 0.07f, 0.07f));
            tex.Apply();
            var mat = MatCor(Color.white, tex);
            var cs = Ponto(S_SALTO - 0.8f); var dir = Direcao(S_SALTO); var rr = Direita(S_SALTO);
            var lista = Caixa(raiz, "AvisoRampa", cs + Vector3.up * 0.04f, new Vector3(LARG, 0.06f, 1.6f), Quaternion.LookRotation(dir, Vector3.up), mat);
            lista.GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale = new Vector2(LARG / 2.5f, 1f);
        }

        // lava em camadas: magma (brilha, flui) + crosta (placas escuras com fendas)
        static void Lava(GameObject raiz, string nome, float x0, float z0, float x1, float z1, float y)
        {
            Desenho(raiz, nome + "_Magma", Retangulo(x0, z0, x1, z1, y, 1f / 38f), matMagma);
            Desenho(raiz, nome + "_Crosta", Retangulo(x0, z0, x1, z1, y + 0.06f, 1f / 48f), matCrosta);
        }

        static Mesh Retangulo(float x0, float z0, float x1, float z1, float y, float esc)
        {
            var v = new List<Vector3> { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1) };
            var uv = new List<Vector2>(); foreach (var p in v) uv.Add(new Vector2(p.x * esc, p.z * esc));
            var tri = new List<int> { 0, 2, 1, 0, 3, 2 };
            return NovaMalha(v, uv, tri, null);
        }

        // fita que acompanha o terreno ao longo de um caminho (x,z); u da textura = ao longo do caminho
        static Mesh Fita(List<Vector3> centro, List<float> larg, float sobe, float uvEsc, List<float> alfa)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            float[] cols = { -0.5f, -0.32f, 0.32f, 0.5f };
            float acc = 0;
            for (int i = 0; i < centro.Count; i++)
            {
                Vector3 d = (i + 1 < centro.Count ? centro[i + 1] : centro[i]) - (i > 0 ? centro[i - 1] : centro[i]); d.y = 0; d.Normalize();
                Vector3 r = new Vector3(d.z, 0, -d.x);
                if (i > 0) acc += D2(centro[i], centro[i - 1]);
                for (int q = 0; q < 4; q++)
                {
                    float off = cols[q] * larg[i];
                    Vector3 p = centro[i] + r * off;
                    float hy = Mathf.Max(centro[i].y, Altura(p.x, p.z) + sobe * 0.6f);
                    p.y = hy;
                    v.Add(p);
                    uv.Add(new Vector2(0.5f + off / 16f, -acc * uvEsc));
                    bool borda = q == 0 || q == 3;
                    cor.Add(new Color(1f, 1f, 1f, (borda ? 0f : 1f) * alfa[i]));
                }
            }
            for (int i = 0; i + 1 < centro.Count; i++)
                for (int q = 0; q < 3; q++)
                {
                    int a = i * 4 + q, b = a + 1, c = a + 4, d = c + 1;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
                }
            var m = NovaMalha(v, uv, tri, cor);
            var nr = m.normals; if (nr.Length > 6 && nr[5].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            return m;
        }

        static void RioDoPlanalto(GameObject raiz)
        {
            // bacia de lava em cima do planalto
            float topo = Altura(RIO[0].x, RIO[0].z);
            var bacia = new Vector3(RIO[0].x, topo + 1.6f, RIO[0].z);
            Desenho(raiz, "BaciaMagma", Disco(bacia, 13f, 1f / 30f), matMagma);
            Desenho(raiz, "BaciaCrosta", Disco(bacia + Vector3.up * 0.05f, 12f, 1f / 45f), matCrosta);
            // rio: amostra o caminho a cada 1,5 m
            var c = new List<Vector3>(); var l = new List<float>(); var a = new List<float>();
            float total = 0; for (int i = 0; i + 1 < RIO.Length; i++) total += D2(RIO[i], RIO[i + 1]);
            for (float u = 4f; u <= total; u += 1.5f)
            {
                Vector3 p = PontoCaminho(RIO, u);
                float h = Altura(p.x, p.z) + 0.35f;
                c.Add(new Vector3(p.x, h, p.z));
                l.Add(9f + 3f * Mathf.Sin(u * 0.07f));
                a.Add(Mathf.Clamp01(u / 6f));
            }
            Desenho(raiz, "RioMagma", Fita(c, l, 0.35f, 1f / 24f, a), matMagmaRio);
            var l2 = new List<float>(); foreach (var x in l) l2.Add(x * 0.85f);
            var c2 = new List<Vector3>(); foreach (var p in c) c2.Add(p + Vector3.up * 0.06f);
            Desenho(raiz, "RioCrosta", Fita(c2, l2, 0.4f, 1f / 36f, a), matCrostaRio);
            // cachoeira: trecho íngreme ganha um véu de magma rápido por cima e brilho
            var cq = new List<Vector3>(); var lq = new List<float>(); var aq = new List<float>();
            for (int i = 0; i < c.Count; i++)
            {
                float dy = i > 0 ? c[i - 1].y - c[i].y : 0f;
                if (c[i].z > -16f && c[i].z < 26f) { cq.Add(c[i] + Vector3.up * 0.15f); lq.Add(l[i] * 0.9f); aq.Add(Mathf.Clamp01(dy * 1.2f + 0.2f)); }
            }
            if (cq.Count > 2) Desenho(raiz, "Cachoeira", Fita(cq, lq, 0.5f, 1f / 14f, aq), matQueda);
            var pe = PontoCaminho(RIO, 66f); pe.y = Altura(pe.x, pe.z) + 0.5f;
            var fx = new GameObject("VaporCachoeira"); fx.transform.SetParent(raiz.transform, false); fx.transform.position = pe;
            var col = fx.AddComponent<ColunaFumaca>(); col.mat = MatSprite(Tex("v2_fumaca.png"), Color.white, 3050); col.n = 8; col.vida = 7f; col.subida = 3.5f; col.raio = 5f;
            col.tamIni = 3f; col.tamFim = 13f; col.alfa = 0.3f; col.corBaixo = new Color(1f, 0.55f, 0.35f); col.corAlto = new Color(0.45f, 0.38f, 0.38f); col.vento = new Vector3(1f, 0, 0.3f);
        }

        static Vector3 PontoCaminho(Vector3[] cam, float u)
        {
            float acc = 0;
            for (int i = 0; i + 1 < cam.Length; i++)
            {
                float d = D2(cam[i], cam[i + 1]);
                if (acc + d >= u) return Vector3.Lerp(cam[i], cam[i + 1], (u - acc) / d);
                acc += d;
            }
            return cam[cam.Length - 1];
        }

        static void FluxosDoVulcao(GameObject raiz)
        {
            // lava no fundo da cratera
            float fundo = Altura(VC.x, VC.z);
            var cc = new Vector3(VC.x, fundo + 9f, VC.z);
            Desenho(raiz, "CrateraMagma", Disco(cc, VRC * 0.75f, 1f / 30f), matMagma);
            Desenho(raiz, "CrateraCrosta", Disco(cc + Vector3.up * 0.1f, VRC * 0.7f, 1f / 45f), matCrosta);
            for (int k = 0; k < FLUXOS.Length; k++)
            {
                var c = new List<Vector3>(); var l = new List<float>(); var a = new List<float>();
                float fim = FLUXO_FIM[k];
                for (float r = VRC * 0.92f; r <= fim; r += 3f)
                {
                    float th = FluxoTheta(k, r);
                    float x = VC.x + Mathf.Cos(th) * r, z = VC.z + Mathf.Sin(th) * r;
                    c.Add(new Vector3(x, Altura(x, z) + 0.6f, z));
                    l.Add(FluxoLarg(k, r));
                    a.Add(Mathf.Clamp01((fim - r) / 20f) * Mathf.Clamp01((r - VRC * 0.92f) / 8f + 0.3f));
                }
                // de cima para baixo (a textura escorre morro abaixo)
                Desenho(raiz, "Fluxo" + k + "_Magma", Fita(c, l, 0.6f, 1f / 30f, a), matMagmaRio);
                var l2 = new List<float>(); foreach (var x in l) l2.Add(x * 0.8f);
                var c2 = new List<Vector3>(); foreach (var p in c) c2.Add(p + Vector3.up * 0.08f);
                Desenho(raiz, "Fluxo" + k + "_Crosta", Fita(c2, l2, 0.7f, 1f / 40f, a), matCrostaRio);
                // poça no pé
                float thf = FluxoTheta(k, fim);
                var pf = new Vector3(VC.x + Mathf.Cos(thf) * (fim + 6f), 0, VC.z + Mathf.Sin(thf) * (fim + 6f));
                pf.y = Altura(pf.x, pf.z) + 0.9f;
                Desenho(raiz, "PocaVulcao" + k, Disco(pf, 16f + 4f * (k % 2), 1f / 30f), matMagma);
            }
        }

        static Mesh Disco(Vector3 c, float raio, float esc)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var cor = new List<Color>();
            int n = 48;
            v.Add(c); uv.Add(new Vector2(c.x * esc, c.z * esc)); cor.Add(Color.white);
            for (int ring = 1; ring <= 2; ring++)
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI * 2f * i / n;
                    float rr = raio * (ring == 1 ? 0.8f : 1.05f) * (1f + 0.12f * Mathf.Sin(a * 5f + 1f) + 0.06f * Mathf.Sin(a * 11f));
                    var p = c + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                    v.Add(p); uv.Add(new Vector2(p.x * esc, p.z * esc)); cor.Add(new Color(1, 1, 1, ring == 1 ? 1f : 0f));
                }
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + i + 1, a2 = a + n + 1, b2 = b + n + 1;
                tri.Add(0); tri.Add(b); tri.Add(a);
                tri.Add(a); tri.Add(b); tri.Add(a2); tri.Add(b); tri.Add(b2); tri.Add(a2);
            }
            var m = NovaMalha(v, uv, tri, cor);
            var nr = m.normals; if (nr[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            return m;
        }

        static GameObject Pedra(GameObject raiz, Mesh m, Quaternion rotBase, Vector3 pos, float yaw, float esc, float afundar)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(raiz.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = matRocha;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * rotBase;
            go.transform.localScale = Vector3.one * esc;
            go.transform.position = pos;
            AssentarNoChao(go, pos);
            go.transform.position += Vector3.up * (pos.y - afundar);
            return go;
        }

        static GameObject Desenho(GameObject raiz, string nome, Mesh m, Material mat)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(raiz.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Vector3 NoChao(float x, float z) { return new Vector3(x, Altura(x, z), z); }

        static void Enfeitar(GameObject raiz)
        {
            Dictionary<string, Quaternion> rGrandes, rPedras, rArcos;
            var grandes = MalhasDe("e4c2world.fbx", out rGrandes);
            var pedras = MalhasDe("e4c1props.fbx", out rPedras);
            var arcos = MalhasDe("e4c1_rocks.fbx", out rArcos);
            var nomesGrandes = new List<string>(); foreach (var n in grandes.Keys) if (n.StartsWith("E4C2_rock")) nomesGrandes.Add(n);
            var nomesPedras = new List<string>(); foreach (var n in pedras.Keys) if (n.StartsWith("Desert_Stones")) nomesPedras.Add(n);
            if (grandes.ContainsKey("E4C2_rock010")) { pedraBaixa = grandes["E4C2_rock010"]; rotPedraBaixa = rGrandes["E4C2_rock010"]; }
            var rnd = new System.Random(11);
            Func<double> R = rnd.NextDouble;
            var rochas = new GameObject("Rochas"); rochas.transform.SetParent(raiz.transform, false);
            float muro = LARG / 2f + MARGEM + 0.6f;

            // pedras baixas na beira (onde a vista é aberta: largada, passarela, encosta)
            if (pedraBaixa != null)
                for (float s = 0; s < Total; s += 2.6f)
                {
                    if (NaZonaDoSalto(s) && s > S_SALTO - 2f) continue;
                    bool aberto = s < 190f || s > 1300f || (s > 990f && s < 1215f) || (s > 330f && s < 560f);
                    foreach (int lado in new[] { -1, 1 })
                    {
                        if (!aberto && R() < 0.75) continue;
                        float ss = s + (float)R() * 1.0f;
                        var p = Ponto(ss) + Direita(ss) * lado * (muro + 0.9f + (float)R() * 0.8f);
                        p = NoChao(p.x, p.z);
                        Pedra(rochas, pedraBaixa, rotPedraBaixa, p, (float)R() * 360f, 0.026f + (float)R() * 0.02f, 0.35f);
                    }
                }
            if (nomesGrandes.Count > 0)
            {
                // formações grandes no alto do cânion e ao longe, fora da pista
                for (int i = 0; i < 160; i++)
                {
                    float x = (float)(R() * 860 - 430), z = (float)(R() * 860 - 390);
                    float d, s, y;
                    if (PertoPista(x, z, 45f, out d, out s, out y)) continue;
                    var p = NoChao(x, z);
                    if (Mathf.Abs(Altura(x + 6f, z) - Altura(x - 6f, z)) + Mathf.Abs(Altura(x, z + 6f) - Altura(x, z - 6f)) > 9f) continue;
                    if (p.y < LAVA_Y + 1f && R() < 0.7) continue;               // poucas no meio da lava (ilhas)
                    if (D2(p, MESA) < 70f) continue;
                    if (D2(p, VC) < VRB * 0.95f) continue;
                    var nome = nomesGrandes[rnd.Next(nomesGrandes.Count)];
                    float esc = p.y < LAVA_Y + 1f ? 0.15f + (float)R() * 0.12f : 0.25f + (float)R() * 0.45f;
                    Pedra(rochas, grandes[nome], rGrandes[nome], p, (float)R() * 360f, esc, 2f);
                }
                // coroa de rochas na borda da cratera (menos na brecha)
                for (int i = 0; i < 26; i++)
                {
                    float th = Mathf.PI * 2f * i / 26f + (float)(R() - 0.5) * 0.15f;
                    if (Mathf.Abs(AngDif(th, TH_BRECHA)) < 0.3f) continue;
                    float r = VRC + 4f + (float)R() * 10f;
                    var p = NoChao(VC.x + Mathf.Cos(th) * r, VC.z + Mathf.Sin(th) * r);
                    var nome = nomesGrandes[rnd.Next(nomesGrandes.Count)];
                    Pedra(rochas, grandes[nome], rGrandes[nome], p, (float)R() * 360f, 0.5f + (float)R() * 0.5f, 6f);
                }
                // rochas nas encostas do vulcão
                for (int i = 0; i < 70; i++)
                {
                    float th = (float)R() * Mathf.PI * 2f, r = VRC + 30f + (float)R() * (VRB * 0.85f - VRC);
                    var p = NoChao(VC.x + Mathf.Cos(th) * r, VC.z + Mathf.Sin(th) * r);
                    var nome = nomesGrandes[rnd.Next(nomesGrandes.Count)];
                    Pedra(rochas, grandes[nome], rGrandes[nome], p, (float)R() * 360f, 0.2f + (float)R() * 0.35f, 4f);
                }
                // borda do planalto (dá silhueta de rocha)
                for (int i = 0; i < 18; i++)
                {
                    float th = Mathf.PI * 2f * i / 18f;
                    float r = 60f + (float)R() * 8f;
                    var p = NoChao(MESA.x + Mathf.Cos(th) * r, MESA.z + Mathf.Sin(th) * r);
                    if (D2(p, new Vector3(12, 0, -4)) < 22f) continue;          // deixa a cachoeira livre
                    var nome = nomesGrandes[rnd.Next(nomesGrandes.Count)];
                    Pedra(rochas, grandes[nome], rGrandes[nome], p, (float)R() * 360f, 0.3f + (float)R() * 0.3f, 3f);
                }
            }
            // pedras médias espalhadas perto da pista
            if (nomesPedras.Count > 0)
                for (float s = 10f; s < Total; s += 45f)
                {
                    float lado = R() < 0.5 ? -1f : 1f;
                    var p = Ponto(s) + Direita(s) * lado * (muro + 8f + (float)R() * 14f);
                    p = NoChao(p.x, p.z);
                    if (p.y < LAVA_Y + 0.5f) continue;
                    var nome = nomesPedras[rnd.Next(nomesPedras.Count)];
                    Pedra(rochas, pedras[nome], rPedras[nome], p, (float)R() * 360f, 0.7f + (float)R() * 0.5f, 0.6f);
                }
            // arcos de pedra no lago a oeste e no lago sul
            foreach (var kv in arcos)
            {
                bool tres = kv.Key.Contains("3");
                Pedra(rochas, kv.Value, rArcos[kv.Key], tres ? new Vector3(-350f, LAVA_Y, 60f) : new Vector3(-80f, LAVA_Y, -235f), tres ? 70f : 10f, 0.6f, 1.5f);
            }
            Log.LogInfo("rochas: " + rochas.transform.childCount);
        }

        static void Ceu(GameObject raiz)
        {
            int NA = 96, NE = 40; float R = 3800f;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int j = 0; j <= NE; j++)
            {
                float el = -12f + 102f * j / NE;
                for (int i = 0; i <= NA; i++)
                {
                    float az = 360f * i / NA;
                    float ce = Mathf.Cos(el * Mathf.Deg2Rad);
                    v.Add(new Vector3(Mathf.Sin(az * Mathf.Deg2Rad) * ce, Mathf.Sin(el * Mathf.Deg2Rad), Mathf.Cos(az * Mathf.Deg2Rad) * ce) * R);
                    uv.Add(new Vector2((float)i / NA, Mathf.Clamp01((el + 8f) / 98f)));
                }
            }
            for (int j = 0; j < NE; j++)
                for (int i = 0; i < NA; i++)
                {
                    int a = j * (NA + 1) + i, b = a + 1, c = a + NA + 1, d = c + 1;
                    tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
                }
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            var t = Tex("v2_ceu.png"); t.wrapModeV = TextureWrapMode.Clamp;
            var mat = MatSprite(t, Color.white, 1000);
            var go = Desenho(raiz, "CeuVulcao", m, mat);
            go.AddComponent<CeuSegue>();
        }

        static void Fumaca(GameObject raiz)
        {
            float topo = Altura(VC.x, VC.z) + VCRAT;
            var fgo = new GameObject("ColunaFumaca"); fgo.transform.SetParent(raiz.transform, false);
            fgo.transform.position = new Vector3(VC.x, topo - 20f, VC.z);
            var mf = MatSprite(Tex("v2_fumaca.png"), Color.white, 3050);
            var col = fgo.AddComponent<ColunaFumaca>(); col.mat = mf;
            col.n = 48; col.raio = 50f; col.tamIni = 60f; col.tamFim = 260f; col.alfa = 0.9f; col.vida = 40f; col.subida = 8f;
            col.vento = new Vector3(5.5f, 0, 1.5f);
            col.corBaixo = new Color(0.95f, 0.45f, 0.22f); col.corAlto = new Color(0.20f, 0.15f, 0.16f); col.mistura = 5f;
            var bgo = new GameObject("BrilhoCratera"); bgo.transform.SetParent(raiz.transform, false);
            bgo.transform.position = new Vector3(VC.x, topo - 30f, VC.z);
            var br = bgo.AddComponent<ColunaFumaca>(); br.mat = matAditivo;
            br.n = 12; br.vida = 6f; br.subida = 6f; br.raio = 50f; br.tamIni = 60f; br.tamFim = 110f; br.alfa = 0.55f;
            br.corBaixo = new Color(1f, 0.55f, 0.15f); br.corAlto = new Color(1f, 0.3f, 0.08f); br.vento = Vector3.zero;
            // fumarolas no planalto e na beira do lago
            foreach (var pp in new[] { new Vector3(-25, 0, 80), new Vector3(150, 0, -190), new Vector3(-300, 0, 120) })
            {
                var g = new GameObject("Fumarola"); g.transform.SetParent(raiz.transform, false); g.transform.position = NoChao(pp.x, pp.z);
                var c = g.AddComponent<ColunaFumaca>(); c.mat = mf; c.n = 14; c.vida = 14f; c.subida = 4f; c.raio = 6f;
                c.tamIni = 8f; c.tamFim = 40f; c.alfa = 0.5f; c.corBaixo = new Color(0.9f, 0.6f, 0.45f); c.corAlto = new Color(0.45f, 0.38f, 0.38f); c.vento = new Vector3(1.5f, 0, 0.5f);
            }
        }

        static void Brasas(GameObject raiz, Vector3 pos, Vector3 caixa, float taxa)
        {
            var go = new GameObject("Brasas"); go.transform.SetParent(raiz.transform, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.4f, 0.05f, 1f));
            main.maxParticles = 500; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = taxa;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = caixa;
            var no = ps.noise; no.enabled = true; no.strength = 0.8f; no.frequency = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.3f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MatAditivo(Tex("v2_fumaca.png"), new Color(1f, 0.6f, 0.2f, 1f));
            ps.Play();
        }

        // ================= LUZ / CLIMA =================
        static void LuzVulcao(Scene cena)
        {
            AplicarClima();
            if (instancia != null) instancia.StartCoroutine(ReaplicarClima());
        }

        static IEnumerator ReaplicarClima()
        {
            foreach (float t in new[] { 0.5f, 2f, 5f })
            {
                yield return new WaitForSeconds(t);
                if (!Montada || !TemaVulcao) yield break;
                AplicarClima();
            }
        }

        static void AplicarClima()
        {
            // sol baixo a oeste-noroeste (mesmo lado do brilho pintado no céu)
            Vector3 sol = Quaternion.Euler(-24f, 300f, 0f) * Vector3.forward;
            foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                if (!luzesOriginais.ContainsKey(l)) luzesOriginais[l] = l.intensity;
                l.color = new Color(1f, 0.74f, 0.58f);
                l.intensity = luzesOriginais[l] * 1.0f;
                l.transform.rotation = Quaternion.LookRotation(-sol.normalized);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientLight = new Color(0.32f, 0.24f, 0.30f);
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.25f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.22f, 0.20f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.12f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.34f, 0.15f, 0.15f);
            RenderSettings.fogStartDistance = 380f;
            RenderSettings.fogEndDistance = 3200f;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflection = Reflexo();
            RenderSettings.reflectionIntensity = 0.55f;
            foreach (var rp in UnityEngine.Object.FindObjectsOfType<ReflectionProbe>()) rp.enabled = false;
        }

        static Dictionary<Light, float> luzesOriginais = new Dictionary<Light, float>();
    }

    // quem cai no vão do salto renasce depois dele (senão renasceria parado na beira da rampa)
    [HarmonyPatch(typeof(RcMultiPath), "RefreshRespawn")]
    static class Patch_RespawnSalto
    {
        static void Postfix(RcVehicleRaceStats _Stats)
        {
            if (!PistaNova.Ativa || !PistaNova.Montada || !PistaNova.TemVao || _Stats == null) return;
            var v = _Stats.GetVehicle(); if (v == null) return;
            float s = PistaNova.Projetar(v.GetRespawnPos());
            if (!PistaNova.NaZonaDoSalto(s)) return;
            float sl = PistaNova.S_SALTO + PistaNova.VAO + 10f;
            v.SetRespawnPos(PistaNova.Ponto(sl) + Vector3.up);
            v.SetRespawnOrientation(Quaternion.LookRotation(PistaNova.Direcao(sl), Vector3.up));
        }
    }
}
