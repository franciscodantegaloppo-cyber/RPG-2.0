using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillTreeUI : MonoBehaviour
{
    const float TreeCanvasSize = 5200f;
    struct Node
    {
        public string id, parent, title;
        public Vector2 pos;
        public bool major;
        public Node(string i,string p,string t,Vector2 position,bool isMajor=false)
        { id=i;parent=p;title=t;pos=position;major=isMajor; }
    }
    Node[] nodeData;
    GameObject panel;
    RectTransform treeContent;
    TextMeshProUGUI pointsLabel;
    GameObject tooltip;
    TextMeshProUGUI tooltipTitle;
    TextMeshProUGUI tooltipDescription;
    readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    readonly Dictionary<string, Image> nodeIcons = new Dictionary<string, Image>();
    readonly Dictionary<string, Image> connections = new Dictionary<string, Image>();
    SkillTreeProgress tree;
    PlayerStats stats;
    SkillIconCatalog iconCatalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap(){ if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="NewGame")return; if(FindAnyObjectByType<SkillTreeUI>(FindObjectsInactive.Include)==null) new GameObject("RuntimeSkillTreeUI").AddComponent<SkillTreeUI>(); }
    void Awake(){ DontDestroyOnLoad(gameObject);nodeData=BuildNodeData(); }

    Node[] BuildNodeData()
    {
        // 1 central node + 13 perfectly repeated branches * 40 nodes = 521.
        // Every ring distributes its nodes uniformly inside the same angular sector, so the
        // result remains rotationally symmetric even as the outer rings become denser.
        var result=new List<Node>(521);
        result.Add(new Node("core","","Núcleo del héroe",new Vector2(.5f,.5f),true));
        string[] prefixes={"sword","dragon","fire_res","magic","lightning","wind","athletics","gather","defense","resist","ice_res","all_resist","vitality"};
        string[] titles={"Espadas","Cazadragones","Fuego","Arcano","Rayo","Viento","Atletismo","Supervivencia","Defensa","Resistencia oscura","Hielo","Resistencia elemental","Vitalidad"};
        int[] ringCounts={1,2,3,4,5,6,9,10};
        float[] radii={210f,430f,690f,980f,1300f,1640f,2020f,2440f};
        float sectorAngle=360f/prefixes.Length;
        for(int branch=0;branch<prefixes.Length;branch++)
        {
            float centerAngle=90f-branch*sectorAngle;
            string prefix=prefixes[branch];
            string[] ids=BranchIds(prefix,40);
            int idIndex=0;
            var previousRing=new List<string>();
            for(int ring=0;ring<ringCounts.Length;ring++)
            {
                int count=ringCounts[ring];
                var currentRing=new List<string>(count);
                float step=sectorAngle/count;
                for(int index=0;index<count;index++)
                {
                    string id=ids[idIndex++];
                    string parent;
                    if(ring==0)parent="core";
                    else
                    {
                        int parentIndex=Mathf.Min(previousRing.Count-1,
                            Mathf.FloorToInt(index*(float)previousRing.Count/count));
                        parent=previousRing[parentIndex];
                    }
                    float offset=(index-(count-1)*.5f)*step;
                    result.Add(new Node(id,parent,titles[branch],
                        PolarAnchor(centerAngle+offset,radii[ring]),ring==0));
                    currentRing.Add(id);
                }
                previousRing=currentRing;
            }
        }
        return result.ToArray();
    }

    static string[] BranchIds(string prefix,int count)
    {
        var ids=new string[count];
        ids[0]=prefix;
        for(int i=1;i<count;i++)ids[i]=prefix+"_"+i;

        // Keep the IDs used by spell unlocking and old saved games inside the expanded tree.
        if(prefix=="magic")
        {
            ids[1]="fire";
            ids[2]="ice";
            for(int i=3;i<count;i++)ids[i]="magic_"+(i-2);
        }
        if(prefix=="athletics")
            ids[count-1]="speed";
        return ids;
    }

    static Vector2 PolarAnchor(float angleDegrees,float radius)
    {
        float angle=angleDegrees*Mathf.Deg2Rad;
        return new Vector2(.5f+Mathf.Cos(angle)*radius/TreeCanvasSize,.5f+Mathf.Sin(angle)*radius/TreeCanvasSize);
    }
    void Update()
    {
        if(tree==null){ var p=GameObject.FindWithTag("Player"); if(p!=null){stats=p.GetComponent<PlayerStats>();tree=p.GetComponent<SkillTreeProgress>()??p.AddComponent<SkillTreeProgress>();tree.OnChanged+=Refresh;} }
    }
    public void Toggle(){ if(panel==null)Build();bool open=!panel.activeSelf;panel.SetActive(open);if(open){GameManager.Instance?.SetState(GameState.InMenu);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Refresh();}else{GameManager.Instance?.SetState(GameState.Exploration);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;} }
    void Build()
    {
        GameObject canvasGo=new GameObject("SkillTreeCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=3000;CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        panel=new GameObject("SkillTreePanel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvasGo.transform,false);var pr=panel.GetComponent<RectTransform>();pr.anchorMin=pr.anchorMax=new Vector2(.5f,.5f);pr.sizeDelta=new Vector2(1540,920);Image panelImage=panel.GetComponent<Image>();panelImage.sprite=Resources.Load<Sprite>("UI/SharpUI/Panel");panelImage.type=Image.Type.Sliced;panelImage.color=Color.white;
        GameObject viewportObject=new GameObject("TreeViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(SkillTreeDragHandler));viewportObject.transform.SetParent(panel.transform,false);
        RectTransform viewport=viewportObject.GetComponent<RectTransform>();viewport.anchorMin=viewport.anchorMax=new Vector2(.5f,.5f);viewport.sizeDelta=new Vector2(1000,760);viewport.anchoredPosition=new Vector2(0,-28);
        Image viewportImage=viewportObject.GetComponent<Image>();viewportImage.sprite=Resources.Load<Sprite>("UI/SharpUI/Panel");viewportImage.type=Image.Type.Sliced;viewportImage.color=new Color(.12f,.10f,.08f,.96f);
        GameObject content=new GameObject("TreeContent",typeof(RectTransform));content.transform.SetParent(viewport,false);treeContent=content.GetComponent<RectTransform>();treeContent.anchorMin=treeContent.anchorMax=new Vector2(.5f,.5f);treeContent.sizeDelta=Vector2.one*TreeCanvasSize;treeContent.anchoredPosition=Vector2.zero;
        RawImage art=new GameObject("SharpUI_SkillTreeBackdrop",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();art.transform.SetParent(treeContent,false);art.rectTransform.anchorMin=Vector2.zero;art.rectTransform.anchorMax=Vector2.one;art.rectTransform.offsetMin=new Vector2(18,18);art.rectTransform.offsetMax=new Vector2(-18,-18);art.texture=Resources.Load<Texture2D>("UI/SharpUI/SkillTreeBackdrop");art.color=art.texture!=null?new Color(1f,1f,1f,.16f):new Color(.03f,.03f,.03f);art.raycastTarget=true;SkillTreeDragHandler drag=viewportObject.GetComponent<SkillTreeDragHandler>();drag.content=treeContent;drag.viewport=viewport;
        pointsLabel=Label(panel.transform,"DISPONIBLES  0   •   ARRASTRA PARA EXPLORAR   •   RUEDA: ZOOM   •   K PARA CERRAR",new Vector2(0,393),new Vector2(860,30),17);pointsLabel.color=new Color(1,.78f,.25f);
        BuildSideCard(new Vector2(-635f,205f),"OFENSIVA","Ataque físico, velocidad y dominio de armas.");
        BuildSideCard(new Vector2(-635f,-75f),"MAGIA","Fuego, hielo, rayo y poder de hechizos.");
        BuildSideCard(new Vector2(635f,205f),"DEFENSA","Armadura y resistencias elementales.");
        BuildSideCard(new Vector2(635f,-75f),"UTILIDAD","Movimiento, recolección y especializaciones.");
        iconCatalog=Resources.Load<SkillIconCatalog>("UI/SkillIconCatalog");
        foreach(Node node in nodeData) CreateConnection(node);
        foreach(Node node in nodeData) CreateNode(node);
        BuildTooltip();
        SharpUIWindowChrome.Attach(panel,"ÁRBOL DE HABILIDADES",Toggle);
        panel.SetActive(false);
    }
    void BuildSideCard(Vector2 position,string title,string description)
    {
        GameObject card=new GameObject("SkillCard_"+title,typeof(RectTransform),typeof(Image));card.transform.SetParent(panel.transform,false);
        RectTransform rect=card.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(250,220);rect.anchoredPosition=position;
        Image image=card.GetComponent<Image>();image.sprite=Resources.Load<Sprite>("UI/SharpUI/Tooltip");image.type=Image.Type.Sliced;image.color=Color.white;
        TextMeshProUGUI heading=Label(card.transform,title,new Vector2(0,78),new Vector2(250,34),20);heading.color=new Color(1f,.74f,.34f);heading.fontStyle=FontStyles.Bold;
        TextMeshProUGUI body=Label(card.transform,description,new Vector2(0,4),new Vector2(208,92),14);body.color=new Color(.82f,.78f,.69f);body.textWrappingMode=TextWrappingModes.Normal;
        TextMeshProUGUI hint=Label(card.transform,"Pasa el cursor sobre un nodo para ver sus beneficios.",new Vector2(0,-72),new Vector2(208,48),12);hint.color=new Color(.55f,.72f,.48f);hint.textWrappingMode=TextWrappingModes.Normal;
    }
    void CreateNode(Node node)
    {
        GameObject go=new GameObject("SkillNode_"+node.id,typeof(RectTransform),typeof(Image),typeof(Button),typeof(CircularNodeRaycast),typeof(SkillNodeHover));go.transform.SetParent(treeContent,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=node.pos;float diameter=node.id=="core"?96f:node.major?72f:48f;rect.sizeDelta=Vector2.one*diameter;var image=go.GetComponent<Image>();image.sprite=Resources.Load<Sprite>("UI/SharpUI/RoundButton")??NodeGlow.Sprite;image.type=Image.Type.Sliced;image.color=Color.white;Button button=go.GetComponent<Button>();string parentId=node.parent;button.onClick.AddListener(()=>{if(tree!=null&&tree.TryUpgrade(node.id,parentId))Refresh();});buttons[node.id]=button;
        SkillNodeHover hover=go.GetComponent<SkillNodeHover>();hover.owner=this;hover.nodeId=node.id;
        GameObject iconObject=new GameObject("SkillIcon",typeof(RectTransform),typeof(Image));iconObject.transform.SetParent(go.transform,false);
        RectTransform iconRect=iconObject.GetComponent<RectTransform>();iconRect.anchorMin=new Vector2(.18f,.18f);iconRect.anchorMax=new Vector2(.82f,.82f);iconRect.offsetMin=iconRect.offsetMax=Vector2.zero;
        Image icon=iconObject.GetComponent<Image>();icon.sprite=iconCatalog!=null?iconCatalog.ForNode(node.id):null;icon.preserveAspect=true;icon.raycastTarget=false;nodeIcons[node.id]=icon;
        TextMeshProUGUI label=Label(go.transform,"0",new Vector2(diameter*.28f,-diameter*.28f),new Vector2(30,24),node.major?15:12);label.name="Value";label.fontStyle=FontStyles.Bold;label.color=new Color(1f,.91f,.46f);label.outlineWidth=.18f;
    }

    void CreateConnection(Node node)
    {
        string parentId=node.parent;
        if(string.IsNullOrEmpty(parentId))return;
        Node? parent=null;
        foreach(Node candidate in nodeData)if(candidate.id==parentId){parent=candidate;break;}
        if(!parent.HasValue)return;
        Vector2 from=new Vector2((parent.Value.pos.x-.5f)*treeContent.sizeDelta.x,(parent.Value.pos.y-.5f)*treeContent.sizeDelta.y);
        Vector2 to=new Vector2((node.pos.x-.5f)*treeContent.sizeDelta.x,(node.pos.y-.5f)*treeContent.sizeDelta.y);
        Vector2 delta=to-from;
        GameObject lineObject=new GameObject("Path_"+parentId+"_to_"+node.id,typeof(RectTransform),typeof(Image));lineObject.transform.SetParent(treeContent,false);
        RectTransform line=lineObject.GetComponent<RectTransform>();line.anchorMin=line.anchorMax=new Vector2(.5f,.5f);line.pivot=new Vector2(0f,.5f);line.anchoredPosition=from;line.sizeDelta=new Vector2(delta.magnitude,node.major?7f:4f);line.localRotation=Quaternion.Euler(0f,0f,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        Image path=lineObject.GetComponent<Image>();path.color=new Color(.34f,.22f,.10f,.72f);path.raycastTarget=false;connections[node.id]=path;
    }

    void BuildTooltip()
    {
        tooltip=new GameObject("SkillNodeTooltip",typeof(RectTransform),typeof(Image));tooltip.transform.SetParent(panel.transform,false);
        RectTransform rect=tooltip.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(300,150);rect.anchoredPosition=new Vector2(585f,-310f);
        tooltip.GetComponent<Image>().color=new Color(.008f,.014f,.026f,.98f);
        GameObject frame=new GameObject("SharpUI_Frame",typeof(RectTransform),typeof(Image));frame.transform.SetParent(tooltip.transform,false);RectTransform fr=frame.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;Image fi=frame.GetComponent<Image>();fi.sprite=Resources.Load<Sprite>("UI/SharpUI/Tooltip");fi.type=Image.Type.Sliced;fi.color=Color.white;fi.raycastTarget=false;
        tooltipTitle=Label(tooltip.transform,"Nodo",new Vector2(0,43),new Vector2(260,30),18);tooltipTitle.fontStyle=FontStyles.Bold;tooltipTitle.color=new Color(1f,.78f,.26f);
        tooltipDescription=Label(tooltip.transform,"Bonificación",new Vector2(0,-18),new Vector2(260,76),13);tooltipDescription.textWrappingMode=TextWrappingModes.Normal;tooltipDescription.color=new Color(.84f,.88f,.94f);
        tooltip.SetActive(false);
    }

    public void ShowNodeTooltip(string id, Vector2 screenPosition)
    {
        if(tooltip==null)return;
        GetNodeInfo(id,out string title,out string description);
        tooltipTitle.text=title;tooltipDescription.text=description;
        tooltip.transform.SetAsLastSibling();tooltip.SetActive(true);
    }

    public void HideNodeTooltip(){if(tooltip!=null)tooltip.SetActive(false);}

    static void GetNodeInfo(string id,out string title,out string description)
    {
        string rank=NodeRank(id);
        if(id=="core"){title="Núcleo del héroe";description="+1 de defensa por punto. Es el centro de las trece especializaciones.";return;}
        if(id=="fire"){title="Bola de fuego";description="Desbloquea Bola de Fuego y otorga +3.5% de poder mágico por punto.";return;}
        if(id=="ice"){title="Magia de hielo";description="Desbloquea Nova de Hielo y otorga +3.5% de poder mágico por punto.";return;}
        if(id=="magic_1"){title="Magia de rayo";description="Desbloquea Relámpago y otorga +3.5% de poder mágico por punto.";return;}
        if(id.StartsWith("magic")){title="Dominio arcano "+rank;description="+3.5% de poder mágico por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("sword")){title="Dominio de espadas "+rank;description="+2% de ataque físico por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("dragon")){title="Cazadragones "+rank;description="+2% de ataque físico por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("athletics")||id=="speed"){title="Atletismo "+rank;description="+1% de velocidad de movimiento por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("gather")){title="Fortuna del explorador "+rank;description="+1% de oro obtenido de enemigos por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("vitality")){title="Vitalidad "+rank;description="+1% de estamina máxima por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("fire_res")){title="Resistencia al fuego "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("ice_res")||id.StartsWith("ice_")){title="Resistencia al hielo "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("lightning")){title="Resistencia al rayo "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("wind")){title="Resistencia al viento "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("resist")){title="Resistencia oscura "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";return;}
        if(id.StartsWith("all_resist")){title="Resistencia elemental "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";return;}
        title="Defensa "+rank;description="+1 de defensa por punto. Máximo 10 puntos.";
    }

    static string NodeRank(string id)
    {
        int split=id.LastIndexOf('_');
        return split>=0&&int.TryParse(id.Substring(split+1),out int number)
            ?"· "+number
            :"· principal";
    }
    void Refresh()
    {
        if(tree==null)return;
        if(pointsLabel!=null)pointsLabel.text="DISPONIBLES  "+stats.AvailableSkillPoints+"   •   ARRASTRA PARA EXPLORAR   •   RUEDA: ZOOM   •   K PARA CERRAR";
        foreach(Node n in nodeData)
        {
            if(!buttons.TryGetValue(n.id,out Button b))continue;
            int level=tree.GetLevel(n.id);
            string parentId=n.parent;
            bool access=tree.CanAccess(parentId);
            b.interactable=access&&level<10;
            Image i=b.GetComponent<Image>();
            Sprite sharpNode=Resources.Load<Sprite>(level>0?"UI/SharpUI/RoundButtonSelected":"UI/SharpUI/RoundButton");
            if(sharpNode!=null)i.sprite=sharpNode;
            i.color=Color.white;
            if(nodeIcons.TryGetValue(n.id,out Image icon))icon.color=level>0?Color.white:new Color(.48f,.52f,.58f,.72f);
            var value=b.GetComponentInChildren<TextMeshProUGUI>();
            if(value!=null){value.text=level.ToString();value.color=new Color(1f,.91f,.46f);}
            if(connections.TryGetValue(n.id,out Image line))
            {
                bool active=level>0||(!string.IsNullOrEmpty(parentId)&&tree.GetLevel(parentId)>=5);
                line.color=active?new Color(1f,.57f,.16f,.95f):new Color(.34f,.22f,.10f,.72f);
            }
        }
    }
    static TextMeshProUGUI Label(Transform parent,string value,Vector2 pos,Vector2 size,float font){var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();t.text=value;t.fontSize=font;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.raycastTarget=false;var r=t.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return t;}
}

public class SkillNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillTreeUI owner;
    public string nodeId;
    public void OnPointerEnter(PointerEventData eventData)=>owner?.ShowNodeTooltip(nodeId,eventData.position);
    public void OnPointerExit(PointerEventData eventData)=>owner?.HideNodeTooltip();
}

public class SkillTreeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    const float MinZoom = .18f;
    const float MaxZoom = 1.5f;
    const float ZoomStep = 1.17f;
    public RectTransform content;
    public RectTransform viewport;
    public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData) { }
    public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if(content==null||viewport==null)return;
        content.anchoredPosition+=eventData.delta;
        ClampPosition();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if(content==null||viewport==null||Mathf.Approximately(eventData.scrollDelta.y,0f))return;

        float oldZoom=content.localScale.x;
        float wheelSteps=Mathf.Clamp(eventData.scrollDelta.y,-3f,3f);
        float newZoom=Mathf.Clamp(oldZoom*Mathf.Pow(ZoomStep,wheelSteps),MinZoom,MaxZoom);
        if(Mathf.Approximately(oldZoom,newZoom))return;

        Camera eventCamera=eventData.pressEventCamera;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
               viewport,eventData.position,eventCamera,out Vector2 cursorLocal))
            cursorLocal=Vector2.zero;

        // Keep the node beneath the cursor fixed while zooming so the large tree
        // behaves like a navigable map instead of jumping toward its centre.
        Vector2 contentPoint=(cursorLocal-content.anchoredPosition)/oldZoom;
        content.localScale=Vector3.one*newZoom;
        content.anchoredPosition=cursorLocal-contentPoint*newZoom;
        ClampPosition();
        eventData.Use();
    }

    void ClampPosition()
    {
        if(content==null||viewport==null)return;
        float zoom=Mathf.Max(.001f,content.localScale.x);
        float limitX=Mathf.Max(0f,(content.rect.width*zoom-viewport.rect.width)*.5f);
        float limitY=Mathf.Max(0f,(content.rect.height*zoom-viewport.rect.height)*.5f);
        Vector2 position=content.anchoredPosition;
        position.x=Mathf.Clamp(position.x,-limitX,limitX);
        position.y=Mathf.Clamp(position.y,-limitY,limitY);
        content.anchoredPosition=position;
    }
}

static class NodeGlow
{
    static Sprite sprite;
    public static Sprite Sprite
    {
        get
        {
            if (sprite != null) return sprite;
            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = Vector2.one * (size - 1) * .5f;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float d=Vector2.Distance(new Vector2(x,y),center)/(size*.5f);
                // Illuminate the full disc uniformly, then feather only its final rim. This
                // follows the complete node medallion instead of brightening just its number.
                float alpha=1f-Mathf.SmoothStep(.76f,1f,d);
                texture.SetPixel(x,y,new Color(1f,1f,1f,alpha));
            }
            texture.Apply(); sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f); return sprite;
        }
    }
}

public class CircularNodeRaycast : MonoBehaviour, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out Vector2 local)) return false;
        return local.sqrMagnitude <= Mathf.Pow(Mathf.Min(rect.rect.width, rect.rect.height) * .5f, 2f);
    }
}
