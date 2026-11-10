// Ferramentas de filmagem do trailer (vão dentro do GKTeste.dll; só existem com TESTE=1).
// Comandos (pelo arquivo gkmod_cmd do GKTeste):
//   gravar NOME            começa a gravar quadros (60 fps, tela inteira) em BepInEx/gkcine/NOME/
//   parar                  para de gravar
//   q N                    espera N quadros (tempo do filme)
//   cam fixa px py pz ax ay az fov
//   cam trilho N fov  p1x p1y p1z a1x a1y a1z  p2x ...     (N quadros, curva suave por todos os pontos)
//   cam kart QUEM lat alt frente  olhaLat olhaAlt olhaFrente fov [suav]   QUEM = J (jogador) | F (kart à frente) | A (alvo do ovo) | número
//   cam pista s0 s1 N lat alt adiante fov                (voa sobre o traçado da pista nova)
//   cam orbita QUEM raio alt graus0 graus1 N fov
//   cam jogo               devolve a câmera ao jogo
//   hud on|off             mostra/esconde HUD e menus do jogo (as legendas ficam)
//   lento F                Time.timeScale
//   legenda N POS TAM texto [| linha de cima]           POS = baixo|centro|alto
//   ovo frente|cima        dá um ovo ao jogador e usa
//   mira on|off            ovo do jogador vai certeiro no kart da frente
//   chamar Tipo Metodo     chama método público sem parâmetros no primeiro objeto do tipo
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GKMod
{
    public class Cinema : MonoBehaviour
    {
        public static Cinema I;
        public static bool Gravando;
        public static int Quadro;              // quadros desde que o plugin começou (avança 1 por frame)
        static CultureInfo ci = CultureInfo.InvariantCulture;
        static float F(string s) { return float.Parse(s, ci); }

        public static void Garantir(GameObject host)
        {
            if (I == null) { I = host.AddComponent<Cinema>(); Camera.onPreCull += I.AntesDeRender; }
        }

        void Update() { Quadro++; }

        // ---------------- gravação ----------------
        string pasta; int nQuadros;
        IEnumerator Gravar(string nome)
        {
            pasta = Path.Combine(Path.Combine(Paths.BepInExRootPath, "gkcine"), nome);
            if (Directory.Exists(pasta)) foreach (var f in Directory.GetFiles(pasta)) File.Delete(f);
            Directory.CreateDirectory(pasta);
            Time.captureFramerate = 60;
            Gravando = true; nQuadros = 0;
            Teste.LogS("gravando " + nome + " em " + Screen.width + "x" + Screen.height);
            while (Gravando)
            {
                yield return new WaitForEndOfFrame();
                if (!Gravando) break;
                var t = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(pasta, nQuadros.ToString("00000") + ".jpg"), t.EncodeToJPG(93));
                Destroy(t);
                nQuadros++;
            }
            Time.captureFramerate = 0;
            Teste.LogS("gravação " + nome + ": " + nQuadros + " quadros");
        }

        // ---------------- câmera ----------------
        enum Modo { Jogo, Fixa, Trilho, Kart, Pista, Orbita }
        Modo modo = Modo.Jogo;
        Vector3 cPos, cAlvo; float cFov = 50f;
        List<Vector3> tPos = new List<Vector3>(), tAlvo = new List<Vector3>();
        int t0, tN;
        string quem = "J"; Vector3 offPos, offOlha; float suav = 0f; Vector3 posSuave; bool temSuave;
        float s0, s1, lat, alt, adiante;
        float orRaio, orAlt, orG0, orG1;

        Camera CamPrincipal()
        {
            var c = Camera.main;
            return c;
        }

        void AntesDeRender(Camera c)
        {
            if (modo == Modo.Jogo || c == null || c != CamPrincipal()) return;
            Aplicar(c);
        }

        void LateUpdate()
        {
            if (hudOculto && Quadro % 20 == 0) Hud(false);
            if (modo == Modo.Jogo) return;
            var c = CamPrincipal(); if (c != null) Aplicar(c);
        }

        static Vector3 CatmullRom(List<Vector3> p, float u)
        {
            if (p.Count == 1) return p[0];
            float x = u * (p.Count - 1);
            int i = Mathf.Min((int)x, p.Count - 2); float t = x - i;
            Vector3 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Count - 1)];
            return 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);
        }

        static float Suave(float u) { u = Mathf.Clamp01(u); return u * u * (3 - 2 * u); }

        int quadroAplicado = -1; Vector3 ultPos, ultAlvo;
        void Aplicar(Camera c)
        {
            if (quadroAplicado != Quadro)
            {
                quadroAplicado = Quadro;
                Calcular(out ultPos, out ultAlvo);
            }
            c.transform.position = ultPos;
            var dir = ultAlvo - ultPos;
            if (dir.sqrMagnitude > 1e-4f) c.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            c.fieldOfView = cFov;
            if (c.farClipPlane < 5000f) c.farClipPlane = 5000f;
        }

        void Calcular(out Vector3 pos, out Vector3 alvo)
        {
            pos = cPos; alvo = cAlvo;
            float u = tN > 0 ? (float)(Quadro - t0) / tN : 1f;
            switch (modo)
            {
                case Modo.Trilho:
                    {
                        float e = Suave(u);
                        pos = CatmullRom(tPos, e); alvo = CatmullRom(tAlvo, e);
                    }
                    break;
                case Modo.Kart:
                    {
                        var k = AcharKart(quem);
                        if (k == null) break;
                        var tr = k.Transform;
                        Vector3 f = tr.forward; f.y = 0; f.Normalize();
                        Vector3 r = Vector3.Cross(Vector3.up, f);
                        Vector3 p = tr.position + r * offPos.x + Vector3.up * offPos.y + f * offPos.z;
                        alvo = tr.position + r * offOlha.x + Vector3.up * offOlha.y + f * offOlha.z;
                        if (suav > 0 && temSuave) p = Vector3.Lerp(posSuave, p, 1f - suav);
                        posSuave = p; temSuave = true;
                        pos = p;
                    }
                    break;
                case Modo.Pista:
                    {
                        float s = Mathf.Lerp(s0, s1, Suave(u) * 0.35f + u * 0.65f);
                        Vector3 c = PontoPista(s), r = DireitaPista(s);
                        pos = c + r * lat + Vector3.up * alt;
                        alvo = PontoPista(s + adiante) + Vector3.up * 1.0f;
                    }
                    break;
                case Modo.Orbita:
                    {
                        var k = AcharKart(quem);
                        if (k == null) break;
                        float g = Mathf.Lerp(orG0, orG1, Suave(u)) * Mathf.Deg2Rad;
                        var tr = k.Transform;
                        Vector3 f = tr.forward; f.y = 0; f.Normalize();
                        Vector3 r = Vector3.Cross(Vector3.up, f);
                        pos = tr.position + (f * Mathf.Cos(g) + r * Mathf.Sin(g)) * orRaio + Vector3.up * orAlt;
                        alvo = tr.position + Vector3.up * 0.7f;
                    }
                    break;
            }
        }

        // pista nova (GKPistaNova.dll) por reflexão
        static MethodInfo mPonto, mDireita;
        static Vector3 PontoPista(float s)
        {
            if (mPonto == null) { var t = Type.GetType("GKMod.PistaNova, GKPistaNova"); mPonto = t.GetMethod("Ponto"); mDireita = t.GetMethod("Direita"); }
            return (Vector3)mPonto.Invoke(null, new object[] { s });
        }
        static Vector3 DireitaPista(float s) { PontoPista(0); return (Vector3)mDireita.Invoke(null, new object[] { s }); }
        static Type tPista() { return Type.GetType("GKMod.PistaNova, GKPistaNova"); }
        static float ProjetarPista(Vector3 p) { return (float)tPista().GetMethod("Projetar").Invoke(null, new object[] { p }); }
        static Vector3 DirecaoPista(float s) { return (Vector3)tPista().GetMethod("Direcao").Invoke(null, new object[] { s }); }
        static float TotalPista() { var f = tPista().GetField("Total"); if (f != null) return Convert.ToSingle(f.GetValue(null)); var pp = tPista().GetProperty("Total"); return Convert.ToSingle(pp.GetValue(null, null)); }

        public static Kart Jogador()
        {
            foreach (var k in FindObjectsOfType<Kart>()) if (k.GetControlType() == RcVehicle.ControlType.Human) return k;
            return null;
        }

        Kart alvoFixo;
        Kart AcharKart(string q)
        {
            if (q == "J") return Jogador();
            if (q == "A")
            {
                var t = Type.GetType("GKMod.Ovo, GKOvo");
                var k = t != null ? t.GetField("UltimoAlvo").GetValue(null) as Kart : null;
                return k != null ? k : Jogador();
            }
            if (q == "F")
            {
                if (alvoFixo != null) return alvoFixo;
                var j = Jogador(); if (j == null) return null;
                Kart melhor = null; float md = 1e9f;
                foreach (var k in FindObjectsOfType<Kart>())
                {
                    if (k == j) continue;
                    Vector3 d = k.Transform.position - j.Transform.position;
                    float fr = Vector3.Dot(d, j.Transform.forward);
                    if (fr < 2f) continue;
                    if (d.magnitude < md) { md = d.magnitude; melhor = k; }
                }
                alvoFixo = melhor;
                return melhor;
            }
            int n;
            if (int.TryParse(q, out n))
            {
                var ks = FindObjectsOfType<Kart>();
                Array.Sort(ks, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
                return n < ks.Length ? ks[n] : null;
            }
            return Jogador();
        }

        // ---------------- HUD ----------------
        List<Behaviour> escondidos = new List<Behaviour>();
        bool hudOculto;
        void Hud(bool on)
        {
            hudOculto = !on;
            if (on)
            {
                foreach (var b in escondidos) if (b != null) b.enabled = true;
                escondidos.Clear();
                return;
            }
            var principal = Camera.main;
            foreach (var c in Camera.allCameras)
                if (c != principal && c.enabled) { c.enabled = false; escondidos.Add(c); }
            foreach (var cv in FindObjectsOfType<Canvas>())
                if (cv.enabled && cv.isRootCanvas && cv.gameObject != (legendaCanvas != null ? legendaCanvas.gameObject : null) && !cv.name.StartsWith("GKCine"))
                { cv.enabled = false; escondidos.Add(cv); }
        }

        // ---------------- legendas ----------------
        Canvas legendaCanvas;
        public static TMP_FontAsset Fonte()
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f.name == "Garfield_TMP") return f;
            return null;
        }

        Canvas CanvasLegenda()
        {
            if (legendaCanvas != null) return legendaCanvas;
            var go = new GameObject("GKCine_Legendas");
            DontDestroyOnLoad(go);
            legendaCanvas = go.AddComponent<Canvas>();
            legendaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            legendaCanvas.sortingOrder = 32000;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
            var f = Fonte();
            if (f != null)
            {
                string teste = "ÃÇÉÊÍÓÔÚÁÀÕ";
                string falta = ""; foreach (char ch in teste) if (!f.HasCharacter(ch)) falta += ch;
                Teste.LogS("fonte Garfield_TMP: faltam [" + falta + "]");
            }
            return legendaCanvas;
        }

        void Legenda(int n, string pos, float tam, string texto)
        {
            string cima = null;
            int bar = texto.IndexOf('|');
            if (bar >= 0) { cima = texto.Substring(bar + 1).Trim(); texto = texto.Substring(0, bar).Trim(); }
            var cv = CanvasLegenda();
            var go = new GameObject("Legenda");
            go.transform.SetParent(cv.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1800, 400);
            float y = pos == "alto" ? 330f : pos == "centro" ? 0f : -300f;
            rt.anchoredPosition = new Vector2(0, y);
            var lg = go.AddComponent<LegendaAnim>();
            lg.Montar(texto, cima, tam, n);
        }

        // ---------------- comandos ----------------
        public static IEnumerator Comando(string[] p)
        {
            var c = I;
            switch (p[0])
            {
                case "gravar": Teste.LogS("[gravar] " + p[1] + " começa no frame " + (Time.frameCount + 1)); c.StartCoroutine(c.Gravar(p[1])); yield return null; break;
                case "ate_s":
                    {
                        float alvoS = F(p[1]); int lim = p.Length > 2 ? int.Parse(p[2]) : 6000;
                        for (int i = 0; i < lim; i++)
                        {
                            var j = Jogador();
                            if (j != null)
                            {
                                float s = ProjetarPista(j.Transform.position);
                                float d = s - alvoS; float tot = TotalPista();
                                d = d - tot * Mathf.Round(d / tot);
                                if (d >= 0 && d < 40f) break;
                            }
                            yield return null;
                        }
                        Teste.LogS("ate_s " + p[1] + " no frame " + Time.frameCount);
                    }
                    break;
                case "parar": Gravando = false; yield return null; yield return null; break;
                case "q": { int n = int.Parse(p[1]); for (int i = 0; i < n; i++) yield return null; } break;
                case "lento": Time.timeScale = F(p[1]); break;
                case "hud": c.Hud(p[1] == "on"); break;
                case "selo_reinicia":
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                        if (t.name == "GKOvoEdition" && t.gameObject.activeInHierarchy) { t.gameObject.SetActive(false); t.gameObject.SetActive(true); }
                    break;
                case "fundo":
                    {
                        var cv = c.CanvasLegenda();
                        var f = cv.transform.Find("Fundo");
                        if (p[1] == "nada") { if (f != null) UnityEngine.Object.Destroy(f.gameObject); break; }
                        if (f == null)
                        {
                            var g = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
                            g.transform.SetParent(cv.transform, false); g.transform.SetAsFirstSibling();
                            var r = (RectTransform)g.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                            f = g.transform;
                        }
                        f.GetComponent<Image>().color = p[1] == "branco" ? Color.white : Color.black;
                    }
                    break;
                case "legenda":
                    {
                        string txt = string.Join(" ", p, 4, p.Length - 4);
                        c.Legenda(int.Parse(p[1]), p[2], F(p[3]), txt);
                    }
                    break;
                case "cam":
                    {
                        c.t0 = Quadro + 1; c.tN = 0; c.temSuave = false; c.alvoFixo = null;
                        switch (p[1])
                        {
                            case "jogo": c.modo = Modo.Jogo; break;
                            case "mancha":
                                {
                                    var t = Type.GetType("GKMod.Ovo, GKOvo");
                                    Vector3 m = (Vector3)t.GetField("UltimaMancha").GetValue(null);
                                    float s = ProjetarPista(m);
                                    Vector3 dir = DirecaoPista(s), r = DireitaPista(s);
                                    float dist = F(p[2]), alt = F(p[3]);
                                    c.modo = Modo.Fixa;
                                    c.cPos = m + dir * dist + r * F(p[4]) + Vector3.up * alt;
                                    c.cAlvo = m - dir * F(p[5]) + Vector3.up * 0.8f;
                                    c.cFov = F(p[6]);
                                }
                                break;
                            case "pfixa":
                                {
                                    // câmera parada relativa à pista: pfixa s lat alt sAlvo latAlvo altAlvo fov
                                    float s = F(p[2]); Vector3 cc0 = PontoPista(s), rr0 = DireitaPista(s);
                                    float sa = F(p[5]); Vector3 cc1 = PontoPista(sa), rr1 = DireitaPista(sa);
                                    c.modo = Modo.Fixa;
                                    c.cPos = cc0 + rr0 * F(p[3]) + Vector3.up * F(p[4]);
                                    c.cAlvo = cc1 + rr1 * F(p[6]) + Vector3.up * F(p[7]);
                                    c.cFov = F(p[8]);
                                }
                                break;
                            case "fixa":
                                c.modo = Modo.Fixa; c.cPos = new Vector3(F(p[2]), F(p[3]), F(p[4])); c.cAlvo = new Vector3(F(p[5]), F(p[6]), F(p[7])); c.cFov = F(p[8]); break;
                            case "trilho":
                                {
                                    c.modo = Modo.Trilho; c.tN = int.Parse(p[2]); c.cFov = F(p[3]);
                                    c.tPos.Clear(); c.tAlvo.Clear();
                                    for (int i = 4; i + 5 < p.Length; i += 6)
                                    {
                                        c.tPos.Add(new Vector3(F(p[i]), F(p[i + 1]), F(p[i + 2])));
                                        c.tAlvo.Add(new Vector3(F(p[i + 3]), F(p[i + 4]), F(p[i + 5])));
                                    }
                                }
                                break;
                            case "kart":
                                c.modo = Modo.Kart; c.quem = p[2];
                                c.offPos = new Vector3(F(p[3]), F(p[4]), F(p[5])); c.offOlha = new Vector3(F(p[6]), F(p[7]), F(p[8]));
                                c.cFov = F(p[9]); c.suav = p.Length > 10 ? F(p[10]) : 0f; break;
                            case "pista":
                                c.modo = Modo.Pista; c.s0 = F(p[2]); c.s1 = F(p[3]); c.tN = int.Parse(p[4]);
                                c.lat = F(p[5]); c.alt = F(p[6]); c.adiante = F(p[7]); c.cFov = F(p[8]); break;
                            case "orbita":
                                c.modo = Modo.Orbita; c.quem = p[2]; c.orRaio = F(p[3]); c.orAlt = F(p[4]); c.orG0 = F(p[5]); c.orG1 = F(p[6]);
                                c.tN = int.Parse(p[7]); c.cFov = F(p[8]); break;
                        }
                    }
                    break;
                case "ovo":
                    {
                        var j = Jogador();
                        if (j == null) break;
                        bool tras = p[1] == "cima";
                        var bm = j.GetBonusMgr();
                        bm.ResetItems();
                        yield return null; yield return null;
                        bm.SetItem(BonusCategory.DIAMOND, 1, -1, true);
                        // espera a roleta e, se pedido, um kart na posição certa (frente: 15-55 m à frente; cima: 5-28 m atrás)
                        bool condicao = p.Length > 2 && p[2] == "espera";
                        int esperou = 0;
                        for (int i = 0; i < 1200; i++)
                        {
                            yield return null;
                            if (bm.GetItem(0) == BonusCategory.DIAMOND && (bm.HUDBonus == null || bm.HUDBonus.Slots[0].State == 0))
                            {
                                if (condicao && esperou < 900)
                                {
                                    bool ok = false;
                                    foreach (var k in FindObjectsOfType<Kart>())
                                    {
                                        if (k == j) continue;
                                        Vector3 d = k.Transform.position - j.Transform.position;
                                        float fr = Vector3.Dot(d, j.Transform.forward), la = Mathf.Abs(Vector3.Dot(d, j.Transform.right));
                                        if (!tras && fr > 15f && fr < 55f && la < 10f) ok = true;
                                        if (tras && fr < -5f && fr > -28f && la < 10f) ok = true;
                                    }
                                    if (!ok) { esperou++; continue; }
                                }
                                bm.ActivateBonus(tras);
                                yield return null;
                                if (bm.GetItem(0) != BonusCategory.DIAMOND) { Teste.LogS("ovo usado (" + p[1] + ") no quadro " + Quadro); break; }
                            }
                        }
                    }
                    break;
                case "espera_mancha":
                    {
                        // espera uma mancha nova com pelo menos 2 karts chegando (5-45 m antes dela) e fixa a câmera ali
                        var t = Type.GetType("GKMod.Ovo, GKOvo");
                        int ultimo = (int)t.GetField("QuadroUltimoEstouro").GetValue(null);
                        int lim = int.Parse(p[1]);
                        for (int i = 0; i < lim; i++)
                        {
                            yield return null;
                            int q = (int)t.GetField("QuadroUltimoEstouro").GetValue(null);
                            if (q == ultimo) continue;
                            ultimo = q;
                            Vector3 m = (Vector3)t.GetField("UltimaMancha").GetValue(null);
                            float sm = ProjetarPista(m); Vector3 cm = PontoPista(sm);
                            if (Mathf.Abs(cm.y - m.y) > 2.5f || (new Vector2(cm.x - m.x, cm.z - m.z)).magnitude > 12f) continue;   // fora da pista
                            int n = 0;
                            foreach (var k in FindObjectsOfType<Kart>())
                            {
                                Vector3 d = m - k.Transform.position;
                                float fr = Vector3.Dot(d, k.Transform.forward);
                                if (fr > 5f && fr < 45f && Mathf.Abs(Vector3.Dot(d, k.Transform.right)) < 9f) n++;
                            }
                            if (n < 2) continue;
                            Vector3 dir = DirecaoPista(sm), r = DireitaPista(sm);
                            c.modo = Modo.Fixa; c.cPos = m + dir * 13f + r * -2.5f + Vector3.up * 2.6f; c.cAlvo = m - dir * 6f + Vector3.up * 0.8f; c.cFov = 55f;
                            Teste.LogS("espera_mancha: " + n + " karts chegando na mancha em " + m.ToString("F0"));
                            break;
                        }
                    }
                    break;
                case "mira":
                    {
                        var t = Type.GetType("GKMod.Ovo, GKOvo");
                        if (t != null) t.GetField("MiraCinema").SetValue(null, p[1] == "on");
                    }
                    break;
                case "chamar":
                    {
                        var tipo = HarmonyLib.AccessTools.TypeByName(p[1]);
                        var o = tipo != null ? FindObjectOfType(tipo) : null;
                        if (o != null) { var m = tipo.GetMethod(p[2], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (m != null) m.Invoke(o, null); }
                        Teste.LogS("chamar " + p[1] + "." + p[2] + " -> " + (o != null));
                    }
                    break;
                default:
                    {
                        var e = CinemaExtra.Comando(p);
                        if (e != null) while (e.MoveNext()) yield return e.Current;
                    }
                    break;
            }
        }
    }

    // texto animado no estilo do jogo: pula para dentro, segura, some
    public class LegendaAnim : MonoBehaviour
    {
        int inicio, dur;
        RectTransform rt;
        List<TextMeshProUGUI> textos = new List<TextMeshProUGUI>();
        float rot0;

        public void Montar(string texto, string cima, float tam, int n)
        {
            rt = GetComponent<RectTransform>();
            inicio = Cinema.Quadro; dur = n;
            rot0 = -2f - 2f * ((Mathf.Abs(texto.GetHashCode()) % 100) / 100f);
            float y = 0;
            if (cima != null)
            {
                Criar(cima, tam * 0.42f, new Vector2(0, tam * 0.62f), new Color(1f, 1f, 1f), new Color(1f, 0.92f, 0.75f), 0.22f);
                y = -tam * 0.12f;
            }
            Criar(texto, tam, new Vector2(0, y), new Color(1f, 0.93f, 0.35f), new Color(1f, 0.52f, 0.05f), 0.28f);
            Atualizar();
        }

        void Criar(string s, float tam, Vector2 pos, Color c1, Color c2, float contorno)
        {
            var f = Cinema.Fonte();
            // sombra
            for (int k = 0; k < 2; k++)
            {
                var go = new GameObject(k == 0 ? "Sombra" : "Texto");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(1800, tam * 1.4f);
                r.anchoredPosition = pos + (k == 0 ? new Vector2(tam * 0.04f, -tam * 0.07f) : Vector2.zero);
                var t = go.AddComponent<TextMeshProUGUI>();
                if (f != null) t.font = f;
                t.text = s; t.fontSize = tam; t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false;
                t.fontStyle = FontStyles.Bold;
                if (k == 0) { t.color = new Color(0.12f, 0.03f, 0.02f, 0.75f); }
                else
                {
                    t.enableVertexGradient = true;
                    t.colorGradient = new VertexGradient(c1, c1, c2, c2);
                    t.outlineWidth = contorno; t.outlineColor = new Color32(70, 20, 8, 255);
                }
                textos.Add(t);
            }
        }

        void Update() { Atualizar(); }

        void Atualizar()
        {
            int q = Cinema.Quadro - inicio;
            float s, a = 1f;
            if (q < 9) { float u = q / 9f; s = Mathf.Lerp(0.3f, 1.14f, 1 - (1 - u) * (1 - u)); }
            else if (q < 16) { float u = (q - 9) / 7f; s = Mathf.Lerp(1.14f, 1f, u * u * (3 - 2 * u)); }
            else s = 1f + 0.03f * Mathf.Clamp01((q - 16f) / Mathf.Max(1f, dur - 26f));
            int sai = dur - q;
            if (sai < 10) { float u = 1 - Mathf.Max(0, sai) / 10f; a = 1 - u; s *= 1 + 0.12f * u; }
            rt.localScale = Vector3.one * s;
            rt.localRotation = Quaternion.Euler(0, 0, rot0 * (q < 16 ? 1.6f - 0.6f * Mathf.Clamp01(q / 16f) : 1f));
            foreach (var t in textos) t.alpha = a * (t.name == "Sombra" ? 0.75f : 1f);
            if (q >= dur) Destroy(gameObject);
        }
    }
}
