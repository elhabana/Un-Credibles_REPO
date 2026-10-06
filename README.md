# Un-Credibles: organización y nomenclatura

Este documento define cómo organizar las carpetas, escenas, scripts y recursos del proyecto para mantener un criterio común entre todos los minijuegos.

## 1. Minijuegos elegidos

1. Ayuda a cruzar a la abuela la calle.
2. Calma al niño en el agua.
3. Cortar el césped del jardín.
4. Ratones en el garaje.
5. Rescatar al gato.

Los títulos visibles pueden estar en español. Los nombres técnicos se escriben en inglés, sin espacios ni tildes.

| Minijuego | Nombre técnico | Prefijo |
|---|---|---|
| Abuela | `CrossyRoad` | `CR` |
| Niño en el agua | `Churro` — correspondencia por confirmar | `CH` |
| Césped | `LawnMower` | `LM` |
| Ratones | `GarageMice` | `GM` |
| Gato | `CatRescue` | `CT` |

Los nombres técnicos de Césped, Ratones y Gato son propuestas para su implementación.

## 2. Organización de carpetas

El contenido propio se organiza dentro de `Assets/_Project`.

```text
_Project/
├── Core/
├── Player/
├── Characters/
├── Minigames/
│   ├── _Framework/
│   ├── _Template/
│   ├── MG_CrossyRoad/
│   ├── MG_Churro/
│   ├── MG_LawnMower/
│   ├── MG_GarageMice/
│   └── MG_CatRescue/
├── UI/
├── Audio/
├── Shared/
├── Networking/
├── BatPad/
└── Tests/
```

Las carpetas de los tres últimos minijuegos se crearán cuando comience su desarrollo.

| Carpeta | Responsabilidad |
|---|---|
| `Core` | Arranque, escenas, configuración y gestión de la partida completa. |
| `Player` | Jugadores, slots, controles y adaptadores de entrada. |
| `Characters` | Recursos de personajes compartidos. |
| `Minigames/_Framework` | Sistemas comunes de tiempo, puntuación, estados y resultados. |
| `Minigames/_Template` | Plantilla para crear nuevos minijuegos. |
| `Minigames/MG_*` | Recursos y comportamiento exclusivos de cada minijuego. |
| `UI` | Menú principal, lobby, resultados y ajustes. |
| `Audio` | Música y efectos compartidos. |
| `Shared` | Materiales, prefabs y otros recursos compartidos. |
| `Networking` | Conexiones y servicios de red. |
| `BatPad` | Integración de móviles como mandos. |
| `Tests` | Pruebas automatizadas. |

Los recursos externos se mantienen en `Assets/ThirdParty` o `Assets/Plugins`, según corresponda.

**Regla de ubicación:** un recurso exclusivo pertenece a su minijuego; si lo utilizan varios, se coloca en la carpeta compartida correspondiente.

## 3. Estructura de cada minijuego

Todos los minijuegos utilizan la misma organización:

```text
MG_Nombre/
├── Art/
├── Audio/
├── Prefabs/
├── Scenes/
├── ScriptableObjects/
└── Scripts/
```

- `Art`: modelos, texturas, materiales y animaciones.
- `Audio`: música y sonidos exclusivos.
- `Prefabs`: jugadores, obstáculos y objetivos reutilizables.
- `Scenes`: escenas del minijuego.
- `ScriptableObjects`: datos, reglas y parámetros configurables.
- `Scripts`: lógica específica del minijuego.

## 4. Nomenclatura de escenas

Las escenas generales conservan los nombres actuales:

```text
00_Boot
01_Core
02_MainMenu
03_PartyLobby
04_Results
```

Las escenas de minijuegos siguen el formato `MG_<NombreTecnico>`:

```text
MG_CrossyRoad
MG_Churro
MG_LawnMower
MG_GarageMice
MG_CatRescue
```

El nombre debe coincidir exactamente con el campo `SceneName` del asset `MinigameData`.

## 5. Nomenclatura de scripts

Los archivos y clases utilizan **PascalCase**. El archivo de un componente de Unity debe llamarse igual que su clase principal.

Ejemplos:

```text
LawnMowerController.cs
LawnMowerPlayer.cs
LawnMowerSettings.cs
LawnMowerAIBrain.cs
```

| Sufijo | Función |
|---|---|
| `Controller` | Coordinar las reglas del minijuego o de una pantalla. |
| `Player` | Gestionar el comportamiento del jugador en ese minijuego. |
| `Settings` | Definir parámetros ajustables. |
| `AIBrain` | Decidir las acciones de un bot. |
| `View` | Mostrar información y actualizar la interfaz. |
| `Manager` | Gestionar un sistema compartido con una responsabilidad concreta. |

