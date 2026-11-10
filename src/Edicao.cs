// "OVO EDITION": selo animado embaixo do logo do Garfield Kart (tela inicial e menu principal).
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace GKMod
{
    [BepInPlugin("gkmod.edicao", "GK Ovo Edition", "1.0.0")]
    public class Edicao : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        static Sprite selo;
        float prox;
        static HashSet<int> logados = new HashSet<int>();

        void Awake() { Log = Logger; Logger.LogInfo("Ovo Edition carregado."); }

        public static Sprite Selo()
        {
            if (selo != null) return selo;
            using (var s = typeof(Edicao).Assembly.GetManifestResourceStream("selo_ovo_edition.png"))
            {
                var buf = new byte[s.Length]; s.Read(buf, 0, buf.Length);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                t.LoadImage(buf); t.wrapMode = TextureWrapMode.Clamp; t.anisoLevel = 4;
                DontDestroyOnLoad(t);
                selo = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return selo;
        }

        Canvas canvas; RectTransform selo_rt; Image selo_img; Image logo; int semLogo;

        void Update()
        {
            if (Time.unscaledTime >= prox)
            {
                prox = Time.unscaledTime + 0.25f;
                Image achado = null;
                foreach (var im in FindObjectsOfType<Image>())
                {
                    if (im.sprite == null || !im.isActiveAndEnabled) continue;
                    string n = im.sprite.name.ToLower();
                    if (n.Contains("logo") && n.Contains("title")) { achado = im; break; }
                }
                if (achado != logo)
                {
                    logo = achado;
                    if (logo != null && selo_rt != null) { selo_rt.gameObject.SetActive(false); selo_rt.gameObject.SetActive(true); }   // entra quicando de novo
                }
            }
            if (canvas == null) Criar();
            bool vis = logo != null && logo.isActiveAndEnabled && logo.canvas != null;
            if (!vis) { selo_img.enabled = false; return; }
            // posiciona embaixo do desenho do logo (o retângulo do logo tem área vazia embaixo)
            var cs = new Vector3[4]; logo.rectTransform.GetWorldCorners(cs);
            Camera cam = logo.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : logo.canvas.worldCamera;
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, cs[0]), tr = RectTransformUtility.WorldToScreenPoint(cam, cs[2]);
            float w = tr.x - bl.x, h = tr.y - bl.y;
            if (w < 50f) { selo_img.enabled = false; return; }
            float k = 1080f / Screen.height;
            selo_rt.anchoredPosition = new Vector2(((bl.x + tr.x) / 2f + w * 0.015f) * k, (tr.y - h * 0.845f) * k);
            var sp = selo_img.sprite;
            float ww = w * 0.64f * k;
            selo_rt.sizeDelta = new Vector2(ww, ww * sp.rect.height / sp.rect.width);
            float a = logo.canvasRenderer.GetInheritedAlpha() * logo.color.a;
            selo_img.color = new Color(1, 1, 1, a);
            selo_img.enabled = a > 0.01f;
        }

        void Criar()
        {
            var go = new GameObject("GKOvoEdition_Canvas");
            DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
            var s = new GameObject("GKOvoEdition", typeof(RectTransform), typeof(Image), typeof(SeloAnim));
            s.transform.SetParent(go.transform, false);
            selo_rt = (RectTransform)s.transform;
            selo_rt.anchorMin = selo_rt.anchorMax = Vector2.zero; selo_rt.pivot = new Vector2(0.5f, 0.5f);
            selo_img = s.GetComponent<Image>(); selo_img.sprite = Selo(); selo_img.raycastTarget = false;
            Logger.LogInfo("selo: canvas próprio criado");
        }
    }

    // entra quicando quando aparece; depois balança devagar
    public class SeloAnim : MonoBehaviour
    {
        float t;
        void OnEnable() { t = 0f; }
        void Update()
        {
            t += Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float d = 0.35f, s;
            float t1 = t - 0.25f;                      // pequeno atraso depois do logo
            if (t1 <= 0) s = 0f;
            else if (t1 < d) { float u = t1 / d; s = 1.18f * (1 - (1 - u) * (1 - u)); }
            else { float u = t1 - d; s = 1f + 0.18f * Mathf.Exp(-u * 7f) * Mathf.Cos(u * 18f); }
            float resp = 1f + 0.015f * Mathf.Sin(t * 2.4f);
            transform.localScale = Vector3.one * s * resp;
            transform.localRotation = Quaternion.Euler(0, 0, -2.5f + 1.6f * Mathf.Sin(t * 1.3f));
        }
    }
}
