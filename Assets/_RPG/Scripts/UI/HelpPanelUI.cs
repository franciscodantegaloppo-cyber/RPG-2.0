using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Self-bootstrapping like InventoryUI's EnsureEventSystem pattern: adds itself once per scene
// load so no manual scene wiring is needed, and rebuilds its canvas fresh each time (it doesn't
// need to survive scene loads - SpawnVillage/Dungeon both get their own instance).
public class HelpPanelUI : MonoBehaviour
{
    GameObject helpPanel;
    bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "NewGame")
            return;
        if (FindAnyObjectByType<HelpPanelUI>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("HelpPanelUI");
        go.AddComponent<HelpPanelUI>();
    }

    void Awake()
    {
        Build();
        CloseHelp();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (isOpen && kb.escapeKey.wasPressedThisFrame)
            CloseHelp();
    }

    public void ToggleHelp()
    {
        if (isOpen) CloseHelp();
        else OpenHelp();
    }

    void OpenHelp()
    {
        isOpen = true;
        helpPanel.SetActive(true);
        GameManager.Instance?.SetState(GameState.InMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseHelp()
    {
        isOpen = false;
        helpPanel.SetActive(false);
        GameManager.Instance?.SetState(GameState.Exploration);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Build()
    {
        EnsureEventSystem();

        // FindAnyObjectByType<Canvas>() grabs whichever canvas Unity happens to enumerate first,
        // which since EnemyHealthBar started creating one small WorldSpace canvas per enemy is no
        // longer reliably the HUD - see InventoryUI.FindReusableCanvas() for the full story.
        GameObject canvasGo = new GameObject("HelpCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2100;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        BuildHelpButton(canvas.transform);
        BuildHelpPanel(canvas.transform);
    }

    void BuildHelpButton(Transform canvasTransform)
    {
        GameObject go = new GameObject("HelpButton");
        go.transform.SetParent(canvasTransform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-20f, 20f);
        rect.sizeDelta = new Vector2(320f, 62f);

        Image image = go.AddComponent<Image>();
        Sprite banner = Resources.Load<Sprite>("UI/SharpUI/DialogHeader");
        if (banner != null)
        {
            image.sprite = banner;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else image.color = new Color(0.1f, 0.11f, 0.13f, 1f);
        Button button = go.AddComponent<Button>();
        button.onClick.AddListener(ToggleHelp);

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "Presiona I para ayuda";
        text.fontSize = 15;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 15f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -8f);
    }

    void BuildHelpPanel(Transform canvasTransform)
    {
        helpPanel = new GameObject("HelpPanel");
        helpPanel.transform.SetParent(canvasTransform, false);
        // Was 0.14-0.86 / 0.08-0.92 - the help text (controls + runas + rareza + modificadores +
        // armadura + arrastrar-para-equipar, several paragraphs) visibly overflowed past the
        // frame art's own edges at that size. Bigger box, same frame art, more room for the text.
        RectTransform rect = helpPanel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.04f);
        rect.anchorMax = new Vector2(0.95f, 0.96f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = helpPanel.AddComponent<Image>();
        Sprite frame = Resources.Load<Sprite>("UI/SharpUI/Panel");
        if (frame != null)
        {
            bg.sprite = frame;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        else bg.color = new Color(0.035f, 0.04f, 0.045f, 1f);

        // Leave enough breathing room for the sliced SharpUI panel border so it
        // never overlaps the first characters of a text line.
        VerticalLayoutGroup outerLayout = helpPanel.AddComponent<VerticalLayoutGroup>();
        outerLayout.padding = new RectOffset(72, 72, 64, 50);
        outerLayout.spacing = 10;
        outerLayout.childControlWidth = true;
        outerLayout.childControlHeight = true;
        outerLayout.childForceExpandWidth = true;
        outerLayout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText(helpPanel.transform, "Ayuda", 26, FontStyles.Bold);
        LayoutElement titleLE = title.gameObject.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 36f;
        titleLE.flexibleHeight = 0f;

        GameObject scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(helpPanel.transform, false);
        LayoutElement scrollLE = scrollGo.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1f;
        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0.001f);
        RectMask2D mask = scrollGo.AddComponent<RectMask2D>();

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        // Without zeroing these, Content kept RectTransform's default 100 sizeDelta baked on top
        // of its full-width anchors, making it 100px wider than the Viewport. With horizontal
        // scrolling locked (scroll.horizontal = false) that overflow silently clipped ~50px off
        // BOTH edges (worse on the left, where it read as every line missing its first few
        // letters - "CONTROLES" showing as "TROLES"). This stretches Content to exactly match
        // the Viewport's width instead.
        contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
        contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 20, 4, 4);
        contentLayout.spacing = 4;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        TextMeshProUGUI body = CreateText(content.transform, BuildHelpText(), 16, FontStyles.Normal);
        body.textWrappingMode = TextWrappingModes.Normal;

        GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform));
        closeGo.transform.SetParent(helpPanel.transform, false);
        LayoutElement closeLE = closeGo.AddComponent<LayoutElement>();
        closeLE.preferredHeight = 44f;
        closeLE.flexibleHeight = 0f;
        Image closeImage = closeGo.AddComponent<Image>();
        closeImage.color = new Color(0.16f, 0.18f, 0.19f, 1f);
        Button closeButton = closeGo.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(CloseHelp);
        TextMeshProUGUI closeText = CreateText(closeButton.transform, "Cerrar (I)", 16, FontStyles.Bold);
        closeText.alignment = TextAlignmentOptions.Center;
        RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        SharpUIWindowChrome.Attach(helpPanel, "AYUDA Y CONTROLES", CloseHelp);
    }

    TextMeshProUGUI CreateText(Transform parent, string text, int size, FontStyles style)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    static string BuildHelpText()
    {
        return
"<b>CONTROLES</b>\n" +
"WASD - Moverse\n" +
"Shift izquierdo - Sprintar (consume estamina)\n" +
"Mouse - Mirar / mover la camara\n" +
"Rueda del mouse - Acercar o alejar la camara\n" +
"Click izquierdo - Atacar o lanzar el hechizo equipado\n" +
"Click derecho mantenido - Bloquear ataques frontales\n" +
"Click derecho justo antes del golpe - Parada perfecta (parry)\n" +
"Z - Agacharse o volver a ponerse de pie\n" +
"E - Interactuar (hablar, abrir puertas, comerciar)\n" +
"E - Avanzar dialogos y omitir cinematicas\n" +
"Tab - Abrir/cerrar inventario\n" +
"G - Desenvainar / envainar arma\n" +
"Alt izquierdo o V - Esquivar (rodar, con invulnerabilidad breve)\n" +
"Espacio - Saltar\n" +
"1 a 5 - Equipar armas de la barra rapida\n" +
"F, H, R, C, O - Equipar hechizos de la barra rapida\n" +
"K - Abrir/cerrar arbol de habilidades\n" +
"J - Abrir/cerrar diario de misiones\n" +
"Enter - Abrir chat y enviar mensajes/comandos\n" +
"I - Abrir/cerrar esta ayuda\n" +
"P - Pausa y opciones\n" +
"Esc - Cerrar ventanas o cancelar\n\n" +

"<b>ANIMACIONES RPG Y ESCUDO</b>\n" +
"El escudo aparece automaticamente en la mano izquierda mientras mantienes click derecho. " +
"La animacion de impacto se reproduce cuando el bloqueo recibe un golpe.\n" +
"F1 a F3 - Variantes de ataque A\n" +
"F4 a F6 - Variantes de ataque B\n" +
"F7 - Vista previa de impacto contra el escudo\n" +
"F8 y F9 - Reacciones al recibir golpes\n" +
"F10 - Alternar acciones especiales\n" +
"F11 - Alternar gestos de dialogo\n" +
"F12 - Animacion de salto alternativa\n" +
"Shift + F1/F2 - Animaciones agachado quieto/en movimiento\n" +
"Shift + F3/F4 - Animaciones de escalera\n" +
"Shift + F5/F6 - Ataques con arco\n" +
"Shift + F7 - Ataque de enemigo del pack\n" +
"Shift + F8/F9 - Posturas de bloqueo y arma a una mano\n" +
"Shift + F10/F11/F12 - Sprint, carrera y caminata del pack\n" +
"Re Pag / Av Pag - Elegir cualquiera de las animaciones importadas\n" +
"Inicio - Reproducir la animacion elegida; Fin - Cancelarla\n\n" +

"<b>INTERFAZ E INVENTARIO</b>\n" +
"Arrastra objetos para moverlos, equiparlos o asignarlos a la barra rapida. " +
"Con el mouse sobre un arma, presiona 1 a 5 para asignarla directamente. " +
"Click derecho abre las acciones del objeto (usar pocion, equipar, etc.). " +
"En el retrato 3D, arrastra para girar al personaje y usa la rueda para acercar o alejar. " +
"En el arbol de habilidades, arrastra para recorrerlo, usa la rueda para el zoom y " +
"haz click sobre un nodo para invertir un punto.\n\n" +

"<b>MEJORA DE EQUIPAMIENTO (RUNAS)</b>\n" +
"Las runas caen de los monstruos al derrotarlos. Con el inventario abierto, " +
"click derecho sobre un arma o armadura y elegi 'Mejorar (usa 1 runa)'. Cada mejora sube " +
"el nivel del objeto en +1 (hasta +100) y aumenta sus estadisticas base un 3% por nivel. " +
"Ademas, cada mejora tiene una probabilidad de agregar un modificador (afijo) aleatorio nuevo, " +
"hasta un maximo de 4 por objeto.\n\n" +

"<b>RAREZA</b>\n" +
"La cantidad de modificadores de un objeto determina su rareza y el color de su nombre:\n" +
"Comun (blanco, 0 modificadores) - Magico (celeste, 1) - Raro (amarillo, 2) - " +
"Epico (violeta, 3) - Legendario (naranja, 4).\n\n" +

"<b>MODIFICADORES POSIBLES</b>\n" +
"Dano fisico / Dano magico: aumentan el dano infligido por el arma.\n" +
"Dano contra no muertos / bestias / jefes: dano extra segun el tipo de enemigo.\n" +
"Probabilidad de golpe critico: chance de infligir el doble de dano en un golpe.\n" +
"Dano critico: cuanto dano extra hace un golpe critico.\n" +
"Penetracion de armadura: ignora una parte fija de la armadura del objetivo.\n" +
"Filo: aumenta la penetracion de armadura del arma.\n" +
"Velocidad de ataque: reduce el tiempo entre ataques.\n" +
"Alcance del arma: aumenta la distancia a la que llegan los golpes.\n" +
"Pasa el mouse sobre cualquier objeto del inventario (sin hacer click) para ver sus " +
"caracteristicas exactas.\n\n" +

"<b>ARMADURA DE LOS ENEMIGOS</b>\n" +
"Los enemigos como los esqueletos tienen armadura (los esqueletos: 20). La armadura reduce " +
"el dano recibido con retornos decrecientes (100 de armadura reduce el dano a la mitad). " +
"La penetracion y el Filo del arma reducen la armadura efectiva del objetivo antes de calcular " +
"el dano final.\n\n" +

"<b>ARRASTRAR PARA EQUIPAR</b>\n" +
"En el inventario, mantene presionado el click sobre un objeto de la mochila y arrastralo " +
"hasta un casillero de equipo para equiparlo, o arrastra un objeto equipado hacia la mochila " +
"para quitartelo.";
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
