// Mini mod de teste: mostra "MOD ATIVO" na tela usando a mesma fonte
// TextMeshPro que o próprio Garfield Kart usa nos menus.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GKMod
{
    [BepInPlugin("gkmod.modativo", "GK Mod Ativo", "1.0.0")]
    public class ModAtivo : BaseUnityPlugin
    {
        private TextMeshProUGUI label;

        private void Awake()
        {
            Logger.LogInfo("GK Mod Ativo carregado. Esperando o jogo mostrar algum texto...");
            StartCoroutine(Setup());
        }

        private IEnumerator Setup()
        {
            TMP_FontAsset font = null;
            Material mat = null;
            while (font == null)
            {
                yield return new WaitForSeconds(1f);
                PickGameFont(out font, out mat);
            }

            var go = new GameObject("GKMod_Canvas");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var textGo = new GameObject("GKMod_Label");
            textGo.transform.SetParent(go.transform, false);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.font = font;
            if (mat != null) label.fontSharedMaterial = mat;
            label.text = "MOD ATIVO";
            label.fontSize = 44;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = new Color(1f, 0.62f, 0.1f); // laranja Garfield
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 18);
            rt.sizeDelta = new Vector2(900, 120);

            Logger.LogInfo("Texto criado com a fonte '" + font.name + "' e material '" +
                           (mat != null ? mat.name : "(padrao)") + "'.");

            // Só para teste automático: se existir o arquivo-gatilho, tira um print.
            string trigger = Path.Combine(Paths.BepInExRootPath, "gkmod_screenshot");
            if (File.Exists(trigger))
            {
                for (int i = 1; i <= 3; i++)
                {
                    yield return new WaitForSeconds(4f);
                    string shot = Path.Combine(Paths.BepInExRootPath, "gkmod_shot" + i + ".png");
                    ScreenCapture.CaptureScreenshot(shot);
                    Logger.LogInfo("Print salvo em " + shot);
                }
            }
        }

        private void Update()
        {
            if (label != null)
            {
                float s = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 3f);
                label.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        // Escolhe a fonte (e o material, que traz contorno/sombra) mais usada
        // pelos textos visíveis do jogo neste momento.
        private void PickGameFont(out TMP_FontAsset font, out Material mat)
        {
            font = null;
            mat = null;
            var texts = Object.FindObjectsOfType<TextMeshProUGUI>();
            if (texts.Length == 0) return;

            var fontCount = new Dictionary<TMP_FontAsset, int>();
            foreach (var t in texts)
            {
                if (t.font == null || !t.isActiveAndEnabled) continue;
                int c;
                fontCount.TryGetValue(t.font, out c);
                fontCount[t.font] = c + 1;
            }
            int best = 0;
            foreach (var kv in fontCount)
            {
                Logger.LogInfo("Fonte do jogo: '" + kv.Key.name + "' usada por " + kv.Value + " textos");
                if (kv.Value > best) { best = kv.Value; font = kv.Key; }
            }
            if (font == null) return;

            var matCount = new Dictionary<Material, int>();
            foreach (var t in texts)
            {
                if (t.font != font || !t.isActiveAndEnabled || t.fontSharedMaterial == null) continue;
                int c;
                matCount.TryGetValue(t.fontSharedMaterial, out c);
                matCount[t.fontSharedMaterial] = c + 1;
            }
            best = 0;
            foreach (var kv in matCount)
            {
                Logger.LogInfo("  material '" + kv.Key.name + "' usado por " + kv.Value);
                if (kv.Value > best) { best = kv.Value; mat = kv.Key; }
            }
        }
    }
}
