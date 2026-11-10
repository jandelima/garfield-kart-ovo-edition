// Ajudante de teste (só funciona se existir BepInEx/gkmod_teste).
// - Cria BepInEx/gkmod_snap -> salva um print gkmod_snapN.png
// - Registra a velocidade dos karts a cada segundo
// - Se existir BepInEx/gkmod_autopilot, o kart do jogador é dirigido pela IA
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using BepInEx;
using UnityEngine;

namespace GKMod
{
    [BepInPlugin("gkmod.teste", "GK Teste", "1.0.0")]
    public class Teste : BaseUnityPlugin
    {
        private bool ligado;
        private int n;
        private float proximo;
        private float maxHumano, maxBots;
        private Kart jogador;
        private bool dumped;
        private int tentativas;

        private void TentarDump()
        {
            try
            {
                var icons = UISprites.DifficultyIcons;
                if (icons == null) return;
                for (int i = 0; i < icons.Length; i++)
                {
                    var sp = icons[i];
                    if (sp == null) continue;
                    Rect r = sp.textureRect;
                    var tex = sp.texture;
                    var rtex = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, rtex);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rtex;
                    var t = new Texture2D((int)r.width, (int)r.height, TextureFormat.ARGB32, false);
                    t.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                    t.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rtex);
                    string f = Path.Combine(Paths.BepInExRootPath, "gkmod_icon" + i + ".png");
                    File.WriteAllBytes(f, t.EncodeToPNG());
                    Logger.LogInfo("icone " + i + " '" + sp.name + "' " + r + " pivot=" + sp.pivot + " ppu=" + sp.pixelsPerUnit + " -> " + f);
                }
                dumped = true;
            }
            catch (System.Exception e) { tentativas++; Logger.LogWarning("dump falhou: " + e.Message); if (tentativas > 40) dumped = true; }
        }

        public static BepInEx.Logging.ManualLogSource L;
        public static void LogS(string s) { if (L != null) L.LogInfo(s); }

        private void Awake()
        {
            L = Logger;
            ligado = File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_teste"));
            if (ligado)
            {
                Cinema.Garantir(gameObject);
                Logger.LogInfo("Ajudante de teste LIGADO.");
                Application.runInBackground = true;
                new HarmonyLib.Harmony("gkmod.teste").Patch(HarmonyLib.AccessTools.Method(typeof(HUDPause), "OnApplicationPause"),
                    new HarmonyLib.HarmonyMethod(typeof(Teste), "SemPausa"));
                AudioListener.volume = 0f;
                EsconderJanela();
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, mode) =>
                {
                    Logger.LogInfo("cena carregada: " + sc.name + " (" + mode + ")");
                    if (File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_dumpscene"))) DumpCena(sc);
                };
            }
        }

        static bool SemPausa() { return false; } // no teste a janela nunca tem foco: não pausar sozinho

        // ---------- janela fora da tela, sem foco ----------
        delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, System.IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(System.IntPtr h);
        [DllImport("user32.dll")] static extern bool SetWindowPos(System.IntPtr h, System.IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(System.IntPtr h, int idx);
        [DllImport("user32.dll")] static extern int SetWindowLong(System.IntPtr h, int idx, int val);
        static System.IntPtr janela;

        static void EsconderJanela()
        {
            try
            {
                uint meu = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                EnumWindows((h, l) =>
                {
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    if (pid == meu && IsWindowVisible(h)) { janela = h; return false; }
                    return true;
                }, System.IntPtr.Zero);
                if (janela == System.IntPtr.Zero) return;
                const int GWL_EXSTYLE = -20; const int WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;
                int ex = GetWindowLong(janela, GWL_EXSTYLE);
                SetWindowLong(janela, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);
                // HWND_BOTTOM, SWP_NOSIZE|SWP_NOACTIVATE|SWP_FRAMECHANGED
                SetWindowPos(janela, new System.IntPtr(1), -6000, -6000, 0, 0, 0x0001 | 0x0010 | 0x0020);
            }
            catch (System.Exception) { }
        }

        // ---------- comandos (arquivo BepInEx/gkmod_cmd) ----------
        private bool rodandoCmd;

        private void ChecarComandos()
        {
            if (rodandoCmd) return;
            string f = Path.Combine(Paths.BepInExRootPath, "gkmod_cmd");
            if (!File.Exists(f)) return;
            string[] linhas;
            try { linhas = File.ReadAllLines(f); File.Delete(f); } catch { return; }
            StartCoroutine(Rodar(linhas));
        }

        private IEnumerator Rodar(string[] linhas)
        {
            rodandoCmd = true;
            foreach (var l0 in linhas)
            {
                string l = l0.Trim();
                if (l.Length == 0) continue;
                string[] p = l.Split(' ');
                Logger.LogInfo("cmd: " + l);
                var es = EventSystem.current;
                var mod = es != null ? es.GetComponent<GkInputModule>() : null;
                switch (p[0])
                {
                    case "espera": yield return new WaitForSecondsRealtime(float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)); break;
                    case "ok": if (mod != null && mod.SubmitAction != null) mod.SubmitAction(0, new BaseEventData(es)); break;
                    case "voltar": if (mod != null && mod.CancelAction != null) mod.CancelAction(0, new BaseEventData(es)); break;
                    case "esq": case "dir": case "cima": case "baixo":
                    case "aba+": case "aba-":
                        if (mod != null)
                        {
                            var ev = new AxisEventData(es);
                            ev.moveDir = p[0] == "esq" || p[0] == "aba-" ? MoveDirection.Left : p[0] == "dir" || p[0] == "aba+" ? MoveDirection.Right : p[0] == "cima" ? MoveDirection.Up : MoveDirection.Down;
                            if (p[0].StartsWith("aba")) { if (mod.SecondaryMove != null) mod.SecondaryMove(0, ev); }
                            else if (mod.PrimaryMove != null) mod.PrimaryMove(0, ev);
                        }
                        break;
                    case "foto":
                    case "fotolimpa":
                        {
                            var ov = GameObject.Find("GKMod_Canvas");
                            if (p[0] == "fotolimpa" && ov != null) { ov.SetActive(false); yield return null; }
                            string arq = Path.Combine(Paths.BepInExRootPath, "gkmod_foto_" + p[1] + ".png");
                            ScreenCapture.CaptureScreenshot(arq);
                            yield return new WaitForSecondsRealtime(0.8f);
                            Logger.LogInfo("foto salva " + arq);
                            if (ov != null) ov.SetActive(true);
                        }
                        break;
                    case "auto": if (p[1] == "on") File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "gkmod_autodrive"), ""); else File.Delete(Path.Combine(Paths.BepInExRootPath, "gkmod_autodrive")); break;
                    case "ok_ate":
                        {
                            var tipo = HarmonyLib.AccessTools.TypeByName(p[1]);
                            float limite = Time.realtimeSinceStartup + 40f;
                            bool achou = false;
                            while (Time.realtimeSinceStartup < limite)
                            {
                                var o = tipo != null ? Object.FindObjectOfType(tipo) as Behaviour : null;
                                if (o != null && o.isActiveAndEnabled) { achou = true; break; }
                                var es2 = EventSystem.current; var mod2 = es2 != null ? es2.GetComponent<GkInputModule>() : null;
                                if (mod2 != null && mod2.SubmitAction != null) mod2.SubmitAction(0, new BaseEventData(es2));
                                yield return new WaitForSecondsRealtime(2.5f);
                            }
                            Logger.LogInfo("ok_ate " + p[1] + " -> " + (achou ? "ok" : "TEMPO ESGOTADO"));
                            yield return new WaitForSecondsRealtime(1.2f);
                        }
                        break;
                    case "aguarda":
                        {
                            var tipo = HarmonyLib.AccessTools.TypeByName(p[1]);
                            float limite = Time.realtimeSinceStartup + 40f;
                            bool achou = false;
                            while (Time.realtimeSinceStartup < limite)
                            {
                                Object o = tipo != null ? Object.FindObjectOfType(tipo) : GameObject.Find(p[1]);
                                var mb = o as Behaviour;
                                if (o != null && (mb == null || mb.isActiveAndEnabled)) { achou = true; break; }
                                yield return new WaitForSecondsRealtime(0.25f);
                            }
                            Logger.LogInfo("aguarda " + p[1] + " -> " + (achou ? "ok" : "TEMPO ESGOTADO"));
                            yield return new WaitForSecondsRealtime(1.2f);
                        }
                        break;
                    case "vista":
                        {
                            // vista px py pz alvox alvoy alvoz nome [fov]
                            var ci = System.Globalization.CultureInfo.InvariantCulture;
                            var pos = new Vector3(float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci));
                            var alvo = new Vector3(float.Parse(p[4], ci), float.Parse(p[5], ci), float.Parse(p[6], ci));
                            var go = new GameObject("GKTeste_Camera");
                            var cam = go.AddComponent<Camera>();
                            var principal = Camera.main;
                            if (principal != null) { cam.CopyFrom(principal); }
                            cam.enabled = false;
                            cam.fieldOfView = p.Length > 8 ? float.Parse(p[8], ci) : 50f;
                            cam.farClipPlane = 5000f;
                            go.transform.position = pos; go.transform.LookAt(alvo);
                            var rt = new RenderTexture(1280, 720, 24);
                            cam.targetTexture = rt; cam.Render();
                            RenderTexture.active = rt;
                            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                            RenderTexture.active = null; cam.targetTexture = null;
                            File.WriteAllBytes(Path.Combine(Paths.BepInExRootPath, "gkmod_foto_" + p[7] + ".png"), tex.EncodeToPNG());
                            Object.Destroy(rt); Object.Destroy(go);
                            Logger.LogInfo("vista salva " + p[7]);
                        }
                        break;
                    case "quem":
                        {
                            var ci = System.Globalization.CultureInfo.InvariantCulture;
                            var c = new Vector3(float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci));
                            float r = float.Parse(p[4], ci);
                            foreach (var rd in Object.FindObjectsOfType<Renderer>())
                            {
                                if (!rd.enabled || !rd.gameObject.activeInHierarchy) continue;
                                if (rd.bounds.SqrDistance(c) > r * r) continue;
                                var t = rd.transform; string cam = t.name;
                                while (t.parent != null) { t = t.parent; cam = t.name + "/" + cam; }
                                Logger.LogInfo("quem: " + rd.gameObject.scene.name + " :: " + cam + " layer=" + LayerMask.LayerToName(rd.gameObject.layer) +
                                    " mat=" + (rd.sharedMaterial != null ? rd.sharedMaterial.name + "/" + rd.sharedMaterial.shader.name : "-") + " bounds=" + rd.bounds.center.ToString("F0"));
                            }
                        }
                        break;
                    case "material":
                        {
                            foreach (var rd in Object.FindObjectsOfType<Renderer>())
                                if (rd.name == p[1] && rd.sharedMaterial != null)
                                {
                                    var m = rd.sharedMaterial;
                                    string props = "";
                                    foreach (var n in new[] { "_MainTex", "_Color", "_Splat0", "_Splat1", "_Splat2", "_Control", "_BaseMap", "_Tex1", "_Tex2", "_Grass", "_Dirt" })
                                        if (m.HasProperty(n)) props += " " + n;
                                    Logger.LogInfo("material " + p[1] + ": " + m.name + " shader=" + m.shader.name + " props:" + props + " kw=" + string.Join(",", m.shaderKeywords));
                                    break;
                                }
                        }
                        break;
                    case "cc":
                        {
                            // 0..2 = abas do jogo, 3 = aba 200cc do mod
                            int n = int.Parse(p[1]);
                            var gm = Object.FindObjectOfType<MenuHDGameMode>();
                            UnityEngine.UI.Toggle alvo = null;
                            if (n == 3) { var g = GameObject.Find("Tab_200cc"); if (g != null) alvo = g.GetComponent<UnityEngine.UI.Toggle>(); }
                            else if (gm != null)
                            {
                                var arr = HarmonyLib.AccessTools.Field(typeof(MenuHDGameMode), "m_tabs").GetValue(gm);
                                var raw = (System.Array)arr.GetType().GetProperty("RawArray").GetValue(arr, null);
                                alvo = (UnityEngine.UI.Toggle)raw.GetValue(n);
                            }
                            if (alvo != null) GkEventSystem.Current.SelectTab(alvo);
                            Logger.LogInfo("cc -> " + (alvo != null ? alvo.name : "não achei"));
                        }
                        break;
                    case "corrida":
                        { var gm = Object.FindObjectOfType<MenuHDGameMode>(); if (gm != null) gm.OnSubmitSingleRace(); else Logger.LogWarning("menu de modo não está aberto"); }
                        break;
                    case "card":
                        {
                            UnityEngine.UI.Button b = null;
                            if (p[1] == "nova") { var g = GameObject.Find("Card_PistaNova"); if (g != null) b = g.GetComponent<UnityEngine.UI.Button>(); }
                            else
                            {
                                var ts = Object.FindObjectOfType<MenuHDTrackSelection>();
                                if (ts != null)
                                {
                                    var arr = HarmonyLib.AccessTools.Field(typeof(MenuHDTrackSelection), "m_buttons").GetValue(ts);
                                    var raw = (System.Array)arr.GetType().GetProperty("RawArray").GetValue(arr, null);
                                    b = (UnityEngine.UI.Button)raw.GetValue(int.Parse(p[1]));
                                }
                            }
                            if (b != null) { GkEventSystem.Current.SelectButton(b); b.onClick.Invoke(); }
                            Logger.LogInfo("card -> " + (b != null ? b.name : "não achei"));
                        }
                        break;
                    case "info":
                        {
                            var sb = new System.Text.StringBuilder();
                            for (int i = 0; i < 32; i++) { var ln = LayerMask.LayerToName(i); if (ln != "") sb.Append(i + "=" + ln + " "); }
                            Logger.LogInfo("camadas: " + sb);
                            foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>()) Logger.LogInfo("shader: " + sh.name + " fila=" + sh.renderQueue);
                            foreach (var c in Camera.allCameras)
                            {
                                string comps = "";
                                foreach (var x in c.GetComponents<Component>()) comps += " " + x.GetType().Name;
                                Logger.LogInfo("camera " + c.name + " hdr=" + c.allowHDR + " far=" + c.farClipPlane + " mask=" + c.cullingMask + " comps:" + comps);
                            }
                            foreach (var t in Resources.FindObjectsOfTypeAll<Collider>())
                                if (t.name.Contains("Kill") || t.name.Contains("Death") || t.name.Contains("Lava"))
                                    Logger.LogInfo("kill: " + t.name + " layer=" + LayerMask.LayerToName(t.gameObject.layer) + " tipo=" + t.GetType().Name + " trig=" + t.isTrigger + " bounds=" + t.bounds + " ativo=" + t.gameObject.activeInHierarchy);
                            foreach (var k in Object.FindObjectsOfType<RcKinematicPhysic>())
                            {
                                var f = HarmonyLib.AccessTools.Field(typeof(RcKinematicPhysic), "m_vSurfaceResponses");
                                var arr = f != null ? f.GetValue(k) as System.Collections.IEnumerable : null;
                                if (arr != null) foreach (var sr in arr) Logger.LogInfo("superficie: " + JsonUtility.ToJson(sr));
                                Logger.LogInfo("deathmask=" + k.DeathMaskValue);
                                break;
                            }
                        }
                        break;
                    default:
                        {
                            var e = Cinema.Comando(p);
                            while (e.MoveNext()) yield return e.Current;
                        }
                        break;
                    case "sel":
                        {
                            var b = GkEventSystem.Current != null ? GkEventSystem.Current.Selections[0].button : null;
                            Logger.LogInfo("selecionado: " + (b != null ? b.name : "nada"));
                        }
                        break;
                }
                if (!Cinema.Gravando) yield return new WaitForSecondsRealtime(0.35f);
            }
            Logger.LogInfo("cmd fim");
            rodandoCmd = false;
        }

        private void DumpCena(UnityEngine.SceneManagement.Scene sc)
        {
            var sb = new System.Text.StringBuilder();
            limite = sc.name.EndsWith("_GRAPH") ? 400 : 40;
            detalhe = !sc.name.EndsWith("_GRAPH");
            foreach (var root in sc.GetRootGameObjects()) DumpGo(root.transform, 0, sb);
            detalhe = true; limite = 40;
            string f = Path.Combine(Paths.BepInExRootPath, "gkmod_scene_" + sc.name + ".txt");
            File.WriteAllText(f, sb.ToString());
            Logger.LogInfo("cena salva em " + f);
        }

        private bool detalhe = true;
        private int limite = 40;
        private bool aiDumped;

        private void DumpTudo(Transform root, string nome, int lim)
        {
            var sb = new System.Text.StringBuilder();
            limite = lim;
            DumpGo(root, 0, sb);
            limite = 40;
            string f = Path.Combine(Paths.BepInExRootPath, "gkmod_obj_" + nome + ".txt");
            File.WriteAllText(f, sb.ToString());
            Logger.LogInfo("objeto salvo em " + f);
        }

        private void DumpGo(Transform t, int depth, System.Text.StringBuilder sb)
        {
            sb.Append(new string(' ', depth * 2));
            sb.Append(t.name).Append(t.gameObject.activeSelf ? "" : " [OFF]");
            sb.Append(" L=").Append(LayerMask.LayerToName(t.gameObject.layer));
            sb.Append(" p=").Append(t.position.ToString("F1"));
            if (t.childCount > 0) sb.Append(" filhos=").Append(t.childCount);
            sb.Append(" :");
            foreach (var c in t.GetComponents<Component>())
                if (c != null && !(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
            sb.Append(" rot=").Append(t.rotation.eulerAngles.ToString("F0"));
            sb.Append('\n');
            if (detalhe)
            {
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string pad = new string(' ', depth * 2 + 4);
                    var bc = c as BoxCollider;
                    if (bc != null) sb.Append(pad).Append("BoxCollider size=" + bc.size + " center=" + bc.center + " trigger=" + bc.isTrigger + "\n");
                    var sc2 = c as SphereCollider;
                    if (sc2 != null) sb.Append(pad).Append("SphereCollider r=" + sc2.radius + " trigger=" + sc2.isTrigger + "\n");
                    var mr = c as Renderer;
                    if (mr != null && mr.sharedMaterial != null) sb.Append(pad).Append("material=" + mr.sharedMaterial.name + " shader=" + mr.sharedMaterial.shader.name + "\n");
                    var mb = c as MonoBehaviour;
                    if (mb != null)
                    {
                        string js = "";
                        try { js = JsonUtility.ToJson(mb); } catch { }
                        if (js.Length > 2) sb.Append(pad).Append(c.GetType().Name + " " + (js.Length > 600 ? js.Substring(0, 600) + "..." : js) + "\n");
                    }
                }
            }
            // não desce em pastas de muitos filhos iguais (pontos de caminho, decoração)
            bool muitos = t.childCount > limite;
            for (int i = 0; i < t.childCount; i++)
            {
                if (muitos && i >= 3) { sb.Append(new string(' ', (depth + 1) * 2)).Append("... +" + (t.childCount - 3) + " filhos\n"); break; }
                DumpGo(t.GetChild(i), depth + 1, sb);
            }
        }

        private void Update()
        {
            if (!ligado) return;
            if (Time.frameCount % 30 == 0) { EsconderJanela(); AudioListener.volume = 0f; }
            ChecarComandos();
            string snap = Path.Combine(Paths.BepInExRootPath, "gkmod_snap");
            if (File.Exists(snap))
            {
                File.Delete(snap);
                n++;
                string f = Path.Combine(Paths.BepInExRootPath, "gkmod_snap" + n + ".png");
                ScreenCapture.CaptureScreenshot(f);
                Logger.LogInfo("snap " + f);
            }
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 1f;

            if (!dumped) TentarDump();
            if (!aiDumped && File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_dumpscene")))
            {
                var ai = Object.FindObjectOfType<AIPathHandler>();
                if (ai != null) { aiDumped = true; DumpTudo(ai.transform, "AIPaths", 6); }
            }
            var karts = Object.FindObjectsOfType<Kart>();
            if (karts.Length == 0) { maxHumano = maxBots = 0; return; }
            float hum = 0, bots = 0;
            if (jogador == null)
                foreach (var k in karts)
                    if (k.GetControlType() == RcVehicle.ControlType.Human) { jogador = k; break; }
            foreach (var k in karts)
            {
                var phys = k.GetComponentInChildren<RcVehiclePhysic>();
                if (phys == null) continue;
                float v = phys.GetLocalSpeedKPH();
                if (k == jogador) hum = v;
                else bots = Mathf.Max(bots, v);
            }
            maxHumano = Mathf.Max(maxHumano, hum);
            maxBots = Mathf.Max(maxBots, bots);
            Logger.LogInfo(string.Format("vel: jogador {0:0} km/h (max {1:0}) | bot mais rapido {2:0} km/h (max {3:0}) | karts={4}",
                hum, maxHumano, bots, maxBots, karts.Length));
        }
    }
}