Evitar nombres ambiguos como `Script1`, `PruebaFinal` o `Manager2`.

### Convenciones de código

| Elemento | Convención | Ejemplo |
|---|---|---|
| Clase | PascalCase | `LawnMowerPlayer` |
| Método | PascalCase | `StartGame` |
| Propiedad | PascalCase | `CurrentScore` |
| Campo privado | camelCase | `moveSpeed` |
| Variable o parámetro | camelCase | `playerId` |
| Interfaz | Prefijo `I` | `IPlayerInput` |
| Constante | PascalCase | `MaxPlayers` |
| Booleano | Expresar una condición | `isReady`, `canJump` |

Los campos configurables desde Unity se declaran preferiblemente privados con `[SerializeField]`.

## 6. Namespaces y ensamblados

Cada minijuego utiliza un namespace propio:

```text
UnCredibles.Minigames.CrossyRoad
UnCredibles.Minigames.Churro
UnCredibles.Minigames.LawnMower
UnCredibles.Minigames.GarageMice
UnCredibles.Minigames.CatRescue
```

Su ensamblado sigue el mismo nombre:

```text
UnCredibles.Minigames.LawnMower.asmdef
```

Se conserva la convención actual de jugadores: carpeta `Player`, ensamblado `UnCredibles.Player` y namespace `UnCredibles.Players`.

Los minijuegos utilizan sistemas compartidos del framework y evitan depender directamente unos de otros.

## 7. Nomenclatura de recursos

Para los recursos nuevos se propone:

| Recurso | Ejemplo |
|---|---|
| Prefab | `LM_Player.prefab` |
| Datos del minijuego | `MG_LawnMower_Data.asset` |
| Ajustes | `LM_Settings.asset` |
| Material | `MAT_LM_Grass.mat` |
| Textura | `TEX_LM_Grass_BaseColor.png` |
| Sprite | `SPR_LM_Icon.png` |
| Efecto de sonido | `SFX_LM_CutGrass.wav` |
| Música | `MUS_LM_Main.ogg` |
| Animación | `ANIM_LM_Player_Run.anim` |
| Animator Controller | `AC_LM_Player.controller` |

Los recursos compartidos pueden usar `Shared`, por ejemplo `MAT_Shared_Player`.

Los nombres existentes se conservan hasta acordar una reorganización.

### Identificadores internos

Los IDs de los minijuegos utilizan minúsculas y guiones bajos:

```text
crossy_road
churro
lawn_mower
garage_mice
cat_rescue
```

Cada ID es único y permanece estable aunque cambie el título visible.

## 8. Organización de la interfaz

La interfaz general se divide por pantalla:

```text
UI/
├── MainMenu/
├── PartyLobby/
├── Results/
└── Settings/
```

Los elementos exclusivos de un minijuego permanecen dentro de su carpeta.

Para objetos nuevos de interfaz se propone:

```text
Canvas_LawnMower
Panel_Instructions
Panel_Results
Button_Continue
Text_Score
Image_PlayerIcon
```

## 9. Incorporación de un nuevo minijuego

1. Duplicar `_Template` desde Unity.
2. Renombrar carpeta, escena, scripts, namespace y ensamblado.
3. Crear su `MinigameData` con ID único, reglas y número de jugadores.
4. Implementar el controlador heredando de `MinigameController`.
5. Leer los controles mediante `IPlayerInput`.
6. Utilizar los sistemas compartidos de puntuación, tiempo y aparición cuando corresponda.
7. Finalizar mediante `EndGame`.
8. Registrar sus datos en `MinigameManager`, dentro de `01_Core`.
9. Añadir la escena al perfil de compilación.
10. Probarlo de forma aislada y dentro de una partida completa.

## 10. Reglas de trabajo

- Crear, mover y renombrar recursos desde Unity para conservar sus referencias.
- Mantener cada recurso junto a su archivo `.meta`.
- Acordar quién modifica cada escena para reducir conflictos.
- Utilizar prefabs para elementos reutilizables.
- Guardar los parámetros ajustables en el Inspector o en assets de configuración.
- Mantener fuera del repositorio carpetas generadas como `Library`, `Temp`, `Logs` y `UserSettings`.
- Escribir mensajes de cambios concretos, como `Añade puntuación por césped cortado`.
- Actualizar este documento cuando cambie una convención.

Antes de integrar un minijuego, comprobar su inicio, controles, finalización, resultados y transición a la siguiente ronda.
