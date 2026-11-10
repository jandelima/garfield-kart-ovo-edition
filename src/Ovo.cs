// Item OVO: substitui o diamante.
// - Para a frente: voa em arco e estoura ao tocar chão, parede ou kart.
// - "Para trás": joga o ovo para cima (~5 m); ele cai onde foi jogado e estoura.
// - Acertou um kart em cheio: estoura na hora e esse kart escorrega.
// - No estouro, em vez do domo do diamante, fica uma mancha de ovo no chão (mesmo raio do domo) por 8 s.
//   Quem está com as rodas no chão dentro da mancha escorrega (efeito SPIN da torta), inclusive quem jogou.
//   Rodar só ao ENTRAR na mancha (quem já rodou e continua dentro não roda de novo) + 1,5 s de imunidade.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace GKMod
{
    [BepInPlugin("gkmod.ovo", "GK Ovo", "1.0.0")]
    public class Ovo : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public const float DURACAO = 8f, FADE = 1.6f, IMUNIDADE = 1.5f;
        public static float Raio = 0f;                 // raio do domo do diamante (lido do próprio jogo)
        public static bool Teste, Vitrine;
        public static bool MiraCinema;          // trailer: ovo para a frente vai certeiro no kart mais próximo à frente
        public static Kart UltimoAlvo;
        public static Vector3 UltimaMancha; public static int QuadroUltimoEstouro = -1;
        public static bool SoBots;              // trailer: todo item dos bots vira ovo
        public static float DuracaoExtra = 0f;
        static Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
        static Sprite spItem;
        static AudioClip somEstouro;
        bool tutoTrocado;

        void Awake()
        {
            Log = Logger;
            Teste = File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_ovo_teste"));
            Vitrine = File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_ovo_vitrine"));
            SoBots = File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_ovo_bots"));
            new Harmony("gkmod.ovo").PatchAll(typeof(Ovo).Assembly);
            Logger.LogInfo("Ovo carregado (substitui o diamante)." + (Teste ? " [teste]" : ""));
        }

        void Update()
        {
            if (!tutoTrocado)
            {
                var ui = UnityEngine.Object.FindObjectOfType<UISprites>();
                if (ui != null && UISprites.TutoBonusIcons != null && SpItem != null)
                {
                    var arr = UISprites.TutoBonusIcons.RawArray;
                    foreach (var t in new[] { TutorialItem.BONUS_TYPE.DIAMOND_FORWARD, TutorialItem.BONUS_TYPE.DIAMOND_BACKWARD })
                        if ((int)t < arr.Length) arr[(int)t] = SpItem;
                    tutoTrocado = true;
                }
            }
            if (Teste) TesteAutomatico.Passo();
            if (Vitrine) TesteAutomatico.Vitrine();
        }

        // ---------------- recursos ----------------
        public static Texture2D Tex(string nome)
        {
            Texture2D t;
            if (texs.TryGetValue(nome, out t) && t != null) return t;
            using (var s = typeof(Ovo).Assembly.GetManifestResourceStream(nome))
            {
                if (s == null) { Log.LogWarning("Recurso não encontrado: " + nome); return Texture2D.whiteTexture; }
                var buf = new byte[s.Length]; s.Read(buf, 0, buf.Length);
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                t.LoadImage(buf);
            }
            t.wrapMode = TextureWrapMode.Clamp; t.anisoLevel = 4; t.name = nome;
            UnityEngine.Object.DontDestroyOnLoad(t);
            texs[nome] = t;
            return t;
        }

        static Dictionary<Color, Texture2D> texCor = new Dictionary<Color, Texture2D>();
        public static Texture2D TexCor(Color c)
        {
            Texture2D t;
            if (texCor.TryGetValue(c, out t) && t != null) return t;
            t = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px); t.Apply();
            UnityEngine.Object.DontDestroyOnLoad(t);
            texCor[c] = t;
            return t;
        }

        public static Sprite SpItem
        {
            get
            {
                if (spItem == null) { var t = Tex("item_ovo.png"); spItem = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); spItem.name = "item_ovo"; }
                return spItem;
            }
        }

        static Shader shStd, shSprite, shGK, shGKTransp;
        static bool shBuscado;
        static void BuscarShaders()
        {
            if (shBuscado) return;
            shBuscado = true;
            shStd = Shader.Find("Standard");
            shSprite = Shader.Find("Sprites/Default");
            var nomes = new List<string>();
            foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
            {
                if (sh.name == "GKHD/Standard") shGK = sh;
                if (sh.name == "GKHD/FX/SH_FX_Standard_Transparent") shGKTransp = sh;
                string n = sh.name.ToLower();
                if (n.Contains("transp") || n.Contains("alpha") || n.Contains("fade") || n.Contains("water") || n.Contains("glass") || n.Contains("decal")) nomes.Add(sh.name);
            }
            Log.LogInfo("shaders: Standard=" + (shStd != null) + " GKHD/Standard=" + (shGK != null) + " Sprites=" + (shSprite != null) + " | translúcidos: " + string.Join(", ", nomes.ToArray()));
        }

        // casca: Standard opaco (brilho leve); sem Standard, sprite tingido pela luz da cena
        static Material matCasca;
        public static Material MatCasca()
        {
            if (matCasca != null) return matCasca;
            BuscarShaders();
            if (shGK != null)
            {
                // shader do jogo: _MainTex, normal no formato do jogo, SSAO = (metal, brilho, AO, emissão)
                matCasca = new Material(shGK);
                matCasca.SetTexture("_MainTex", Tex("ovo_casca.png"));
                matCasca.SetTexture("_MainTex_Normal", TexCor(new Color(1f, 0.5f, 0.5f, 0.5f)));
                matCasca.SetTexture("_MainTex_SSAO", TexCor(new Color(0f, 0.55f, 1f, 0f)));
                matCasca.SetColor("_Color", new Color(1.25f, 1.22f, 1.18f, 1f));
                matCasca.SetColor("_DiffuseTeint", Color.white);
                matCasca.SetFloat("_IsFresnel", 1f); matCasca.EnableKeyword("_ISFRESNEL_ON");
                matCasca.SetColor("_FresnelTeint", new Color(1f, 0.95f, 0.85f, 1f));
                matCasca.SetFloat("_FresnelIntensity", 0.6f);
            }
            else if (shStd != null)
            {
                matCasca = new Material(shStd);
                matCasca.mainTexture = Tex("ovo_casca.png");
                matCasca.SetFloat("_Glossiness", 0.42f);
                matCasca.SetFloat("_Metallic", 0f);
                matCasca.color = Color.white;
            }
            else
            {
                matCasca = new Material(shSprite);
                matCasca.mainTexture = Tex("ovo_casca.png");
                matCasca.color = LuzDaCena();
            }
            matCasca.name = "GKOvo_Casca";
            return matCasca;
        }

        // mancha: Standard em modo "Fade" (molhado e brilhante); senão sprite tingido pela luz
        public static Material NovaMatMancha(out bool lit)
        {
            BuscarShaders();
            Material m;
            lit = false;
            if (false && shGKTransp != null)
            {
                m = new Material(shGKTransp);
                m.SetTexture("_MainTex", Tex("ovo_mancha.png"));
                m.SetFloat("_EmissivePower", 0.0f);
                m.SetFloat("_MetalicPower", 0f);
                m.SetFloat("_AlphaPower", 1f);
                m.SetFloat("_DeathFadeDistance", 0f);
                m.SetColor("_ColorTeint", Color.white);
                m.renderQueue = 3000;
                m.name = "GKOvo_Mancha";
                lit = true;
                return m;
            }
            if (shStd != null)
            {
                m = new Material(shStd);
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetFloat("_Glossiness", 0.86f);
                m.SetFloat("_Metallic", 0f);
                m.renderQueue = 3000;
                lit = true;
            }
            else
            {
                m = new Material(shSprite);
                m.renderQueue = 3000;
            }
            m.mainTexture = Tex("ovo_mancha.png");
            m.name = "GKOvo_Mancha";
            return m;
        }

        public static Material NovaMatGota()
        {
            BuscarShaders();
            var m = new Material(shSprite);
            m.mainTexture = Tex("ovo_gota.png");
            m.renderQueue = 3001;
            return m;
        }

        // luz aproximada da cena num chão plano (para os materiais sem iluminação)
        public static Color LuzDaCena()
        {
            Color c = RenderSettings.ambientLight;
            foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional && l.isActiveAndEnabled)
                {
                    float k = Mathf.Max(0.25f, Vector3.Dot(Vector3.up, -l.transform.forward)) * l.intensity;
                    c += l.color * k * 0.85f;
                    break;
                }
            c *= 0.78f;
            return new Color(Mathf.Clamp(c.r, 0.3f, 0.9f), Mathf.Clamp(c.g, 0.3f, 0.9f), Mathf.Clamp(c.b, 0.3f, 0.9f), 1f);
        }

        // som do ovo quebrando: estalos de casca + "splash" molhado + baque + gotinhas
        public static AudioClip SomEstouro()
        {
            if (somEstouro != null) return somEstouro;
            int sr = 44100, n = (int)(sr * 0.7f);
            var d = new float[n];
            var rnd = new System.Random(3);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float ruido = (float)(rnd.NextDouble() * 2 - 1);
                float v = 0;
                // estalos (casca): 3 cliques curtos e agudos
                foreach (float t0 in new[] { 0f, 0.011f, 0.027f })
                {
                    float dt = t - t0;
                    if (dt >= 0 && dt < 0.006f) v += ruido * (1 - dt / 0.006f) * 0.9f;
                }
                // splash: ruído com passa-baixa que fecha (2.6 kHz -> 350 Hz)
                float ts = t - 0.018f;
                if (ts >= 0)
                {
                    float fc = Mathf.Lerp(2600f, 350f, Mathf.Clamp01(ts / 0.3f));
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / sr);
                    lp += (ruido - lp) * a; lp2 += (lp - lp2) * a;
                    float env = Mathf.Min(1f, ts / 0.006f) * Mathf.Exp(-ts / 0.11f);
                    v += lp2 * env * 2.6f;
                }
                // baque grave
                if (ts >= 0) v += Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(95f, 48f, Mathf.Clamp01(ts / 0.15f)) * ts) * Mathf.Exp(-ts / 0.07f) * 0.45f;
                // gotinhas (chirps descendentes)
                foreach (float t0 in new[] { 0.14f, 0.21f, 0.30f })
                {
                    float dt = t - t0;
                    if (dt >= 0 && dt < 0.045f) v += Mathf.Sin(2 * Mathf.PI * (900f - 9000f * dt) * dt) * Mathf.Sin(Mathf.PI * dt / 0.045f) * 0.16f;
                }
                d[i] = v;
            }
            float mx = 0; foreach (var x in d) mx = Mathf.Max(mx, Mathf.Abs(x));
            for (int i = 0; i < n; i++) d[i] = d[i] / mx * 0.85f;
            somEstouro = AudioClip.Create("ovo_estouro", n, 1, sr, false);
            somEstouro.SetData(d, 0);
            return somEstouro;
        }

        // malha de ovo (esfera esticada, mais fina em cima)
        static Mesh malhaOvo;
        public static Mesh MalhaOvo()
        {
            if (malhaOvo != null) return malhaOvo;
            int nu = 22, nv = 16;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int j = 0; j <= nv; j++)
            {
                float fv = (float)j / nv, ang = Mathf.PI * fv;
                float y = -Mathf.Cos(ang), rad = Mathf.Sin(ang) * (1f - 0.13f * y);
                for (int i = 0; i <= nu; i++)
                {
                    float fu = (float)i / nu, a = 2 * Mathf.PI * fu;
                    v.Add(new Vector3(Mathf.Cos(a) * rad * 0.21f, y * 0.28f, Mathf.Sin(a) * rad * 0.21f));
                    uv.Add(new Vector2(fu * 2f, fv));
                }
            }
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int a = j * (nu + 1) + i, b = a + nu + 1;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b); tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                }
            malhaOvo = new Mesh(); malhaOvo.SetVertices(v); malhaOvo.SetUVs(0, uv); malhaOvo.SetTriangles(tri, 0);
            malhaOvo.RecalculateNormals(); malhaOvo.RecalculateBounds();
            var nr = malhaOvo.normals; // garante normais para fora
            if (Vector3.Dot(nr[nu + 2], v[nu + 2]) < 0) { tri.Reverse(); malhaOvo.SetTriangles(tri, 0); malhaOvo.RecalculateNormals(); }
            return malhaOvo;
        }

        // ---------------- regras do escorregão ----------------
        static Dictionary<int, float> imuneAte = new Dictionary<int, float>();

        public static bool Controlo(Kart k)
        {
            var n = GkNetMgr.Instance;
            if (n == null || n.IsDisconnected) return true;
            if (k.Driver != null && k.Driver.IsLocal) return true;
            return k.Driver != null && !k.Driver.IsHuman && n.IsMasterClient;
        }

        public static void AtualizarImunidade(Kart k)
        {
            var spin = k.GetBonusMgr().GetBonusEffectMgr().GetBonusEffect(EBonusEffect.BONUSEFFECT_SPIN);
            if (spin != null && spin.Activated) imuneAte[k.GetInstanceID()] = Time.time + IMUNIDADE;
        }

        // retorna true se o kart rodou
        public static bool Escorregar(Kart k, Kart lancador)
        {
            if (k == null || k.Driver == null) return false;
            var mgr = k.GetBonusMgr().GetBonusEffectMgr();
            var spin = mgr.GetBonusEffect(EBonusEffect.BONUSEFFECT_SPIN);
            if (spin == null || spin.Activated) return false;
            float ate;
            if (imuneAte.TryGetValue(k.GetInstanceID(), out ate) && Time.time < ate) return false;
            if (k.Driver.IsInvulnerable) return false;
            var parf = mgr.GetBonusEffect(EBonusEffect.BONUSEFFECT_ATTRACTED) as ParfumeBonusEffect;
            if (parf != null && parf.Activated) return false;
            mgr.ActivateBonusEffect(EBonusEffect.BONUSEFFECT_SPIN);
            if (!spin.Activated) return false;
            imuneAte[k.GetInstanceID()] = Time.time + IMUNIDADE + 3f;
            try { k.KartSound.PlayVoice(KartSound.EVoices.Bad); } catch (Exception) { }
            if (lancador != null && lancador != k)
            {
                try { lancador.KartSound.PlayVoice(KartSound.EVoices.Good); lancador.Anim.LaunchSuccessAnim(true); } catch (Exception) { }
            }
            Log.LogInfo("[ovo] escorregou: " + k.name + (lancador == k ? " (o próprio lançador)" : ""));
            return true;
        }

        public static List<Kart> Karts()
        {
            return new List<Kart>(UnityEngine.Object.FindObjectsOfType<Kart>());
        }
    }

    // ---------------- comportamento do ovo (fica no objeto do diamante) ----------------
    public class OvoItem : MonoBehaviour
    {
        public DiamondBonusEntity ent;
        public bool voando, estourou, fim;
        Vector3 vel; float t; bool paraCima;
        Kart lancador, atingido;
        Transform visual; Vector3 eixoGiro; float giro;
        List<Kart> karts = new List<Kart>();
        static readonly FieldInfo fEstado = AccessTools.Field(typeof(DiamondBonusEntity), "m_diamondState");
        static readonly FieldInfo fCollider = AccessTools.Field(typeof(BonusEntity), "m_collider");
        static readonly FieldInfo fRoad = AccessTools.Field(typeof(DiamondBonusEntity), "m_layerRoad");
        static readonly FieldInfo fWall = AccessTools.Field(typeof(DiamondBonusEntity), "m_layerWall");
        static readonly FieldInfo fExplodeSnd = AccessTools.Field(typeof(DiamondBonusEntity), "m_soundExplode");
        static readonly FieldInfo fCountdown = AccessTools.Field(typeof(DiamondBonusEntity), "m_soundCountdown");
        static readonly FieldInfo fIdle = AccessTools.Field(typeof(DiamondBonusEntity), "m_particleIdleEffect");
        static readonly FieldInfo fExplGO = AccessTools.Field(typeof(DiamondBonusEntity), "m_explosionAnimationGO");
        static readonly MethodInfo mNetExplode = AccessTools.Method(typeof(DiamondBonusEntity), "NetExplode");
        int mascara;

        public void Preparar(DiamondBonusEntity e)
        {
            ent = e;
            if (visual != null) { EsconderDiamante(); return; }
            EsconderDiamante();
            var go = new GameObject("GKOvo_Visual");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Ovo.MalhaOvo();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Ovo.MatCasca();
            mr.shadowCastingMode = ShadowCastingMode.On;
            visual = go.transform;
            visual.gameObject.SetActive(false);
            mascara = ((LayerMask)fRoad.GetValue(e)).value | ((LayerMask)fWall.GetValue(e)).value;
            if (Ovo.Raio <= 0f) Ovo.Raio = MedirRaioDomo(e);
        }

        public void Reesconder() { EsconderDiamante(); }

        void EsconderDiamante()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (visual == null || r.transform != visual) r.enabled = false;
            var idle = fIdle.GetValue(ent) as ParticleSystem;
            if (idle != null) idle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // raio da esfera de explosão do diamante, medido amostrando a animação do domo
        static float MedirRaioDomo(DiamondBonusEntity e)
        {
            float r = 0f;
            try
            {
                var go = fExplGO.GetValue(e) as GameObject;
                var sc = go != null ? go.GetComponent<SphereCollider>() : null;
                var anim = go != null ? go.GetComponent<Animation>() : null;
                if (sc != null)
                {
                    Func<float> medir = () => { var ls = sc.transform.lossyScale; return sc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))); };
                    r = medir();
                    var escalas = new Dictionary<Transform, Vector3>();
                    foreach (var tr in go.GetComponentsInChildren<Transform>(true)) escalas[tr] = tr.localScale;
                    escalas[go.transform] = go.transform.localScale;
                    if (anim != null && anim.clip != null)
                    {
                        var st = anim[anim.clip.name];
                        st.enabled = true; st.weight = 1f;
                        for (int i = 0; i <= 20; i++) { st.time = st.length * i / 20f; anim.Sample(); r = Mathf.Max(r, medir()); }
                        st.time = 0f; anim.Sample(); st.enabled = false; anim.Stop();
                        Ovo.Log.LogInfo("[ovo] domo: clip " + anim.clip.name + " " + st.length.ToString("0.00") + " s");
                    }
                    foreach (var kv in escalas) kv.Key.localScale = kv.Value;
                    Ovo.Log.LogInfo("[ovo] domo: raio do colisor " + sc.radius + " → raio máximo " + r.ToString("0.00") + " m");
                }
                else Ovo.Log.LogWarning("[ovo] domo sem SphereCollider: " + (go != null ? go.name : "nulo"));
            }
            catch (Exception ex) { Ovo.Log.LogWarning("[ovo] não consegui medir o domo: " + ex.Message); }
            if (r < 2f || r > 15f) { Ovo.Log.LogWarning("[ovo] raio estranho (" + r + "), usando 5 m"); r = 5f; }
            return r;
        }

        public void Lancar(bool behind)
        {
            EsconderDiamante();
            var cd = fCountdown.GetValue(ent) as AudioSource; if (cd != null) cd.Stop();
            voando = true; estourou = false; fim = false; t = 0f; atingido = null;
            lancador = ent.Launcher;
            paraCima = behind;
            karts = Ovo.Karts();
            Vector3 vk = Vector3.zero, fwd = transform.forward;
            if (lancador != null)
            {
                try { vk = lancador.GetVehiclePhysic().GetLinearVelocity(); } catch (Exception) { }
                fwd = lancador.LaunchHorizontalDirection;
            }
            fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
            if (paraCima)
                vel = Vector3.up * 14f + fwd * 1.0f;                 // sobe ~5 m e cai onde foi jogado
            else
                vel = new Vector3(vk.x, Mathf.Max(0f, vk.y), vk.z) + fwd * 22f + Vector3.up * 8f;
            if (Ovo.MiraCinema && !paraCima && lancador != null)
            {
                Kart alvo = null; float md = 1e9f;
                foreach (var k in karts)
                {
                    if (k == null || k == lancador) continue;
                    Vector3 d = k.Transform.position - transform.position;
                    float fr = Vector3.Dot(d, fwd);
                    if (fr < 6f || fr > 80f || Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, fwd))) > fr * 0.6f) continue;
                    if (d.magnitude < md) { md = d.magnitude; alvo = k; }
                }
                if (alvo != null)
                {
                    Vector3 va = Vector3.zero;
                    try { va = alvo.GetVehiclePhysic().GetLinearVelocity(); } catch (Exception) { }
                    float T = Mathf.Clamp(md / 30f, 0.55f, 1.3f);
                    Vector3 prev = alvo.Transform.position + alvo.Transform.up * 0.6f + va * T;
                    vel = (prev - transform.position) / T - 0.5f * (Vector3.up * -20f) * T;
                    Ovo.UltimoAlvo = alvo;
                    Ovo.Log.LogInfo("[ovo] mira no kart " + alvo.name + " a " + md.ToString("0") + " m (voo " + T.ToString("0.00") + " s)");
                }
            }
            visual.gameObject.SetActive(true);
            visual.localPosition = Vector3.zero;
            visual.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(90f, 0, 0);
            eixoGiro = Vector3.Cross(Vector3.up, fwd).normalized; giro = paraCima ? 420f : 720f;
        }

        void FixedUpdate()
        {
            if (!voando || estourou || ent == null) return;
            if (GkNetMgr.Instance != null && !GkNetMgr.Instance.IsGameObjectMine(gameObject)) return;
            float dt = Time.fixedDeltaTime;
            t += dt;
            Vector3 p0 = transform.position;
            vel += Vector3.up * -20f * dt;
            Vector3 passo = vel * dt;
            RaycastHit hit;
            if (Physics.Raycast(p0, passo.normalized, out hit, passo.magnitude + 0.25f, mascara, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * 0.1f;
                Estourar();
                return;
            }
            Vector3 p1 = p0 + passo;
            transform.position = p1;
            foreach (var k in karts)
            {
                if (k == null) continue;
                if (k == lancador && (t < 0.45f || (paraCima && vel.y > 0f))) continue;
                Vector3 c = k.Transform.position + k.Transform.up * 0.6f;
                if ((c - p1).sqrMagnitude < 1.6f * 1.6f)
                {
                    atingido = k;
                    transform.position = p1;
                    Estourar();
                    return;
                }
            }
            if (t > 6f || p1.y < -400f) Estourar();
        }

        void Update()
        {
            if (voando && visual != null) visual.Rotate(eixoGiro, giro * Time.deltaTime, Space.World);
        }

        // dono do ovo decide o estouro e avisa todos (RpcExplode → Explode → EstouroLocal)
        void Estourar()
        {
            voando = false;
            if (atingido != null && Ovo.Controlo(atingido)) Ovo.Escorregar(atingido, lancador);
            try { mNetExplode.Invoke(ent, new object[] { false }); }
            catch (Exception ex) { Ovo.Log.LogWarning("[ovo] NetExplode: " + ex.Message); EstouroLocal(); }
        }

        public void EstouroLocal()
        {
            if (estourou) return;
            estourou = true; voando = false;
            var col = fCollider.GetValue(ent) as Collider; if (col != null) col.enabled = false;
            if (visual != null) visual.gameObject.SetActive(false);
            Vector3 p = transform.position;
            Vector3 chao = p; Vector3 normal = Vector3.up;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 40f, ((LayerMask)fRoad.GetValue(ent)).value, QueryTriggerInteraction.Ignore))
            { chao = hit.point; normal = hit.normal; }
            var lan = lancador != null ? lancador : ent.Launcher;
            OvoMancha.Criar(chao, Ovo.Raio, ((LayerMask)fRoad.GetValue(ent)).value, lan, atingido);
            Ovo.UltimaMancha = chao; Ovo.QuadroUltimoEstouro = Time.frameCount;
            OvoEstilhaco.Criar(p, chao);
            TocarEstouro(p);
            Ovo.Log.LogInfo("[ovo] (frame " + Time.frameCount + ") estourou em " + p.ToString("F1") + (atingido != null ? " acertando " + atingido.name : "") + (paraCima ? " (jogado para cima)" : ""));
            fim = true;
        }

        void TocarEstouro(Vector3 p)
        {
            var orig = fExplodeSnd.GetValue(ent) as AudioSource;
            var go = new GameObject("GKOvo_Som");
            go.transform.position = p;
            var a = go.AddComponent<AudioSource>();
            if (orig != null)
            {
                a.outputAudioMixerGroup = orig.outputAudioMixerGroup;
                a.spatialBlend = orig.spatialBlend; a.minDistance = orig.minDistance; a.maxDistance = orig.maxDistance;
                a.rolloffMode = orig.rolloffMode; a.volume = orig.volume; a.dopplerLevel = 0f;
                if (orig.rolloffMode == AudioRolloffMode.Custom) a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, orig.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            }
            else { a.spatialBlend = 1f; a.minDistance = 8f; a.maxDistance = 120f; }
            a.clip = Ovo.SomEstouro();
            a.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            a.Play();
            Destroy(go, 1.5f);
        }

        public void Encerrar() { voando = false; if (visual != null) visual.gameObject.SetActive(false); }
    }

    // ---------------- mancha de ovo no chão ----------------
    public class OvoMancha : MonoBehaviour
    {
        float raio, t, extra;
        Kart lancador;
        Material mat; bool lit; Color corBase;
        List<Kart> karts;
        Dictionary<int, bool> dentroAntes = new Dictionary<int, bool>();
        public static List<OvoMancha> Ativas = new List<OvoMancha>();

        public static void Criar(Vector3 centro, float raio, int mascaraChao, Kart lancador, Kart jaAtingido)
        {
            var go = new GameObject("GKOvo_Mancha");
            go.transform.position = centro;
            var m = go.AddComponent<OvoMancha>();
            m.raio = raio; m.lancador = lancador; m.extra = Ovo.DuracaoExtra;
            m.karts = Ovo.Karts();
            if (jaAtingido != null) m.dentroAntes[jaAtingido.GetInstanceID()] = true;
            go.AddComponent<MeshFilter>().sharedMesh = MalhaNoChao(centro, raio, mascaraChao);
            var mr = go.AddComponent<MeshRenderer>();
            m.mat = Ovo.NovaMatMancha(out m.lit);
            m.corBase = m.lit ? Color.white : Ovo.LuzDaCena();
            m.mat.color = m.corBase;
            mr.sharedMaterial = m.mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.transform.localScale = new Vector3(0.25f, 1f, 0.25f);
            Ativas.Add(m);
            // quem já está dentro (com as rodas no chão) é atingido pelo estouro
            m.Verificar();
        }

        // disco que acompanha o relevo (raios para baixo), um pouco acima do chão
        static Mesh MalhaNoChao(Vector3 c, float raio, int mascara)
        {
            int n = 30;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float ang = UnityEngine.Random.Range(0f, 360f);
            var rot = Quaternion.Euler(0, ang, 0);
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float u = (float)i / n, w = (float)j / n;
                    Vector3 off = rot * new Vector3((u - 0.5f) * 2f * raio, 0, (w - 0.5f) * 2f * raio);
                    Vector3 p = c + off;
                    RaycastHit hit;
                    float y = 0f;
                    if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out hit, 7f, mascara, QueryTriggerInteraction.Ignore)) y = hit.point.y - c.y;
                    v.Add(new Vector3(off.x, y + 0.045f, off.z));
                    uv.Add(new Vector2(u, w));
                }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + n + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1); tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            if (m.normals[0].y < 0) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            t += Time.deltaTime;
            // espalha rápido (0,22 s) com leve "quique"
            float s;
            if (t < 0.22f) { float g = t / 0.22f; s = 0.25f + 0.85f * (1f - Mathf.Pow(1f - g, 3f)); }
            else s = 1f + 0.10f * Mathf.Exp(-(t - 0.22f) * 10f) * Mathf.Cos((t - 0.22f) * 25f);
            transform.localScale = new Vector3(s, 1f, s);
            float tv = t - extra;
            float a = tv > DURACAO_FADE_INI ? Mathf.Clamp01((Ovo.DURACAO - tv) / Ovo.FADE) : 1f;
            var c = corBase; c.a = a; mat.color = c;
            if (mat.HasProperty("_ColorTeint")) mat.SetColor("_ColorTeint", c);
            if (tv >= Ovo.DURACAO) { Ativas.Remove(this); Destroy(mat); Destroy(gameObject); return; }
            Verificar();
        }

        static float DURACAO_FADE_INI { get { return Ovo.DURACAO - Ovo.FADE; } }

        // rodar só ao entrar (com as rodas no chão)
        void Verificar()
        {
            if (t - extra > Ovo.DURACAO - 0.4f) return;          // quase sumindo: já não escorrega
            foreach (var k in karts)
            {
                if (k == null || k.Transform == null) continue;
                Ovo.AtualizarImunidade(k);
                if (!Ovo.Controlo(k)) continue;
                Vector3 d = k.Transform.position - transform.position;
                float r = raio * 0.92f;
                bool dentro = Mathf.Abs(d.y) < 3f && (d.x * d.x + d.z * d.z) < r * r && k.IsOnGround();
                int id = k.GetInstanceID();
                bool antes;
                dentroAntes.TryGetValue(id, out antes);
                if (dentro && !antes) Ovo.Escorregar(k, lancador);
                dentroAntes[id] = dentro;
            }
        }
    }

    // ---------------- cascas e respingos voando ----------------
    public class OvoEstilhaco : MonoBehaviour
    {
        class Peca { public Transform tr; public Vector3 v; public Vector3 w; public bool gota; public float esc; public bool parou; }
        List<Peca> pecas = new List<Peca>();
        float t, chaoY;
        Material matGota, matGema;
        static Mesh malhaCasca, malhaQuad;

        public static void Criar(Vector3 p, Vector3 chao)
        {
            var go = new GameObject("GKOvo_Estilhaco");
            go.transform.position = p;
            var e = go.AddComponent<OvoEstilhaco>();
            e.chaoY = chao.y + 0.06f;
            Color luz = Ovo.LuzDaCena();
            e.matGota = Ovo.NovaMatGota(); e.matGota.color = new Color(1f, 0.98f, 0.92f, 0.85f) * luz;
            e.matGema = Ovo.NovaMatGota(); e.matGema.color = new Color(1f, 0.72f, 0.12f, 1f) * new Color(Mathf.Max(luz.r, 0.7f), Mathf.Max(luz.g, 0.6f), Mathf.Max(luz.b, 0.5f), 1f);
            var rnd = new System.Random();
            Func<float, float, float> R = (a, b) => (float)(a + (b - a) * rnd.NextDouble());
            // cascas
            for (int i = 0; i < 12; i++)
            {
                var c = new GameObject("casca");
                c.transform.SetParent(go.transform, false);
                c.AddComponent<MeshFilter>().sharedMesh = MalhaCasca();
                var mr = c.AddComponent<MeshRenderer>(); mr.sharedMaterial = Ovo.MatCasca(); mr.shadowCastingMode = ShadowCastingMode.Off;
                float a = R(0, 6.283f);
                float esc = R(0.5f, 1.1f);
                c.transform.localScale = Vector3.one * esc;
                c.transform.rotation = UnityEngine.Random.rotation;
                e.pecas.Add(new Peca { tr = c.transform, v = new Vector3(Mathf.Cos(a) * R(2f, 6f), R(4f, 8f), Mathf.Sin(a) * R(2f, 6f)), w = UnityEngine.Random.onUnitSphere * R(300f, 900f), esc = esc });
            }
            // gotas de clara e gema
            for (int i = 0; i < 34; i++)
            {
                var g = new GameObject("gota");
                g.transform.SetParent(go.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = MalhaQuad();
                var mr = g.AddComponent<MeshRenderer>();
                bool gema = i % 3 == 0;
                mr.sharedMaterial = gema ? e.matGema : e.matGota; mr.shadowCastingMode = ShadowCastingMode.Off;
                float a = R(0, 6.283f), esc = R(0.12f, gema ? 0.42f : 0.34f);
                g.transform.localScale = Vector3.one * esc;
                e.pecas.Add(new Peca { tr = g.transform, v = new Vector3(Mathf.Cos(a) * R(3f, 9f), R(3f, 9f), Mathf.Sin(a) * R(3f, 9f)), gota = true, esc = esc });
            }
            Destroy(go, 2.2f);
        }

        static Mesh MalhaCasca()
        {
            if (malhaCasca != null) return malhaCasca;
            // pedaço curvo de casca (duas faces)
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            v.Add(new Vector3(0, 0.02f, 0)); uv.Add(new Vector2(0.5f, 0.5f));
            int n = 6;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n, r = 0.07f + 0.03f * Mathf.Sin(i * 2.7f);
                v.Add(new Vector3(Mathf.Cos(a) * r, -0.015f, Mathf.Sin(a) * r)); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.3f, 0.5f + Mathf.Sin(a) * 0.3f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n;
                tri.Add(0); tri.Add(b); tri.Add(a);
                tri.Add(0); tri.Add(a); tri.Add(b);
            }
            malhaCasca = new Mesh(); malhaCasca.SetVertices(v); malhaCasca.SetUVs(0, uv); malhaCasca.SetTriangles(tri, 0); malhaCasca.RecalculateNormals();
            return malhaCasca;
        }

        static Mesh MalhaQuad()
        {
            if (malhaQuad != null) return malhaQuad;
            malhaQuad = new Mesh();
            malhaQuad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) });
            malhaQuad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            malhaQuad.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);
            malhaQuad.RecalculateBounds();
            return malhaQuad;
        }

        void Update()
        {
            float dt = Time.deltaTime; t += dt;
            var cam = Camera.main;
            float fade = Mathf.Clamp01((2.2f - t) / 0.6f);
            foreach (var p in pecas)
            {
                if (!p.parou)
                {
                    p.v += Vector3.up * -22f * dt;
                    p.tr.position += p.v * dt;
                    if (!p.gota) p.tr.Rotate(p.w * dt, Space.World);
                    if (p.tr.position.y < chaoY)
                    {
                        var pos = p.tr.position; pos.y = chaoY; p.tr.position = pos;
                        if (p.gota) { p.parou = true; p.tr.localScale = new Vector3(p.esc * 1.3f, p.esc * 1.3f, p.esc); p.tr.rotation = Quaternion.Euler(90, 0, 0); }
                        else if (p.v.y < -2f) { p.v = new Vector3(p.v.x * 0.4f, -p.v.y * 0.3f, p.v.z * 0.4f); p.w *= 0.5f; }
                        else { p.parou = true; }
                    }
                }
                if (p.gota && !p.parou && cam != null) p.tr.rotation = cam.transform.rotation;
                if (p.gota) p.tr.localScale = p.parou ? p.tr.localScale : Vector3.one * p.esc * (0.7f + 0.3f * fade);
            }
            var c1 = matGota.color; c1.a = 0.85f * fade; matGota.color = c1;
            var c2 = matGema.color; c2.a = fade; matGema.color = c2;
        }

        void OnDestroy() { if (matGota != null) Destroy(matGota); if (matGema != null) Destroy(matGema); }
    }

    // ---------------- patches no diamante ----------------
    [HarmonyPatch(typeof(DiamondBonusEntity), "Awake")]
    static class Patch_OvoAwake
    {
        static void Postfix(DiamondBonusEntity __instance)
        {
            var o = __instance.gameObject.GetComponent<OvoItem>();
            if (o == null) { o = __instance.gameObject.AddComponent<OvoItem>(); o.Preparar(__instance); }
            else o.Reesconder();
        }
    }

    [HarmonyPatch(typeof(DiamondBonusEntity), "Launch")]
    static class Patch_OvoLaunch
    {
        static void Postfix(DiamondBonusEntity __instance, bool behind)
        {
            var o = __instance.GetComponent<OvoItem>();
            if (o == null) { o = __instance.gameObject.AddComponent<OvoItem>(); o.Preparar(__instance); }
            o.Lancar(behind);
        }
    }

    [HarmonyPatch(typeof(DiamondBonusEntity), "FixedUpdate")]
    static class Patch_OvoFixed { static bool Prefix() { return false; } }

    [HarmonyPatch(typeof(DiamondBonusEntity), "Update")]
    static class Patch_OvoUpdate
    {
        static readonly FieldInfo fEstado = AccessTools.Field(typeof(DiamondBonusEntity), "m_diamondState");
        static bool Prefix(DiamondBonusEntity __instance)
        {
            var o = __instance.GetComponent<OvoItem>();
            if (o == null) return true;
            if (o.fim)
            {
                o.fim = false;
                fEstado.SetValue(__instance, DiamondBonusEntity.E_STATE.END);
                return true;       // o original faz DetachFromTarget + NetDestroy (volta para o pool)
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(DiamondBonusEntity), "Explode")]
    static class Patch_OvoExplode
    {
        static bool Prefix(DiamondBonusEntity __instance)
        {
            var o = __instance.GetComponent<OvoItem>();
            if (o == null) return true;
            o.EstouroLocal();
            return false;
        }
    }

    [HarmonyPatch(typeof(DiamondBonusEntity), "OnTriggerEnter")]
    static class Patch_OvoTrigger { static bool Prefix() { return false; } }

    [HarmonyPatch(typeof(DiamondBonusEntity), "DoOnTriggerEnter")]
    static class Patch_OvoDoTrigger { static bool Prefix() { return false; } }

    [HarmonyPatch(typeof(DiamondBonusEntity), "CheckAttachToTarget")]
    static class Patch_OvoNaoGruda { static bool Prefix() { return false; } }

    // ícone do item no HUD (roleta e slot)
    [HarmonyPatch(typeof(HUDBonusSlotHD), "Awake")]
    static class Patch_OvoHud
    {
        static void Postfix(HUDBonusSlotHD __instance)
        {
            var lista = Traverse.Create(__instance).Field("m_sprites").GetValue<List<Sprite>>();
            int i = (int)BonusCategory.DIAMOND - 1;
            if (lista != null && lista.Count > i && Ovo.SpItem != null) lista[i] = Ovo.SpItem;
        }
    }

    // ---------------- teste automático (só com o arquivo BepInEx/gkmod_ovo_teste) ----------------
    // todo item vira ovo; o jogador humano (piloto automático do GKTeste) ganha um ovo a cada 5 s e joga,
    // alternando para a frente / para cima
    [HarmonyPatch(typeof(KartBonusMgr), "SetItem")]
    static class Patch_OvoTesteItem
    {
        static void Prefix(KartBonusMgr __instance, ref BonusCategory bonus)
        {
            if (bonus == BonusCategory.NONE) return;
            if (Ovo.Teste) bonus = BonusCategory.DIAMOND;
            else if (Ovo.SoBots)
            {
                var k = __instance.GetComponentInParent<Kart>();
                if (k == null) k = Traverse.Create(__instance).Field("m_kart").GetValue<Kart>();
                if (k != null && k.GetControlType() != RcVehicle.ControlType.Human) bonus = BonusCategory.DIAMOND;
            }
        }
    }

    static class TesteAutomatico
    {
        static bool vitrineFeita; static float proxEstilhaco; static Vector3 posOvo;
        // vitrine para foto: mancha + ovo parado no ar + estilhaços a cada 2 s, na reta da largada do Monte Lasanha
        public static void Vitrine()
        {
            if (UnityEngine.Object.FindObjectOfType<Kart>() == null) { vitrineFeita = false; return; }
            if (!vitrineFeita)
            {
                RaycastHit hit;
                Vector3 c = new Vector3(-187f, 60f, 62f);
                if (!Physics.Raycast(c, Vector3.down, out hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
                vitrineFeita = true;
                Ovo.DuracaoExtra = 1e6f;
                OvoMancha.Criar(hit.point, Ovo.Raio > 0 ? Ovo.Raio : 6.58f, Physics.DefaultRaycastLayers, null, null);
                Ovo.DuracaoExtra = 0f;
                posOvo = hit.point + new Vector3(4.5f, 1.4f, -9f);
                var go = new GameObject("GKOvo_VitrineOvo");
                go.AddComponent<MeshFilter>().sharedMesh = Ovo.MalhaOvo();
                go.AddComponent<MeshRenderer>().sharedMaterial = Ovo.MatCasca();
                go.transform.position = posOvo; go.transform.rotation = Quaternion.Euler(20f, 30f, 25f);
                Ovo.Log.LogInfo("[ovo-vitrine] mancha em " + hit.point.ToString("F1") + " (collider " + hit.collider.name + ")");
            }
            if (Time.time > proxEstilhaco)
            {
                proxEstilhaco = Time.time + 2f;
                OvoEstilhaco.Criar(posOvo + new Vector3(-3f, 0.3f, 4f), posOvo + new Vector3(-3f, -1.4f, 4f));
            }
        }

        static float prox = 12f; static int n; static float tUsar = -1f;
        public static void Passo()
        {
            var gm = Singleton<GameManager>.Instance;
            if (gm == null || gm.GameMode == null) return;
            Kart humano = null;
            foreach (var k in UnityEngine.Object.FindObjectsOfType<Kart>())
                if (k.GetControlType() == RcVehicle.ControlType.Human) { humano = k; break; }
            if (humano == null || !humano.GetState(RcVehicle.eVehicleState.S_IS_RUNNING)) { prox = Time.time + 6f; return; }
            if (tUsar < 0f && Time.time > prox)
            {
                humano.GetBonusMgr().SetItem(BonusCategory.DIAMOND, 1, -1, true);
                tUsar = Time.time + 2.5f;
            }
            if (tUsar > 0f && Time.time > tUsar)
            {
                bool tras = (n % 2) == 1;
                humano.GetBonusMgr().ActivateBonus(tras);
                Ovo.Log.LogInfo("[ovo-teste] jogador usou ovo " + (tras ? "para cima" : "para a frente") + " em " + humano.Transform.position.ToString("F0"));
                n++; tUsar = -1f; prox = Time.time + 5f;
            }
        }
    }
}
