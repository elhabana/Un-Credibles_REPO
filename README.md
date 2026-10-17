# Un-Credibles: estado, organización y nomenclatura

Proyecto de Unity `6000.3.16f1`. Este documento resume el estado del repositorio y define cómo organizar las carpetas, escenas, scripts y recursos para mantener un criterio común entre los minijuegos.

## 1. Minijuegos elegidos

1. Ayuda a cruzar a la abuela la calle.
2. Calma al niño en el agua.
3. Cortar el césped del jardín.
4. Ratones en el garaje.
5. Rescatar al gato.

Los títulos visibles pueden estar en español. Los nombres técnicos se escriben en inglés, sin espacios ni tildes. «En el repositorio» indica que existe una escena y un `MinigameData` registrados en `SCN_Core`; no sustituye una prueba de juego completa.

| Minijuego | Nombre técnico | Prefijo | Estado |
|---|---|---|---|
| Abuela | `CrossyRoad` | `CR` | En el repositorio |
| Niño en el agua | `Churro` | `CH` | En el repositorio |
| Césped | `MowTheLawn` | `MW` | En el repositorio |
| Ratones | `GarageMice` | `GM` | Prototipo en el repositorio |
| Gato | `CatRescue` | `CT` | Pendiente |

El nombre técnico de Gato es una propuesta para su implementación.

Al comenzar una partida desde el lobby compartido, se abre la puerta del garaje y todos los jugadores salen corriendo hacia una luz intensa que oculta el exterior. Esta salida sucede una sola vez por partida, antes del primer minijuego.

En el prototipo de `GarageMice`, la cámara mira desde arriba con una ligera inclinación para mostrar la profundidad del garaje ampliado. Las estanterías bloquean el paso. Los jugadores golpean con una escoba usando la acción `Jump` (`Espacio` o `A`, también disponible en BatPad). Cada ratón eliminado suma un punto; golpear a otro jugador lo aturde brevemente, con estrellas y un pequeño temblor de cámara. Gana quien haya eliminado más ratones al terminar los 90 segundos.

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
│   ├── MG_MowTheLawn/
│   └── MG_GarageMice/
├── UI/
│   ├── Garage/
│   ├── MainMenu/
│   ├── Overlays/
│   ├── PartyLobby/
│   ├── Results/
│   ├── Settings/
│   └── Voting/
├── Audio/
├── Shared/
├── Networking/
└── Tests/
```

La carpeta `MG_CatRescue` se creará cuando comience su desarrollo. La integración de BatPad está dentro de `Networking/BatPad`.

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

Todas las escenas llevan el prefijo `SCN_`, sin numeración. El orden de carga lo define el perfil de compilación, no el nombre.

Las escenas generales, en `Core/Scenes`, son:

```text
SCN_Boot
SCN_Core
SCN_MainMenu
SCN_Results
```

El menú y el lobby son dos vistas del garaje en `SCN_MainMenu`. `SCN_Results` muestra la clasificación entre rondas y el podio al terminar la partida. Sus nombres se usan desde código a través de las constantes de `GameScenes`.

Las escenas de minijuegos siguen el formato `SCN_MG_<NombreTecnico>`:

```text
SCN_MG_CrossyRoad
SCN_MG_Churro
SCN_MG_MowTheLawn
SCN_MG_GarageMice
```

`SCN_MG_Template` (plantilla, fuera de la compilación) y `SCN_NET_Test` (prueba manual de Relay, desactivada en la compilación) completan la lista.

El nombre debe coincidir exactamente con el campo `SceneName` del asset `MinigameData`, porque las escenas se cargan por nombre. Cada escena nueva debe estar habilitada en el perfil de compilación y registrada en `MinigameManager`, dentro de `SCN_Core`.

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
UnCredibles.Minigames.GarageMice
```

Su ensamblado sigue el mismo nombre. Por ejemplo:

```text
UnCredibles.Minigames.MowTheLawn.asmdef
```

El namespace y ensamblado de `CatRescue` se definirán al implementarlo.

Se conserva la convención actual de jugadores: carpeta `Player`, ensamblado `UnCredibles.Player` y namespace `UnCredibles.Players`.

Los minijuegos utilizan sistemas compartidos del framework y evitan depender directamente unos de otros.

## 7. Nomenclatura de recursos

Formato general: `PREFIJO_Ambito_Nombre_Variante`.

