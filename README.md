# Un-Credibles: estado, organización y nomenclatura

Proyecto de Unity `6000.3.16f1`. Este documento resume el estado del repositorio y define cómo organizar las carpetas, escenas, scripts y recursos para mantener un criterio común entre los minijuegos.

## 1. Minijuegos elegidos

1. Ayuda a cruzar a la abuela la calle.
2. Calma al niño en el agua.
3. Cortar el césped del jardín.
4. Ratones en el garaje.
5. Rescatar al gato.

Los títulos visibles pueden estar en español. Los nombres técnicos se escriben en inglés, sin espacios ni tildes. «En el repositorio» indica que existe una escena y un `MinigameData` registrados en `01_Core`; no sustituye una prueba de juego completa.

| Minijuego | Nombre técnico | Prefijo | Estado |
|---|---|---|---|
| Abuela | `CrossyRoad` | `CR` | En el repositorio |
| Niño en el agua | `Churro` | `CH` | En el repositorio |
| Césped | `MowTheLawn` | `MW` | En el repositorio |
| Ratones | `GarageMice` | `GM` | Pendiente |
| Gato | `CatRescue` | `CT` | Pendiente |

Los nombres técnicos de Ratones y Gato son propuestas para su implementación.

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
│   └── MG_MowTheLawn/
├── UI/
│   ├── Garage/
│   ├── MainMenu/
│   ├── Overlays/
│   ├── PartyLobby/
│   ├── Results/
│   └── Settings/
├── Audio/
├── Shared/
├── Networking/
└── Tests/
```

Las carpetas `MG_GarageMice` y `MG_CatRescue` se crearán cuando comience su desarrollo. La integración de BatPad está dentro de `Networking/BatPad`.

| Carpeta | Responsabilidad |
|---|---|
| `Core` | Arranque, escenas, configuración y gestión de la partida completa. |
| `Player` | Jugadores, slots, controles y adaptadores de entrada. |
| `Characters` | Recursos de personajes compartidos. |
| `Minigames/_Framework` | Sistemas comunes de tiempo, puntuación, estados y resultados. |
| `Minigames/_Template` | Plantilla para crear nuevos minijuegos. |
| `Minigames/MG_*` | Recursos y comportamiento exclusivos de cada minijuego. |
| `UI` | Garaje, menú principal, lobby, resultados, ajustes y avisos superpuestos. |
| `Audio` | Música y efectos compartidos. |
| `Shared` | Materiales, prefabs y otros recursos compartidos. |
| `Networking` | Conexiones, servicios de red e integración de móviles como mandos mediante BatPad. |
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

Las escenas generales son:

```text
00_Boot
01_Core
02_MainMenu
04_Results
```

El menú y el lobby son dos vistas del garaje en `02_MainMenu`. La escena antigua `03_PartyLobby` sigue en el proyecto, pero está desactivada en la lista de compilación. `04_Results` muestra la clasificación entre rondas y el podio al terminar la partida.

Las escenas de minijuegos presentes siguen el formato `MG_<NombreTecnico>`:

```text
MG_CrossyRoad
MG_Churro
MG_MowTheLawn
```

El nombre debe coincidir exactamente con el campo `SceneName` del asset `MinigameData`. Cada escena nueva debe estar habilitada en el perfil de compilación y registrada en `MinigameManager`, dentro de `01_Core`.

## 5. Nomenclatura de scripts

Los archivos y clases utilizan **PascalCase**. El archivo de un componente de Unity debe llamarse igual que su clase principal.

Ejemplos:

```text
MowTheLawnController.cs
MowerPlayer.cs
MowTheLawnSettings.cs
MowerAIBrain.cs
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
| Clase | PascalCase | `MowerPlayer` |
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
UnCredibles.Minigames.MowTheLawn
```

Su ensamblado sigue el mismo nombre. Por ejemplo:

```text
UnCredibles.Minigames.MowTheLawn.asmdef
```

Los namespaces y ensamblados de `GarageMice` y `CatRescue` se definirán al implementarlos.

Se conserva la convención actual de jugadores: carpeta `Player`, ensamblado `UnCredibles.Player` y namespace `UnCredibles.Players`.

Los minijuegos utilizan sistemas compartidos del framework y evitan depender directamente unos de otros.

## 7. Nomenclatura de recursos

Para los recursos nuevos se propone:

| Recurso | Ejemplo |
|---|---|
| Prefab | `MW_Mower.prefab` |
| Datos del minijuego | `MG_MowTheLawn_Data.asset` |
| Ajustes | `MW_Settings.asset` |
| Material | `MW_Grass_M.mat` |
| Textura nueva | `TEX_MW_Grass_BaseColor.png` |
| Sprite nuevo | `SPR_MW_Icon.png` |
| Efecto de sonido nuevo | `SFX_MW_CutGrass.wav` |
| Música nueva | `MUS_MW_Main.ogg` |
| Animación nueva | `ANIM_MW_Mower_Run.anim` |
| Animator Controller nuevo | `AC_MW_Mower.controller` |

Los recursos compartidos pueden usar `Shared`, por ejemplo `MAT_Shared_Player`.

Los nombres existentes se conservan hasta acordar una reorganización.

### Identificadores internos

Los IDs actuales de los minijuegos utilizan minúsculas; pueden incluir guiones bajos:

```text
crossy_road
churro
mowthelawn
```

Estos son los IDs actuales de los tres `MinigameData` registrados. Cada ID debe ser único y permanecer estable aunque cambie el título visible. Los IDs de los minijuegos pendientes se decidirán al crearlos.

## 8. Organización de la interfaz

La interfaz general se divide por pantalla:

```text
UI/
├── Garage/
├── MainMenu/
├── Overlays/
├── PartyLobby/
├── Results/
└── Settings/
```

`Garage` coordina las vistas del menú, lobby, ajustes, créditos y galería dentro de `02_MainMenu`. `Results` contiene la clasificación entre rondas y el podio final. Los elementos exclusivos de un minijuego permanecen dentro de su carpeta.

Para objetos nuevos de interfaz se propone:

```text
Canvas_MowTheLawn
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
