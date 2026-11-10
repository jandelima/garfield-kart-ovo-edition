// Modo 200cc para Garfield Kart Furious Racing.
// - Adiciona uma aba "200cc" ao lado de 150cc no menu de modo de jogo.
// - Por baixo, o jogo roda como 150cc (HARD); este mod só aumenta a
//   velocidade máxima e a força do motor de TODOS os karts (jogador e bots),
//   somando de novo o salto que existe entre 100cc e 150cc.
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GKMod
{
    [BepInPlugin("gkmod.200cc", "GK Modo 200cc", "1.0.0")]
    public class Modo200cc : BaseUnityPlugin
    {
        public static bool Ativo;          // 200cc escolhido no menu
        public static bool MudandoPeloMod; // evita que o setter desligue o modo
        public static ManualLogSource Log;
        public static System.Collections.Generic.HashSet<int> IconeTrocado = new System.Collections.Generic.HashSet<int>();
        public static Toggle Aba200;
        static Texture2D texIcone, texAba;
        static Sprite spIcone, spAba;

        static Texture2D CarregarPng(string nome)
        {
            using (var st = typeof(Modo200cc).Assembly.GetManifestResourceStream(nome))
            {
                if (st == null) { Log.LogWarning("Recurso não encontrado: " + nome); return null; }
                var bytes = new byte[st.Length];
                st.Read(bytes, 0, bytes.Length);
                var t = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                t.LoadImage(bytes);
                t.wrapMode = TextureWrapMode.Clamp;
                t.name = nome;
                UnityEngine.Object.DontDestroyOnLoad(t);
                return t;
            }
        }

        public static Texture2D TexIcone { get { if (texIcone == null) texIcone = CarregarPng("icon_200cc.png"); return texIcone; } }
        public static Sprite SpIcone
        {
            get
            {
                if (spIcone == null && TexIcone != null)
                    spIcone = Sprite.Create(TexIcone, new Rect(0, 0, TexIcone.width, TexIcone.height), new Vector2(0.5f, 0.5f), 200f);
                return spIcone;
            }
        }
        public static Sprite SpAba
        {
            get
            {
                if (spAba == null)
                {
                    if (texAba == null) texAba = CarregarPng("icon_200cc_tab.png");
                    if (texAba != null) spAba = Sprite.Create(texAba, new Rect(0, 0, texAba.width, texAba.height), new Vector2(0.5f, 0.5f), 200f);
                }
                return spAba;
            }
        }

        private void Awake()
        {
            Log = Logger;
            new Harmony("gkmod.200cc").PatchAll(typeof(Modo200cc).Assembly);
            Logger.LogInfo("Modo 200cc carregado.");
            // Só para teste automático: arquivo-gatilho liga o modo já de cara.
            if (File.Exists(Path.Combine(Paths.BepInExRootPath, "gkmod_force200")))
            {
                Ativo = true;
                Logger.LogInfo("[teste] 200cc forçado pelo arquivo-gatilho.");
            }
        }

        public static void Definir(bool ligado)
        {
            Ativo = ligado;
            Log.LogInfo("200cc " + (ligado ? "LIGADO" : "desligado"));
        }

        public static T Campo<T>(object obj, string nome)
        {
            var f = AccessTools.Field(obj.GetType(), nome);
            return f == null ? default(T) : (T)f.GetValue(obj);
        }

        public static Array RawArray(object enumArray)
        {
            if (enumArray == null) return null;
            var p = enumArray.GetType().GetProperty("RawArray", BindingFlags.Public | BindingFlags.Instance);
            return p == null ? null : (Array)p.GetValue(enumArray, null);
        }
    }

    // Qualquer mudança de dificuldade feita pelo jogo (online, contra-relógio,
    // desafios...) desliga o 200cc, para nunca vazar para onde não deve.
    [HarmonyPatch(typeof(GameConfigurator), "Difficulty", MethodType.Setter)]
    static class Patch_SetDifficulty
    {
        static void Postfix(Difficulty value)
        {
            if (!Modo200cc.MudandoPeloMod && Modo200cc.Ativo)
                Modo200cc.Definir(false);
        }
    }

    // Física: aplica o 200cc em cada kart quando ele calcula seus atributos.
    [HarmonyPatch(typeof(KartKinematicPhysic), "UpdateStats")]
    static class Patch_UpdateStats
    {
        static readonly FieldInfo fSpeed = AccessTools.Field(typeof(RcVehiclePhysic), "modifiedSpeed");
        static readonly FieldInfo fTorque = AccessTools.Field(typeof(RcVehiclePhysic), "modifiedTorque");

        static void Postfix(KartKinematicPhysic __instance)
        {
            var p = __instance;
            if (!Modo200cc.Ativo || p.Difficulty != Difficulty.HARD) return;
            if (Singleton<GameConfigurator>.Instance.IsNetworkGame) return;
            if (p.m_Speeds == null || p.m_Speeds.Length < 3 || p.m_MaxMotorTorque == null || p.m_MaxMotorTorque.Length < 3) return;

            float s100 = p.m_Speeds[1].m_MaxSpeedKPH, s150 = p.m_Speeds[2].m_MaxSpeedKPH;
            float t100 = p.m_MaxMotorTorque[1], t150 = p.m_MaxMotorTorque[2];
            // Mesmo salto de 100->150, com piso de +25% caso os valores sejam próximos.
            float addSpeed = Mathf.Max(s150 - s100, s150 * 0.25f);
            float torqueScale = Mathf.Max(t100 > 0f ? t150 / t100 : 1f, (s150 + addSpeed) / s150);

            float speed = (float)fSpeed.GetValue(p);
            float torque = (float)fTorque.GetValue(p);
            fSpeed.SetValue(p, speed + addSpeed);
            fTorque.SetValue(p, torque * torqueScale);
            Modo200cc.Log.LogInfo(string.Format(
                "200cc em '{0}': tabela 100cc={1}km/h 150cc={2}km/h | velocidade {3:0.0} -> {4:0.0} km/h, torque {5:0} -> {6:0}",
                p.transform.root.name, s100, s150, speed, speed + addSpeed, torque, torque * torqueScale));
        }
    }

    // Menu: cria a aba 200cc clonando a aba 150cc.
    [HarmonyPatch(typeof(MenuHDGameMode), "Enter")]
    static class Patch_MenuEnter
    {
        static void Postfix(MenuHDGameMode __instance)
        {
            try { CriarAba(__instance); }
            catch (Exception e) { Modo200cc.Log.LogError("Falha ao criar aba 200cc: " + e); }
        }

        static void CriarAba(MenuHDGameMode menu)
        {
            var tabs = Modo200cc.RawArray(Modo200cc.Campo<object>(menu, "m_tabs"));
            if (tabs == null || tabs.Length < 3) { Modo200cc.Log.LogWarning("Abas de dificuldade não encontradas."); return; }
            var t100 = (Toggle)tabs.GetValue(1);
            var t150 = (Toggle)tabs.GetValue(2);

            if (Modo200cc.Aba200 == null)
            {
                var go = UnityEngine.Object.Instantiate(t150.gameObject, t150.transform.parent, false);
                go.name = "Tab_200cc";
                go.transform.SetSiblingIndex(t150.transform.GetSiblingIndex() + 1);
                var aba = go.GetComponent<Toggle>();
                aba.onValueChanged = new Toggle.ToggleEvent(); // tira os eventos copiados do 150cc
                aba.group = t150.group;
                aba.isOn = false;

                // Sem layout automático? Posiciona com o mesmo espaçamento entre 100 e 150.
                if (t150.transform.parent.GetComponent<LayoutGroup>() == null)
                {
                    var r100 = (RectTransform)t100.transform;
                    var r150 = (RectTransform)t150.transform;
                    ((RectTransform)go.transform).anchoredPosition = r150.anchoredPosition + (r150.anchoredPosition - r100.anchoredPosition);
                }

                // Navegação com LB/RB e setas: 150 <-> 200.
                var nav150 = t150.navigation;
                nav150.mode = Navigation.Mode.Explicit;
                nav150.selectOnLeft = t100;
                nav150.selectOnRight = aba;
                t150.navigation = nav150;
                var nav200 = new Navigation();
                nav200.mode = Navigation.Mode.Explicit;
                nav200.selectOnLeft = t150;
                aba.navigation = nav200;

                aba.onValueChanged.AddListener(on =>
                {
                    if (!on) return;
                    Modo200cc.MudandoPeloMod = true;
                    try { menu.SetDifficultyHard(); } finally { Modo200cc.MudandoPeloMod = false; }
                    Modo200cc.Definir(true);
                });
                for (int i = 0; i < tabs.Length; i++)
                {
                    ((Toggle)tabs.GetValue(i)).onValueChanged.AddListener(on => { if (on) Modo200cc.Definir(false); });
                }
                Modo200cc.Aba200 = aba;
                Modo200cc.Log.LogInfo("Aba 200cc criada ao lado de 150cc (pai: " + t150.transform.parent.name + ").");
            }

            foreach (var tmp in Modo200cc.Aba200.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.text = "200cc";
            foreach (var img in Modo200cc.Aba200.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null) continue;
                string n = img.sprite.name.ToLowerInvariant();
                if (!Modo200cc.IconeTrocado.Contains(img.GetInstanceID()))
                    Modo200cc.Log.LogInfo("aba 200cc: imagem '" + img.name + "' sprite '" + img.sprite.name + "'");
                if ((n.Contains("150") || n.Contains("hard") || n.Contains("kettle") || n.Contains("cafeti") || n.Contains("teapot") || n.Contains("coffee")) && Modo200cc.SpAba != null)
                {
                    img.sprite = Modo200cc.SpAba;
                    img.preserveAspect = true;
                }
                Modo200cc.IconeTrocado.Add(img.GetInstanceID());
            }
            Modo200cc.Aba200.interactable = t150.interactable;
        }
    }

    // Contra-relógio trava as abas: trava a 200cc junto; outros modos destravam.
    [HarmonyPatch(typeof(MenuHDGameMode), "OnSelectTimeTrialButton")]
    static class Patch_TimeTrial
    {
        static void Postfix() { if (Modo200cc.Aba200 != null) Modo200cc.Aba200.interactable = false; }
    }

    [HarmonyPatch(typeof(MenuHDGameMode), "OnSelectSingleRaceButton")]
    static class Patch_Single
    {
        static void Postfix() { if (Modo200cc.Aba200 != null) Modo200cc.Aba200.interactable = true; }
    }

    [HarmonyPatch(typeof(MenuHDGameMode), "OnSelectChampionShipButton")]
    static class Patch_Champ
    {
        static void Postfix() { if (Modo200cc.Aba200 != null) Modo200cc.Aba200.interactable = true; }
    }

    // Cabeçalho dos menus (uGUI): troca a chaleira 150cc pela 200cc.
    [HarmonyPatch(typeof(CommonMenuHeaderInfos), "InitHeaderInfos")]
    static class Patch_Header
    {
        static void Postfix(CommonMenuHeaderInfos __instance)
        {
            if (!Modo200cc.Ativo || Modo200cc.SpIcone == null) return;
            var img = Modo200cc.Campo<Image>(__instance, "m_difficultyImage");
            if (img != null && img.sprite != null && img.sprite.name.Contains("150")) img.sprite = Modo200cc.SpIcone;
        }
    }

    // Telas de corrida/resultados (NGUI): cobre o sprite 150cc com a textura 200cc.
    [HarmonyPatch(typeof(UITexturePattern), "ChangeTexture")]
    static class Patch_NguiIcon
    {
        static void Postfix(UITexturePattern __instance, int iNum)
        {
            try
            {
                var sprite = __instance.GetComponent<UISprite>();
                if (sprite == null) return;
                Transform old = __instance.transform.Find("GK200cc_Icon");
                bool eh150 = sprite.spriteName != null && sprite.spriteName.Contains("150");
                bool usar = Modo200cc.Ativo && eh150 && Modo200cc.TexIcone != null;
                if (!usar)
                {
                    if (old != null) { UnityEngine.Object.Destroy(old.gameObject); sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, 1f); }
                    return;
                }
                if (old != null) return;
                var tex = NGUITools.AddChild<UITexture>(__instance.gameObject);
                tex.gameObject.name = "GK200cc_Icon";
                tex.gameObject.layer = __instance.gameObject.layer;
                tex.pivot = sprite.pivot;
                tex.transform.localPosition = Vector3.zero;
                tex.transform.localScale = Vector3.one;
                tex.depth = sprite.depth + 1;
                var mat = sprite.material;
                if (mat != null && mat.shader != null) tex.shader = mat.shader;
                tex.mainTexture = Modo200cc.TexIcone;
                var c = sprite.color; sprite.color = new Color(c.r, c.g, c.b, 0f);
                Modo200cc.Log.LogInfo("Ícone 200cc aplicado em " + __instance.name + " (sprite " + sprite.spriteName + ")");
            }
            catch (Exception e) { Modo200cc.Log.LogError("Ícone NGUI: " + e); }
        }
    }
}