- `PREFIJO`: tipo de recurso, según la tabla.
- `Ambito`: prefijo del minijuego (`CR`, `CH`, `MW`, `GM`, `CT`), `Shared` para recursos compartidos o el área de interfaz (`Garage`…). Se mantiene siempre: evita choques como `M_CH_Player` frente a `M_CR_Player`.
- `Nombre` y `Variante` en PascalCase. No se añaden números si no hay variantes reales, ni sufijos que repitan el tipo (`_M`, `_Mat`).

| Tipo | Prefijo | Ejemplo |
|---|---|---|
| Escena | `SCN_` | `SCN_MG_MowTheLawn.unity` |
| Prefab | `PF_` | `PF_MW_Mower.prefab` |
| Static Mesh | `SM_` | `SM_MW_Mower.fbx` |
| Skeletal Mesh | `SK_` | `SK_Shared_Hero.fbx` |
| Textura | `T_` | `T_MW_Grass_BaseColor.png` |
| Material | `M_` | `M_MW_Grass.mat` |
| Animation Clip | `AN_` | `AN_MW_Mower_Run.anim` |
| Animator Controller | `AC_` | `AC_MW_Mower.controller` |
| Avatar Mask | `AV_` | `AV_Shared_UpperBody.mask` |
| ScriptableObject | `SO_` | `SO_MW_Settings.asset`, `SO_MG_MowTheLawn_Data.asset` |
| Sprite | `SPR_` | `SPR_MW_Icon.png` |
| Sprite Atlas | `ATLAS_` | `ATLAS_UI_Garage.spriteatlas` |
| Fuente | `FONT_` | `FONT_Shared_Title.ttf` |
| Efecto de sonido | `SFX_` | `SFX_MW_CutGrass.wav` |
| Música | `MUS_` | `MUS_MW_Main.ogg` |
| Audio Mixer | `MIXER_` | `MIXER_Main.mixer` |
| Shader Graph | `SH_` | `SH_Shared_Toon.shadergraph` |
| Shader programado | `SHADER_` | `SHADER_MW_GrassBlade.shader` |
| Efecto visual | `FX_` | `FX_GM_Poof.prefab` |
| Physic Material | `PM_` | `PM_CR_Oil.physicMaterial` |
| Cubemap | `CUBEMAP_` | `CUBEMAP_Shared_Sky.cubemap` |
| Render Texture | `RT_` | `RT_UI_Podium.renderTexture` |
| Timeline | `TIMELINE_` | `TIMELINE_Garage_Intro.playable` |
| Preset | `PRESET_` | `PRESET_Shared_TextureUI.preset` |

No llevan prefijo: los scripts C# (el archivo se llama como su clase), los `.asmdef`, los `.inputactions` y los recursos de configuración del motor en `Assets/Settings`. Los recursos de `Assets/ThirdParty` y de los paquetes conservan sus nombres originales.

Los IDs de la lista de animaciones del equipo (`ANI_PJ_…`) son identificadores de producción; al importar cada clip, su archivo se nombra con `AN_`.

### Identificadores internos

Los IDs actuales de los minijuegos utilizan minúsculas; pueden incluir guiones bajos:

```text
crossy_road
churro
mowthelawn
garage_mice
```

Estos son los IDs actuales de los cuatro `MinigameData` registrados. Cada ID debe ser único y permanecer estable aunque cambie el título visible. El ID del minijuego pendiente se decidirá al crearlo.

## 8. Organización de la interfaz

La interfaz general se divide por pantalla:

```text
UI/
├── Garage/
├── MainMenu/
├── Overlays/
├── PartyLobby/
├── Results/
├── Settings/
└── Voting/
```

`Garage` coordina las vistas del menú, lobby, ajustes, créditos y galería dentro de `SCN_MainMenu`. `Results` contiene la clasificación entre rondas y el podio final. Los elementos exclusivos de un minijuego permanecen dentro de su carpeta.

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
2. Renombrar carpeta, escena (`SCN_MG_<NombreTecnico>`), scripts, namespace y ensamblado.
3. Crear su `MinigameData` (`SO_MG_<NombreTecnico>_Data`) con ID único, reglas y número de jugadores.
4. Implementar el controlador heredando de `MinigameController`.
5. Leer los controles mediante `IPlayerInput`.
6. Utilizar los sistemas compartidos de puntuación, tiempo y aparición cuando corresponda.
7. Finalizar mediante `EndGame`.
8. Registrar sus datos en `MinigameManager`, dentro de `SCN_Core`.
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
