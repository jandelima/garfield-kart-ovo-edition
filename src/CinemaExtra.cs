// Prévia do GRAND GRAND PRIX na própria interface do jogo (só para o trailer; não é um modo jogável).
//   ggp_colher   (na tela de Grand Prix) guarda os troféus das 4 copas
//   ggp_card     (na tela de modo de jogo) cria o 4º card "GRAND GRAND PRIX" e reorganiza os cards
//   ggp_sel N    seleciona o card e mantém a descrição por N quadros
//   ggp_tela     (na tela de Grand Prix) aba GGP + grade animada com as 16 pistas
using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GKMod
{
    public static class CinemaExtra
    {
        static List<Sprite> trofeus = new List<Sprite>();
        static GameObject cardGGP;
        const string DESC = "TODAS AS PISTAS DE TODOS OS GRAND PRIX. UM SÓ CAMPEÃO!";

        static Array Raw(object enumArray)
        {
            return (Array)enumArray.GetType().GetProperty("RawArray").GetValue(enumArray, null);
        }

        static Image IconeDe(Transform t, Image excluir)
        {
            // o maior Image filho que não é o fundo
            Image melhor = null; float area = 0;
            foreach (var im in t.GetComponentsInChildren<Image>(true))
            {
                if (im == excluir || im.sprite == null || im.transform == t) continue;
                var r = im.rectTransform.rect; float a = r.width * r.height;
                if (a > area) { area = a; melhor = im; }
            }
            return melhor;
        }

        public static IEnumerator Comando(string[] p)
        {
            switch (p[0])
            {
                case "ggp_colher":
                    {
                        var ts = UnityEngine.Object.FindObjectOfType<MenuHDTrackSelection>();
                        if (ts == null) { Teste.LogS("ggp_colher: tela de pista não aberta"); break; }
                        var tabs = Raw(AccessTools.Field(typeof(MenuHDTrackSelection), "m_tabs").GetValue(ts));
                        trofeus.Clear();
                        for (int i = 0; i < 4 && i < tabs.Length; i++)
                        {
                            var tg = (Toggle)tabs.GetValue(i);
                            var im = IconeDe(tg.transform, tg.targetGraphic as Image);
                            Teste.LogS("ggp: aba " + i + " ícone " + (im != null ? im.name + " / " + im.sprite.name : "nenhum"));
                            if (im != null) trofeus.Add(im.sprite);
                        }
                    }
                    break;
                case "ggp_card":
                    {
                        var gm = UnityEngine.Object.FindObjectOfType<MenuHDGameMode>();
                        if (gm == null) { Teste.LogS("ggp_card: menu de modo não aberto"); break; }
                        var bts = Raw(AccessTools.Field(typeof(MenuHDGameMode), "m_buttons").GetValue(gm));
                        var lista = new List<RectTransform>();
                        Button gp = null;
                        for (int i = 0; i < bts.Length; i++)
                        {
                            var b = (Button)bts.GetValue(i);
                            if (b == null) continue;
                            lista.Add((RectTransform)b.transform);
                            Teste.LogS("ggp: card " + i + " " + b.name + " x=" + ((RectTransform)b.transform).anchoredPosition);
                            if (i == 1) gp = b;
                        }
                        if (gp == null || lista.Count < 3) break;
                        lista.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
                        cardGGP = UnityEngine.Object.Instantiate(gp.gameObject, gp.transform.parent);
                        cardGGP.name = "Card_GrandGrandPrix";
                        var bt = cardGGP.GetComponent<Button>();
                        bt.onClick = new Button.ButtonClickedEvent();
                        foreach (var mb in cardGGP.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            string tn = mb.GetType().Name;
                            if (tn.Contains("Locali") || tn.Contains("I2")) mb.enabled = false;
                        }
                        foreach (var t in cardGGP.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.text = "GRAND\nGRAND PRIX"; t.enableWordWrapping = false; t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = t.fontSize * 0.5f; }
                        foreach (var im0 in cardGGP.GetComponentsInChildren<Image>(true))
                            Teste.LogS("ggp: filho " + im0.name + " sprite=" + (im0.sprite != null ? im0.sprite.name : "-") + " tam=" + im0.rectTransform.rect.size);
                        // ícone: as 4 taças juntas em pirâmide, com brilho dourado
                        Image ic = null;
                        foreach (var im0 in cardGGP.GetComponentsInChildren<Image>(true))
                            if (im0.sprite != null && (im0.sprite.name.ToLower().Contains("cup") || im0.sprite.name.ToLower().Contains("troph") || im0.sprite.name.ToLower().Contains("champ"))) { ic = im0; break; }
                        if (ic == null) ic = IconeDe(cardGGP.transform, bt.targetGraphic as Image);
                        Teste.LogS("ggp: ícone do card = " + (ic != null ? ic.name + "/" + ic.sprite.name : "nenhum"));
                        if (ic != null && trofeus.Count == 4)
                        {
                            var r = ic.rectTransform;
                            ic.enabled = false;
                            var cont = new GameObject("Tacas", typeof(RectTransform));
                            cont.transform.SetParent(r.parent, false);
                            cont.layer = cont.transform.parent.gameObject.layer;
                            var cr = (RectTransform)cont.transform;
                            cr.anchorMin = r.anchorMin; cr.anchorMax = r.anchorMax; cr.pivot = r.pivot; cr.anchoredPosition = r.anchoredPosition; cr.sizeDelta = r.sizeDelta;
                            cont.transform.SetSiblingIndex(r.GetSiblingIndex() + 1);
                            float w = r.rect.width, h = r.rect.height;
                            var pos = new[] { new Vector2(-0.28f, -0.16f), new Vector2(0.28f, -0.16f), new Vector2(-0.14f, 0.16f), new Vector2(0.14f, 0.16f) };
                            int[] ordem = { 2, 3, 0, 1 };      // as de trás primeiro
                            foreach (int i in ordem)
                            {
                                var g = new GameObject("Taca" + i, typeof(RectTransform), typeof(Image));
                                g.transform.SetParent(cont.transform, false);
                                g.layer = g.transform.parent.gameObject.layer;
                                var gr = (RectTransform)g.transform;
                                gr.sizeDelta = new Vector2(w * 0.62f, h * 0.62f);
                                gr.anchoredPosition = new Vector2(pos[i].x * w, pos[i].y * h);
                                var im = g.GetComponent<Image>(); im.sprite = trofeus[i]; im.preserveAspect = true;
                            }
                        }

                        // 4 cards lado a lado
                        lista.Add((RectTransform)cardGGP.transform);
                        float x0 = lista[0].anchoredPosition.x, x2 = lista[2].anchoredPosition.x;
                        float centro = (x0 + x2) / 2f, passo = (x2 - x0) / 2f * 0.80f;
                        for (int i = 0; i < 4; i++)
                        {
                            lista[i].localScale = Vector3.one * 0.80f;
                            var ap = lista[i].anchoredPosition; ap.x = centro + (i - 1.5f) * passo; lista[i].anchoredPosition = ap;
                        }
                        // navegação: GP <-> TIME TRIAL <-> GGP
                        var nav = bt.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnLeft = lista[2].GetComponent<Button>(); bt.navigation = nav;
                        Teste.LogS("ggp: card criado");
                    }
                    break;
                case "ggp_sel":
                    {
                        var gm = UnityEngine.Object.FindObjectOfType<MenuHDGameMode>();
                        if (cardGGP == null || gm == null) break;
                        GkEventSystem.Current.SelectButton(cardGGP.GetComponent<Button>());
                        var info = (TextMeshProUGUI)AccessTools.Field(typeof(MenuHDGameMode), "m_textInfos").GetValue(gm);
                        int n = p.Length > 1 ? int.Parse(p[1]) : 60;
                        for (int i = 0; i < n; i++)
                        {
                            if (info != null) info.text = DESC;
                            if (cardGGP != null) cardGGP.transform.localScale = Vector3.one * (0.80f + 0.025f * Mathf.Sin(i * 0.25f));
                            yield return null;
                        }
                    }
                    break;
                case "ggp_tela":
                    {
                        var ts = UnityEngine.Object.FindObjectOfType<MenuHDTrackSelection>();
                        if (ts == null) { Teste.LogS("ggp_tela: tela de pista não aberta"); break; }
                        var go = new GameObject("GKCine_GGP");
                        go.AddComponent<TelaGGP>().Montar(ts, trofeus);
                    }
                    break;
            }
            yield break;
        }
    }

    // aba "GRAND GRAND PRIX" + grade animada com as 16 pistas sobre o painel de pistas do jogo
    public class TelaGGP : MonoBehaviour
    {
        class Celula { public RectTransform rt; public int ini; }
        List<Celula> cel = new List<Celula>();
        int q;
        TextMeshProUGUI texto;
        RectTransform painel, brilhoTab;

        public void Montar(MenuHDTrackSelection ts, List<Sprite> trofeus)
        {
            var tabs = (Array)AccessTools.Field(typeof(MenuHDTrackSelection), "m_tabs").GetValue(ts).GetType().GetProperty("RawArray").GetValue(AccessTools.Field(typeof(MenuHDTrackSelection), "m_tabs").GetValue(ts), null);
            // aba 4 (sorteio "?") vira a aba GGP: as 4 taças pequenas
            var tab4 = (Toggle)tabs.GetValue(4);
            GkEventSystem.Current.SelectTab(tab4);
            Image icone = null; float area = 0;
            foreach (var im in tab4.GetComponentsInChildren<Image>(true))
            {
                if (im == tab4.targetGraphic || im.sprite == null || im.transform == tab4.transform) continue;
                var r = im.rectTransform.rect; if (r.width * r.height > area) { area = r.width * r.height; icone = im; }
            }
            if (icone != null && trofeus.Count == 4)
            {
                icone.enabled = false;
                var r = icone.rectTransform;
                float w = r.rect.width;
                for (int i = 0; i < 4; i++)
                {
                    var g = new GameObject("TacaMini" + i, typeof(RectTransform), typeof(Image));
                    g.transform.SetParent(r.parent, false);
                    g.layer = g.transform.parent.gameObject.layer;
                    var gr = (RectTransform)g.transform;
                    gr.anchorMin = r.anchorMin; gr.anchorMax = r.anchorMax; gr.pivot = r.pivot;
                    gr.sizeDelta = r.sizeDelta * 0.62f;
                    gr.anchoredPosition = r.anchoredPosition + new Vector2((i - 1.5f) * w * 0.30f, (i == 1 || i == 2) ? w * 0.08f : 0f);
                    var im = g.GetComponent<Image>(); im.sprite = trofeus[i]; im.preserveAspect = true;
                }
                brilhoTab = (RectTransform)tab4.transform;
            }
            // painel de pistas
            var ui = (GameObject)AccessTools.Field(typeof(MenuHDTrackSelection), "m_championShipUIToShow").GetValue(ts);
            painel = (RectTransform)ui.transform;
            var raiz = new GameObject("GradeGGP", typeof(RectTransform), typeof(Image));
            raiz.transform.SetParent(painel, false);
            raiz.layer = raiz.transform.parent.gameObject.layer;
            var rr = (RectTransform)raiz.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            raiz.GetComponent<Image>().color = new Color(0.12f, 0.03f, 0.02f, 1f);
            float W = painel.rect.width, H = painel.rect.height, gap = 6f;
            float cw = (W - gap * 5) / 4f, ch = (H - gap * 5) / 4f;
            var dados = PlayerGameEntities.ChampionShipDataList;
            if (dados != null) foreach (var dd in dados) { try { dd.Localize(); } catch (Exception) { } }
            var f = Cinema.Fonte();
            for (int c = 0; c < 4 && dados != null && c < dados.Count; c++)
                for (int t = 0; t < 4; t++)
                {
                    var sp = dados[c].Sprites[t];
                    var g = new GameObject("Pista" + c + t, typeof(RectTransform), typeof(RawImage));
                    g.transform.SetParent(raiz.transform, false);
                    g.layer = g.transform.parent.gameObject.layer;
                    var gr = (RectTransform)g.transform;
                    gr.anchorMin = gr.anchorMax = new Vector2(0, 1); gr.pivot = new Vector2(0.5f, 0.5f);
                    gr.sizeDelta = new Vector2(cw, ch);
                    gr.anchoredPosition = new Vector2(gap + cw * 0.5f + t * (cw + gap), -(gap + ch * 0.5f + c * (ch + gap)));
                    var ri = g.GetComponent<RawImage>();
                    if (sp != null)
                    {
                        ri.texture = sp.texture;
                        var tr = sp.textureRect; float tw = sp.texture.width, th = sp.texture.height;
                        float hCrop = tr.width * ch / cw;          // recorte horizontal no meio da imagem alta
                        float y0 = tr.y + (tr.height - hCrop) * 0.42f;
                        ri.uvRect = new Rect(tr.x / tw, y0 / th, tr.width / tw, Mathf.Min(hCrop, tr.height) / th);
                    }
                    // nome da pista
                    var lg = new GameObject("Nome", typeof(RectTransform));
                    lg.transform.SetParent(g.transform, false);
                    lg.layer = lg.transform.parent.gameObject.layer;
                    var lr = (RectTransform)lg.transform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0.34f); lr.offsetMin = lr.offsetMax = Vector2.zero;
                    var tx = lg.AddComponent<TextMeshProUGUI>();
                    if (f != null) tx.font = f;
                    tx.text = dados[c].TracksName != null && t < dados[c].TracksName.Length ? dados[c].TracksName[t] : "";
                    tx.fontSize = 22; tx.alignment = TextAlignmentOptions.Center; tx.enableAutoSizing = true; tx.fontSizeMin = 12; tx.fontSizeMax = 22;
                    tx.color = Color.white; tx.fontStyle = FontStyles.Bold; tx.outlineWidth = 0.3f; tx.outlineColor = new Color32(40, 10, 5, 255);
                    gr.localScale = Vector3.zero;
                    cel.Add(new Celula { rt = gr, ini = 6 + (c * 4 + t) * 5 });
                }
            // texto de baixo do jogo
            var txt = (TextMeshProUGUI)AccessTools.Field(typeof(MenuHDTrackSelection), "m_textCupCircuit").GetValue(ts);
            texto = txt;
        }

        void Update()
        {
            q++;
            foreach (var c in cel)
            {
                int k = q - c.ini;
                float s = k <= 0 ? 0f : k < 7 ? Mathf.Lerp(0f, 1.12f, k / 7f) : k < 12 ? Mathf.Lerp(1.12f, 1f, (k - 7) / 5f) : 1f;
                c.rt.localScale = Vector3.one * s;
            }
            if (texto != null) texto.text = "GRAND GRAND PRIX  -  4 COPAS  -  16 PISTAS";
            if (brilhoTab != null) brilhoTab.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(q * 0.2f));
        }
    }
}
