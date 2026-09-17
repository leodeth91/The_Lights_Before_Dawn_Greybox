using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Cambia solamente las etiquetas de los objetos. Los identificadores del juego siguen en inglés.</summary>
public static class SpanishHierarchySetup
{
    private const string BackupPath = "Backups/SpanishHierarchy-20260914";
    private static readonly Dictionary<string, string> Names = BuildNames();


    private static Dictionary<string, string> BuildNames()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        const string pairs = @"Geometry_ProBuilder=Geometria_ProBuilder
STATIC_GREYBOX_FURNITURE=MOBILIARIO_GREYBOX
ROOM_MODULE_POOL=POOL_HABITACIONES
AlienSearchPoints=PuntosBusqueda_Alien
Room=Habitacion
ROOM=HABITACION
ROOM_INSTANCE=INSTANCIA_HABITACION
HIDESPOT=ESCONDITE
Floor=Piso
Ceiling=Techo
Wall=Pared
North=Norte
South=Sur
East=Este
West=Oeste
Left=Izquierda
Right=Derecha
Top=Superior
Bottom=Inferior
UpperLanding=DescansoSuperior
Safety=Seguridad
StairSafetyDivider=SeparadorSeguridadEscalera
Stairs=Escalera
StairStep=Escalon
StairAccess=AccesoEscalera
Window=Ventana
BedFrame=EstructuraCama
BedLeg=PataCama
Headboard=Cabecera
Mattress=Colchon
Nightstand=MesaDeLuz
BedEndBench=BancoPieDeCama
DeskChair=SillaEscritorio
OfficeChair=SillaOficina
DeskLeg=PataEscritorio
DeskTop=SuperficieEscritorio
DiningChair=SillaComedor
DiningTop=SuperficieMesaComedor
TableLeg=PataMesa
WardrobeBack=FondoArmario
WardrobeBase=BaseArmario
WardrobeLeft=LateralIzquierdoArmario
WardrobeRight=LateralDerechoArmario
WardrobeTop=ParteSuperiorArmario
CabinetBack=FondoMueble
CabinetLeft=LateralIzquierdoMueble
CabinetRight=LateralDerechoMueble
CabinetTop=ParteSuperiorMueble
ChestBack=FondoBaul
ChestBase=BaseBaul
ChestFront=FrenteBaul
ChestLeft=LateralIzquierdoBaul
ChestRight=LateralDerechoBaul
ChestLid=TapaBaul
BasketBack=FondoCanasto
BasketBase=BaseCanasto
BasketFront=FrenteCanasto
BasketLeft=LateralIzquierdoCanasto
BasketRight=LateralDerechoCanasto
DirtyLaundry=RopaSucia
CurtainLeft=CortinaIzquierda
CurtainRight=CortinaDerecha
CurtainRod=BarralCortina
BathCurtain=CortinaBanera
ShowerCurtain=CortinaDucha
ShowerBack=FondoDucha
ShowerLip=BordeDucha
ShowerTray=BaseDucha
TubBack=FondoBanera
TubBase=BaseBanera
TubFront=FrenteBanera
TubLeft=LateralIzquierdoBanera
TubRight=LateralDerechoBanera
Door=Puerta
DoorLeaf=HojaPuerta
DoorPivot=PivotePuerta
Hinge=Bisagra
HingePivot=PivoteBisagra
LidHingePivot=PivoteTapa
Frame=Marco
Header=Dintel
PortalAssembly=ConjuntoPuerta
ThresholdSensor=SensorUmbral
EntryPoint=PuntoEntrada
ExitPoint=PuntoSalida
HiddenPoint=PuntoOculto
Socket=ConexionPuerta
Search=Busqueda
Hide=Escondite
Center=Centro
Corner=Esquina
NW=NO
SW=SO
Arm=Apoyabrazos
Back=Respaldo
Body=Cuerpo
Head=Cabeza
Eye=Ojo
Visual_Body=Visual_Cuerpo
Visual_Head=Visual_Cabeza
Leg=Pata
Seat=Asiento
Side=Lateral
Spine=SoporteCentral
Shelf=Estante
Surface=Superficie
Handle=Manija
Hooks=Ganchos
Bookcase=Biblioteca
CentralDoubleSided=CentralDobleCara
NorthLeft=NorteIzquierda
NorthRight=NorteDerecha
SouthLeft=SurIzquierda
SouthRight=SurDerecha
Refrigerator=Heladera
FreezerDivision=DivisionFreezer
TallCabinet=AlacenaAlta
BaseCabinet=MuebleBajo
SideCabinet=MuebleAuxiliar
Counter=Mesada
SinkCounter=MesadaConPileta
StoveAndOven=CocinaYHorno
OvenFront=FrenteHorno
Burner=Hornalla
Tap=Canilla
TVConsole=MuebleTV
TVStand=SoporteTV
Television=Televisor
FoyerArmchair=SillonRecibidor
FoyerConsole=ConsolaRecibidor
CoatRack=Perchero
Toilet=Inodoro
Tank=Deposito
Bowl=Taza
Washbasin=Lavamanos
DoubleVanity=VanitoryDoble
BasinBottom=FondoPileta
BasinRim=BordePileta
BasinSide=LateralPileta
WasherDoor=PuertaLavarropas
WashingMachine=Lavarropas
RoomLight=LuzHabitacion
MoonDirectionalLight=LuzLuna
ENVIRONMENT=ENTORNO
GAME_SYSTEMS=SISTEMAS_JUEGO
ConnectionText=TextoConexion
InteractionText=TextoInteraccion
RoomText=TextoHabitacion
TimerText=TextoTiempo
StartHint=AyudaInicio
StartPanel=PanelInicio
Title=Titulo
ChildBed=CamaNino
ChildWardrobe=ArmarioNino
ChildCurtain=CortinaNino
ChildDesk=EscritorioNino
ParentsBed=CamaPadres
ParentsCurtain=CortinaPadres
MotherWardrobe=ArmarioMadre
FatherWardrobe=ArmarioPadre
Bathtub=Banera
UpperBathCurtain=CortinaBanoSuperior
LaundryBasket=CanastoRopa
LibraryCurtain=CortinaBiblioteca
OfficeNorthCurtain=CortinaOficinaNorte
OfficeEastCurtain=CortinaOficinaOeste
OfficeDesk=EscritorioOficina
OfficeWardrobe=ArmarioOficina
CorridorCurtain=CortinaPasillo
LivingWestCurtain=CortinaSalaOeste
LivingNorthLeftCurtain=CortinaSalaNorteIzquierda
LivingNorthRightCurtain=CortinaSalaNorteDerecha
DiningTable=MesaComedor
DecorativeChest=BaulSala
LowCabinet=MuebleBajo
KitchenCurtain=CortinaCocina
Shower=Ducha
LowerHallCurtain=CortinaHallInferior
LowerLaundryBasket=CanastoRopaInferior";
        foreach (string line in pairs.Split('\n'))
        {
            string[] pair = line.Trim().Split('=');
            result.Add(pair[0], pair[1]);
        }
        result["Post"] = "Poste";
        result["ResultPanel"] = "PanelResultado";
        result["ResultText"] = "TextoResultado";
        string[] roomIds = { "child_room", "parents_bedroom", "office", "library", "kitchen", "living_dining_room", "foyer", "lower_bathroom", "upper_bathroom", "stair_hall", "upper_corridor" };
        string[] roomLabels = { "Nino", "Padres", "Oficina", "Biblioteca", "Cocina", "SalaComedor", "Recibidor", "BanoInferior", "BanoSuperior", "HallEscalera", "PasilloSuperior" };
        for (int i = 0; i < roomIds.Length; i++)
            foreach (string prefix in new[] { "Room_", "ROOM_", "ROOM_INSTANCE_", "HABITACION_INSTANCE_" })
                result[prefix + roomIds[i]] = "Habitacion_" + roomLabels[i];
        return result;
    }

    public static string Translate(string name)
    {
        if (Names.TryGetValue(name, out string translated)) return translated;
        // Conservamos los códigos de habitación y de conexión: son datos usados por la anomalía.
        return string.Join("/", name.Split('/').Select(part => Names.TryGetValue(part, out string label) ? label :
            string.Join("_", part.Split('_').Select(word => Names.TryGetValue(word, out string value) ? value : word))));
    }

    public static Transform Find(Transform parent, string name)
    {
        return parent == null ? null : parent.Find(name) ?? parent.Find(Translate(name));
    }

    [MenuItem("The Lights Before Dawn/Organization/Translate object names to Spanish")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play antes de cambiar nombres.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Volvé a Scenes antes de ejecutar la traducción para conservar la edición del prefab.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/ModularPrototype.unity") throw new InvalidOperationException("Abrí ModularPrototype para traducir sus objetos.");
        Directory.CreateDirectory(BackupPath);
        // Primero guardamos la distribución actual, incluida la silla movida por el usuario.
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la escena actual.");
        string backupScene = BackupPath + "/ModularPrototype.unity";
        if (!File.Exists(backupScene)) File.Copy(scene.path, backupScene);
        Transform[] sceneObjects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        string snapshot = string.Join("\n", sceneObjects.Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t) + " | " + PathOf(t)
            + " | position=" + t.localPosition.ToString("F6") + " | rotation=" + t.localRotation.ToString("F6")
            + " | scale=" + t.localScale.ToString("F6")));
        if (!File.Exists(BackupPath + "/Manual-layout.txt")) File.WriteAllText(BackupPath + "/Manual-layout.txt", snapshot);
        int count = 0;
        // La escena y los prefabs se editan por separado: nunca se aplican posiciones de la escena al prefab.
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Generated/ModularPrototype/Rooms", "Assets/Aliens" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        foreach (string path in prefabPaths)
        {
            string backup = BackupPath + "/" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { count += Rename(root.GetComponentsInChildren<Transform>(true)); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        count += Rename(sceneObjects);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/SpanishHierarchyResult.txt", "OK: " + count + " nombres traducidos; distribución y componentes conservados. Respaldo: " + BackupPath);
    }

    private static int Rename(Transform[] objects)
    {
        int count = 0;
        foreach (Transform item in objects)
        {
            string translated = Translate(item.name);
            if (translated == item.name) continue;
            Vector3 position = item.localPosition, scale = item.localScale;
            Quaternion rotation = item.localRotation;
            Undo.RecordObject(item.gameObject, "Traducir nombre");
            item.name = translated;
            if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item.gameObject);
            if (position != item.localPosition || rotation != item.localRotation || scale != item.localScale)
                throw new InvalidOperationException("Cambió una posición al traducir " + item.name);
            count++;
        }
        return count;
    }

    private static string PathOf(Transform item) => item.parent == null ? item.name : PathOf(item.parent) + "/" + item.name;
}
