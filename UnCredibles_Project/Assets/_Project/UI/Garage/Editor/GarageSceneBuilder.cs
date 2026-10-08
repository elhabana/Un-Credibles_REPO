#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnCredibles.Core;
using UnCredibles.UI.MainMenu;
using UnCredibles.UI.PartyLobby;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage.Editor
{
    // Explicit authoring command; never runs automatically on import.
    public static class GarageSceneBuilder
    {
        private static Transform environment;
        private static Material concrete, metal, sofa, accent;
        private static readonly Color Ink = new Color(.06f, .08f, .12f, .88f);
        [MenuItem("UnCredibles/Garage/Build prototype scene")]
        public static void Build()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new System.InvalidOperationException("Save open scenes before rebuilding the garage prototype.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Core loader").AddComponent<CoreSceneLoader>();
            environment = new GameObject("Garage - provisional geometry").transform;
            concrete = Material("Concrete", new Color(.26f, .3f, .34f));
            metal = Material("Metal", new Color(.12f, .16f, .22f));
            sofa = Material("Sofa", new Color(.34f, .18f, .12f));
            accent = Material("Accent", new Color(.92f, .57f, .15f));
            Box("Floor", new Vector3(0,-.2f,0), new Vector3(15,.4f,15), concrete);
            Box("North wall", new Vector3(0,2.5f,7), new Vector3(14,5,.3f), concrete);
            Box("West wall", new Vector3(-7,2.5f,0), new Vector3(.3f,5,14), concrete);
            Box("East wall", new Vector3(7,2.5f,0), new Vector3(.3f,5,14), concrete);
            Box("Garage door", new Vector3(-6.8f,2,0), new Vector3(.1f,3.8f,10), metal);
            for(int i=0;i<7;i++) Box("Door slat",new Vector3(-6.72f,.4f+i*.5f,0),new Vector3(.05f,.04f,10),concrete);
            Box("Sofa seat",new Vector3(-2.8f,.55f,5),new Vector3(4,1,1.5f),sofa);
            Box("Sofa back",new Vector3(-2.8f,1.2f,5.65f),new Vector3(4,1.5f,.4f),sofa);
            Box("TV stand",new Vector3(3,.65f,5),new Vector3(2,1.3f,1),metal);
            Box("TV",new Vector3(3,1.9f,5),new Vector3(2,1.3f,.4f),accent);
            Box("TV screen",new Vector3(3,1.9f,4.77f),new Vector3(1.7f,1,.04f),metal);
            Label("UN-CREDIBLES",new Vector3(0,3.5f,6.79f),Quaternion.identity,8f);
            Box("Credits door",new Vector3(6.78f,1.6f,0),new Vector3(.15f,3.2f,1.8f),sofa);
            for(int i=0;i<3;i++) Box("Step",new Vector3(6.1f+i*.2f,.12f+i*.15f,0),new Vector3(.6f,.24f+i*.3f,2),metal);
            Box("Gallery board",new Vector3(6.78f,2,-4),new Vector3(.15f,2.5f,3.4f),sofa);
            for(int i=0;i<4;i++) Box("Concept placeholder",new Vector3(6.65f,1.5f+i%2, -4.8f+i/2*1.6f),new Vector3(.05f,.75f,1),accent);
            var light = new GameObject("Garage light").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.7f; light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientLight=new Color(.45f,.45f,.5f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag="MainCamera"; camera.fieldOfView=60; camera.nearClipPlane=.1f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.09f,.13f);
            camera.gameObject.AddComponent<AudioListener>();
            var rig=new GameObject("Garage camera viewpoints").AddComponent<GarageCameraRig>();
            rig.output=camera;
            rig.viewpoints=new [] { Pose("Home",new Vector3(0,2,-1),new Vector3(0,2,6)),
                Pose("Lobby",new Vector3(1,2.3f,0),new Vector3(-6,1.4f,0)),
                Pose("Settings - TV",new Vector3(2,2,1.5f),new Vector3(3,1.9f,5)),
                Pose("Credits - door",new Vector3(2,2,-.4f),new Vector3(7,1.7f,0)),
                Pose("Gallery - board",new Vector3(2.5f,2,-4),new Vector3(7,2,-4)) };
            foreach(var pose in rig.viewpoints) pose.SetParent(rig.transform);
            rig.Show(GarageView.Home,true);
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var canvas=new GameObject("Garage UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            var front=new GameObject("Garage frontend").AddComponent<GarageFrontend>(); front.cameras=rig;
            var menuRoot=Root("Main menu",canvas.transform); front.menuRoot=menuRoot;
            var menu=menuRoot.AddComponent<MainMenuController>(); front.menu=menu;
            var home=Root("Home options",menuRoot.transform);
            Text(home.transform,"UN-CREDIBLES",new Vector2(-650,350),new Vector2(500,70),48);
            var single=Button(home.transform,"JUGAR LOCAL",new Vector2(-650,220));
            var multi=Button(home.transform,"MULTIPLAYER",new Vector2(-650,120));
            var settings=Button(home.transform,"AJUSTES",new Vector2(-650,20));
            Button(home.transform,"CREDITOS",new Vector2(-650,-80),front.ShowCredits);
            Button(home.transform,"GALERIA",new Vector2(-650,-180),front.ShowGallery);
            Text(home.transform,"GARAJE / PROTOTIPO",new Vector2(650,-450),new Vector2(450,50),22);
            var setup=Root("Online setup",menuRoot.transform);
            Panel(setup.transform,new Vector2(-600,0),new Vector2(600,650));
            Text(setup.transform,"MULTIPLAYER",new Vector2(-600,240),new Vector2(500,70),40);
            var create=Button(setup.transform,"CREAR SALA",new Vector2(-600,130));
            var fieldGo=new GameObject("Room code",typeof(RectTransform),typeof(Image),typeof(TMP_InputField));
            fieldGo.transform.SetParent(setup.transform,false); Rect(fieldGo,new Vector2(-600,20),new Vector2(430,65));
            fieldGo.GetComponent<Image>().color=Ink;
            var input=fieldGo.GetComponent<TMP_InputField>();
            var inputText=Text(fieldGo.transform,"",Vector2.zero,new Vector2(400,65),30);
            input.textViewport=inputText.rectTransform; input.textComponent=inputText;
            input.characterLimit=12; input.contentType=TMP_InputField.ContentType.Alphanumeric;
            var join=Button(setup.transform,"UNIRSE",new Vector2(-600,-80));
            var back=Button(setup.transform,"VOLVER",new Vector2(-600,-230));
            var connectionStatus=Text(setup.transform,"",new Vector2(-600,-155),new Vector2(540,65),20);
            Text(setup.transform,"CODIGO DE SALA",new Vector2(-600,68),new Vector2(430,30),18);
            Assign(menu,"singleplayerButton",single,"multiplayerButton",multi,"settingsButton",settings,
                "mainPanel",home,"multiplayerSetupPanel",setup,"createRoomButton",create,"joinRoomButton",join,
                "backButton",back,"roomCodeInput",input,"connectionStatus",connectionStatus);
            var lobbyRoot=Root("Lobby",canvas.transform); front.lobbyRoot=lobbyRoot;
            var controller=lobbyRoot.AddComponent<PartyLobbyController>(); var view=lobbyRoot.AddComponent<PartyLobbyView>();
            var mode=Text(lobbyRoot.transform,"LOBBY",new Vector2(0,460),new Vector2(1100,60),34);
            var hint=Text(lobbyRoot.transform,"",new Vector2(0,-470),new Vector2(1600,60),24);
            var countdown=Text(lobbyRoot.transform,"",new Vector2(0,220),new Vector2(600,100),70);
            var lobbyBack=Button(lobbyRoot.transform,"SALIR DE LA SALA",new Vector2(-680,460));
            var cards=new LobbySlotView[4]; var avatars=new GameObject[4];
            for(int i=0;i<4;i++)
            {
                var card=Panel(lobbyRoot.transform,new Vector2(-600+i*400,-280),new Vector2(370,260));
                card.name="Player "+(i+1); cards[i]=card.AddComponent<LobbySlotView>();
                var name=Text(card.transform,"",new Vector2(0,85),new Vector2(350,40),28);
                var source=Text(card.transform,"",new Vector2(0,45),new Vector2(350,35),20);
                var status=Text(card.transform,"",new Vector2(0,5),new Vector2(350,40),20);
                var crown=Text(card.transform,"HOST",new Vector2(125,110),new Vector2(100,25),16);
                var ai=Button(card.transform,"+ BOT",new Vector2(0,-75)); Rect(ai.gameObject,new Vector2(0,-75),new Vector2(280,55));
                var remove=Button(card.transform,"QUITAR",new Vector2(0,-75)); Rect(remove.gameObject,new Vector2(0,-75),new Vector2(280,55));
                var invite=Button(card.transform,"",Vector2.zero); invite.gameObject.SetActive(false);
                Assign(cards[i],"background",card.GetComponent<Image>(),"crown",crown.gameObject,"nameText",name,
                    "inputText",source,"statusText",status,"addAIButton",ai,"removeButton",remove,"inviteButton",invite);
                avatars[i]=Avatar(i,new Vector3(-5.8f,0,-3.75f+i*2.5f));
            }
            var qr=new GameObject("Phone QR",typeof(RectTransform),typeof(RawImage)); qr.transform.SetParent(lobbyRoot.transform,false);
            Rect(qr,new Vector2(780,230),new Vector2(170,170));
            var qrText=Text(lobbyRoot.transform,"",new Vector2(780,110),new Vector2(300,70),20);
            Assign(view,"controller",controller,"modeText",mode,"hintText",hint,"countdownText",countdown,
                "backButton",lobbyBack,"batPadQr",qr.GetComponent<RawImage>(),"batPadText",qrText);
            var serialized=new SerializedObject(view); var slots=serialized.FindProperty("slotViews"); slots.arraySize=4;
            for(int i=0;i<4;i++) slots.GetArrayElementAtIndex(i).objectReferenceValue=cards[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var players=lobbyRoot.AddComponent<GaragePlayers>(); players.lobby=controller; players.avatars=avatars;
            front.detailPanels=new GameObject[3];
            for(int i=0;i<3;i++)
            {
                var detail=Root(new[]{"Settings","Credits","Gallery"}[i],canvas.transform); front.detailPanels[i]=detail;
                Panel(detail.transform,new Vector2(-600,0),new Vector2(620,800));
                Text(detail.transform,new[]{"AJUSTES","CREDITOS","GALERIA"}[i],new Vector2(-600,320),new Vector2(550,65),42);
                Button(detail.transform,"VOLVER",new Vector2(-600,-320),front.ShowHome);
                if(i==0)
                {
                    front.masterVolume=Volume(detail.transform,"GENERAL",180);
                    front.musicVolume=Volume(detail.transform,"MUSICA",40);
                    front.sfxVolume=Volume(detail.transform,"EFECTOS",-100);
                }
                else Text(detail.transform,i==1?"Espacio reservado para los creditos del equipo.":"Espacio reservado para concepts, modelos y proceso de arte.",
                    new Vector2(-600,30),new Vector2(490,300),30);
                detail.SetActive(false);
            }
            lobbyRoot.SetActive(false); setup.SetActive(false);
            foreach(var avatar in avatars) avatar.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/_Project/Core/Scenes/02_MainMenu.unity");
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Select(s=>new EditorBuildSettingsScene(s.path,s.enabled && !s.path.EndsWith("03_PartyLobby.unity"))).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("GARAGE_BUILD_OK: shared scene, five viewpoints, local/online lobby and volume settings.");
        }

        private static GameObject Root(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform; r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero;
            return go;
        }
        private static void Rect(GameObject go,Vector2 position,Vector2 size)
        {
            var r=(RectTransform)go.transform; r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=position; r.sizeDelta=size;
        }
        private static GameObject Panel(Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject("Panel",typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            Rect(go,pos,size); go.GetComponent<Image>().color=Ink; return go;
        }
        private static TMP_Text Text(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize)
        {
            var go=new GameObject("Text - "+value,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false); Rect(go,pos,size);
            var text=go.GetComponent<TextMeshProUGUI>(); text.text=value; text.font=TMP_Settings.defaultFontAsset;
            text.fontSize=fontSize; text.alignment=TextAlignmentOptions.Center; text.color=Color.white; text.raycastTarget=false; return text;
        }
        private static Button Button(Transform parent,string label,Vector2 pos,UnityAction action=null)
        {
            var go=Panel(parent,pos,new Vector2(430,75)); go.name=label; var button=go.AddComponent<Button>();
            button.targetGraphic=go.GetComponent<Image>(); Text(go.transform,label,Vector2.zero,new Vector2(420,70),28);
            if(action!=null) UnityEventTools.AddPersistentListener(button.onClick,action); return button;
        }
        private static Slider Volume(Transform parent,string label,float y)
        {
            Text(parent,label,new Vector2(-600,y+40),new Vector2(480,40),25);
            var track=Panel(parent,new Vector2(-600,y),new Vector2(430,28)); var slider=track.AddComponent<Slider>();
            var handle=Panel(track.transform,Vector2.zero,new Vector2(32,42)); handle.GetComponent<Image>().color=new Color(.95f,.65f,.2f);
            slider.handleRect=(RectTransform)handle.transform; slider.targetGraphic=handle.GetComponent<Image>(); slider.value=1; return slider;
        }
        private static Transform Pose(string name,Vector3 pos,Vector3 look)
        {
            var t=new GameObject(name).transform; t.position=pos; t.rotation=Quaternion.LookRotation(look-pos); return t;
        }
        private static Material Material(string name,Color color)
        {
            const string folder="Assets/_Project/UI/Garage/Materials";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/UI/Garage","Materials");
            string path=folder+"/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.color=color; EditorUtility.SetDirty(mat); return mat;
        }
        private static GameObject Box(string name,Vector3 pos,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(environment);
            go.transform.position=pos; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; return go;
        }
        private static void Label(string value,Vector3 pos,Quaternion rotation,float width)
        {
            var text=new GameObject(value).AddComponent<TextMeshPro>(); text.transform.SetParent(environment); text.transform.SetPositionAndRotation(pos,rotation);
            text.text=value; text.font=TMP_Settings.defaultFontAsset; text.fontSize=7; text.alignment=TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta=new Vector2(width,1); text.color=new Color(.95f,.65f,.2f);
        }
        private static GameObject Avatar(int slot,Vector3 pos)
        {
            var root=new GameObject("Player "+(slot+1)+" - accessory anchors"); root.transform.SetParent(environment); root.transform.position=pos;
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.transform.SetParent(root.transform,false);
            body.transform.localPosition=new Vector3(0,1,0); body.transform.localScale=new Vector3(.7f,1,.7f);
            body.GetComponent<Renderer>().sharedMaterial=slot%2==0?accent:sofa;
            var head=GameObject.CreatePrimitive(PrimitiveType.Sphere); head.transform.SetParent(root.transform,false);
            head.transform.localPosition=new Vector3(0,2.2f,0); head.transform.localScale=Vector3.one*.65f;
            head.GetComponent<Renderer>().sharedMaterial=concrete;
            var mask=new GameObject("Mask anchor").transform; mask.SetParent(root.transform,false); mask.localPosition=new Vector3(.35f,2.2f,0);
            var clothes=new GameObject("Clothing anchor").transform; clothes.SetParent(root.transform,false); clothes.localPosition=Vector3.up;
            return root;
        }
        private static void Assign(Object target,params object[] pairs)
        {
            var serialized=new SerializedObject(target);
            for(int i=0;i<pairs.Length;i+=2) serialized.FindProperty((string)pairs[i]).objectReferenceValue=(Object)pairs[i+1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
