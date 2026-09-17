# The Lights Before Dawn: guía de las mecánicas y del código

Este informe explica el proyecto con palabras sencillas, para acompañar una presentación y comparar con los diagramas de flujo. Describe el código actual, incluida la anomalía que combina habitaciones de ambas plantas. No modifica el juego.

Los diagramas muestran las decisiones principales; no representan cada instrucción del programa. Los nombres de los scripts están en inglés porque así aparecen en el proyecto.

## Cómo leer las explicaciones

Un **script** es un archivo con instrucciones. Un **componente** es una parte del GameObject que se ocupa de una tarea: moverse, escuchar, mostrar un texto, etc.

En cada explicación se indican las herramientas de programación que realmente utiliza ese script:

- **Interfaz:** un acuerdo de lo que un objeto permite hacer. Por ejemplo, pedir «abrir» sin necesitar saber si es una puerta o un baúl.
- **Singleton:** un coordinador único al que otros scripts pueden consultar mediante `Instance`.
- **State:** separa el comportamiento en acciones, como patrullar, perseguir o buscar.
- **Strategy:** separa la manera de resolver una tarea. Permite cambiar cómo se elige un destino sin cambiar cómo se mueve la habitación.
- **Pool:** reutiliza objetos existentes, en lugar de crearlos y destruirlos continuamente.
- **Observer:** un script emite un aviso y otros scripts que están escuchando reaccionan. En este proyecto se implementa con eventos.
- **Encapsulamiento:** el script cuida sus propios datos y ofrece formas controladas de consultarlos o modificarlos.
- **Herencia:** una clase parte de otra y aprovecha lo que ya tiene, agregando o cambiando algunas tareas.
- **Polimorfismo:** una misma orden produce el comportamiento correspondiente en distintos tipos de objeto.
- **Composición:** un personaje reúne varios componentes especializados, en vez de concentrar todo en una sola clase.

Las estructuras de datos son distintas maneras de organizar información:

- `List`: una lista que puede crecer, recorrerse y perder elementos.
- `Queue`: una fila; sale primero lo que entró primero.
- `Dictionary`: relaciona una clave con un dato, como «código de habitación → habitación».
- `HashSet`: una colección sin repetidos, útil para saber quién ya está registrado.
- **Array**, escrito con `[]`: un conjunto de espacios de tamaño definido, útil para puntos del cuerpo, pivotes o resultados de física.

### Una aclaración que se aplica a muchos scripts

Los componentes del juego normalmente **heredan de `MonoBehaviour`**, la clase de Unity que permite recibir llamadas como `Update`, `OnEnable` y `OnTriggerEnter`. Esto es herencia de Unity; no significa que cada componente implemente un patrón State o Strategy.

También es común tener campos `private` con `[SerializeField]`: pueden ajustarse desde el Inspector, pero otros scripts no los modifican directamente. Las propiedades con `get` permiten consultar datos; `private set` deja el cambio en manos de la propia clase. Una propiedad calculada, como `IsHidden`, responde una pregunta a partir del estado actual.

Cuando una explicación no menciona una interfaz, un patrón o una colección, no se le atribuye uno solo para completar la lista.

## 1. Movimiento, cámara y agacharse

El jugador controla al niño con teclado y mouse. Puede caminar, correr, saltar y agacharse. Al agacharse cambia su tamaño y el volumen que utiliza para chocar con el entorno. Eso permite quedar detrás de un mueble, pero no lo vuelve invisible por sí solo.

```text
Entrada del teclado y mouse
          ↓
¿El movimiento está bloqueado por un escondite?
     ├─ Sí → No aplicar movimiento normal
     └─ No → Calcular dirección, velocidad y gravedad
                           ↓
                 ¿Quiere agacharse?
                 ├─ Sí → Reducir cuerpo y collider
                 └─ No → Comprobar espacio para levantarse
                           ↓
               Mover con CharacterController
                           ↓
               Actualizar cámara y puntos visibles
```

### Scripts y decisiones utilizadas

- **`ChildPlayerController.cs` — `Assets/Scripts/Player`.** Lee las acciones del **nuevo Input System** y mueve al niño con el **CharacterController de Unity**. También aplica gravedad y controla la postura. Usa **encapsulamiento**: guarda el bloqueo y la postura internamente, y permite consultarlos mediante propiedades como `IsCrouched` e `IsSprinting`. Su evento de cambio de postura permite **Observer**: otra parte puede reaccionar cuando el niño se agacha. Justificación: no mezclar el movimiento con la lógica de cada escondite ni con la visión del alien.

- **`OverShoulderCamera.cs` — `Assets/Scripts/Player`.** Sigue al niño y orienta la cámara; si hay un obstáculo, ajusta su distancia. Utiliza referencias a componentes y campos privados: **composición y encapsulamiento**. No necesita una interfaz ni un patrón de selección de objetivos. Justificación: la cámara puede ajustarse sin reescribir cómo camina el niño.

- **`PlayerVisibility.cs` — `Assets/Scripts/Player`.** Le informa al alien si el niño está disponible para ser visto y ofrece varios puntos del cuerpo para comprobarlo. Implementa **`IPerceptionTarget`**, el acuerdo de información de un objetivo detectable. Sus propiedades consultan el movimiento y el escondite: **encapsulamiento y composición**. Llena un **array** de puntos que recibe de la percepción; no crea una lista nueva en cada comprobación. Justificación: mirar varios puntos evita depender de un único rayo demasiado alto o demasiado bajo.

- **`IPerceptionTarget.cs` — `Assets/Scripts/Core`.** Es la **interfaz** que permite consultar el tipo de objetivo, su punto de referencia y si está disponible para detección. La cumplen `PlayerVisibility` y `HideSpot`. Justificación: compartir datos básicos, sin obligar a que el jugador y el escondite se detecten con exactamente las mismas reglas.

**Relación:** movimiento y escondite determinan la postura; `PlayerVisibility` informa el resultado; `AlienPerception` comprueba si un obstáculo realmente tapa al niño.

## 2. Interactuar con los objetos

Normalmente, el jugador se acerca, apunta con el centro de la cámara y pulsa E. El barrido tiene un pequeño grosor para que apuntar no exija tanta precisión. No debe permitir usar un objeto a través de una pared. El celular tiene una ayuda especial de proximidad, explicada en la sección de objetivos.

```text
Buscar lo que está frente a la cámara
                 ↓
¿Está cerca, disponible y sin un obstáculo delante?
     ├─ No → No ofrecer esa interacción
     └─ Sí → Mostrar nombre y acción de E
                            ↓
                       ¿Presiona E?
                       ├─ No → Seguir comprobando
                       └─ Sí → Pedir al objeto que actúe

Si ya está escondido → La acción de salir tiene prioridad
```

### Scripts y decisiones utilizadas

- **`PlayerInteractor.cs` — `Assets/Scripts/Interaction`.** Busca y elige la interacción, consulta si está permitida y muestra el texto correspondiente. Usa **`IInteractable`**: no necesita un código completamente distinto para cada puerta, escondite o teléfono. Esto permite **polimorfismo**: la misma orden de interactuar abre una puerta o revisa el celular según el objeto. Usa un **`HashSet<IInteractable>`** para no contar varias veces un objeto que tenga varios colliders, y un **array** reutilizado para consultas de cercanía. El SphereCast también devuelve un array de impactos; no todas las consultas del sistema evitan crear memoria nueva. Emite `PromptChanged`: **Observer**, para avisar a la interfaz cuando cambia el texto. `CurrentPrompt` usa **encapsulamiento**: otros lo consultan, pero su cambio lo maneja este script. Justificación: centralizar E y evitar que una puerta cercana se imponga a lo que se está apuntando.

- **`IInteractable.cs` — `Assets/Scripts/Core`.** Define el texto, la comprobación de disponibilidad y la acción de interacción. Es una **interfaz**, no un componente que se agrega al Inspector. Justificación: todos los objetos utilizables responden a las mismas preguntas, aunque hagan cosas diferentes.

**Relación:** `PlayerInteractor` encuentra un componente que cumple `IInteractable`; el objeto decide qué hace E; `PrototypeHUD` recibe el aviso del texto.

## 3. Esconderse y revisar escondites

Cada escondite indica por dónde entrar, dónde queda oculto el niño y por dónde salir. Algunos permiten entrar desde distintos lados. Si el niño ya está oculto, el alien no puede verlo a distancia aunque sobresalga parte de su collider. Eso no lo protege si el alien llega a revisar el escondite.

```text
Apuntar al escondite y pulsar E
                ↓
¿Se puede entrar y está libre?
     ├─ No → No iniciar la entrada
     └─ Sí → Bloquear movimiento normal
                        ↓
               Elegir recorrido de entrada
                        ↓
              Adaptar cuerpo y mover al niño
                        ↓
               Registrar que está oculto
                        ↓
                   ¿Presiona E?
                   └─ Sí → Salir, restaurar cuerpo y liberar lugar

Alien llega a inspeccionar
                ↓
Abrir tapa o puertas del mueble, si corresponde
                ↓
¿Está ocupado por el niño oculto?
     ├─ Sí → Pedir derrota
     └─ No → Terminar la revisión y continuar
```

### Scripts y decisiones utilizadas

- **`HideSpot.cs` — `Assets/Scripts/Interaction`.** Guarda puntos de entrada, ocultamiento y salida, distancias y ocupante. Implementa **`IInteractable`** para E, **`IInspectable`** para la revisión del alien y **`IPerceptionTarget`** para ofrecer datos perceptibles del escondite. Usa **encapsulamiento**: `IsOccupied` permite preguntar si está ocupado sin cambiar directamente el ocupante. Sus `UnityEvent` de entrada, salida y revisión permiten **Observer**. Justificación: la cama y el baúl pueden ofrecer las mismas operaciones, aunque tengan recorridos y formas diferentes.

- **`PlayerHidingController.cs` — `Assets/Scripts/Player`.** Organiza entrar, permanecer oculto y salir. Cambia el cuerpo y las colisiones temporalmente, y luego los restaura. Usa una **`List<Collider>`** para recordar qué colisiones ignoró y poder recuperarlas al salir. Mantiene un estado mediante un **enum** y propiedades con modificación controlada: **encapsulamiento**. Emite `StateChanged`: **Observer**. Utiliza **Strategy** al delegar el recorrido en `IHideTransitionStrategy`. Justificación: separar «estar escondido» de «cómo se mueve el cuerpo para entrar». Su enum de estados no es la misma implementación por clases de State que tienen los aliens.

- **`HideTransitionStrategy.cs` — `Assets/Scripts/Interaction`.** Contiene **`IHideTransitionStrategy`** y sus implementaciones: `UnderBedHideTransitionStrategy` desliza al niño; `ArcHideTransitionStrategy` describe un arco para superar un borde. Es **Strategy y polimorfismo**: el controlador pide una posición y una rotación sin conocer la fórmula elegida. `HideTransitionStrategyFactory` es una **fábrica simple** que devuelve la alternativa correspondiente. La altura del arco es privada y `readonly`: **encapsulamiento**, porque se establece al crear la estrategia. Justificación: cambiar el recorrido sin duplicar todo el código de ocultamiento.

- **`HingedHideFurniture.cs` — `Assets/Scripts/Interaction`.** Abre las puertas de armarios y las tapas de baúles girando sus pivotes. Implementa **`IOpenable`**. Usa **arrays** para relacionar cada pivote con sus ángulos abierto y cerrado. `IsOpen` tiene cambio privado: **encapsulamiento**. Justificación: un mismo componente puede manejar una o varias hojas, sin escribir otro script por cada armario.

- **`IOpenable.cs` e `IInspectable.cs` — `Assets/Scripts/Core`.** Son **interfaces** para pedir abrir/cerrar y comenzar/terminar una inspección. Justificación: el alien puede ordenar «abrir» y «revisar» sin saber cómo está construido cada mueble. Separar abrir de cerrar evita que repetir un intento de apertura termine cerrándolo.

**Relación:** `PlayerInteractor` solicita la entrada a `HideSpot`; `PlayerHidingController` realiza la transición; la estrategia calcula el recorrido. El alien consulta el mismo `HideSpot` mediante `IInspectable` y abre su mobiliario mediante `IOpenable` cuando corresponde.

## 4. Aliens: patrullar, observar y elegir qué revisar

Los dos aliens utilizan el prefab Alien y comienzan su actividad después de la espera inicial de tres segundos. Caminan, se detienen, mueven cabeza y cuerpo para mirar y reúnen cosas que les interesan. La elección depende de lo visible, lo alcanzable y la fase de actividad.

```text
Crear aliens desde el prefab → Esperar activación
                                   ↓
                               Deambular
                                   ↓
                       Detenerse y observar alrededor
                                   ↓
                    Vaciar y llenar la lista Visible
                                   ↓
              ¿Hay intereses visibles y alcanzables?
              ├─ No → Volver a deambular
              └─ Sí → Elegir categoría y objeto
                                   ↓
                     Acercarse, revisar o cruzar
                                   ↓
                    Finalizar → Volver a deambular

Ver al jugador tiene prioridad sobre este recorrido
```

### Scripts y decisiones utilizadas

- **`AlienPopulation.cs` — `Assets/Scripts/Aliens`.** Crea los personajes desde el prefab y prepara conexiones de navegación entre habitaciones. Usa **composición**: configura componentes del Alien, no genera una jerarquía alternativa por código como respaldo. Escucha aperturas y cierres de la casa: **Observer**. Su **`Dictionary<RoomConnection, NavMeshLink>`** relaciona cada paso con su enlace de navegación para poder retirarlo cuando se cierra. Ofrece una consulta de solo lectura: **encapsulamiento**. Justificación: que la conexión física y la ruta utilizable por el alien se mantengan coordinadas. Los aliens se instancian; el pool explicado más adelante es de habitaciones, no de aliens.

- **`AlienController.cs` — `Assets/Scripts/Aliens`.** Es el coordinador de cada alien: decide cuándo cambiar de acción y conecta movimiento, visión, memoria y voz. Usa **State**, ejecutando las clases de `AlienState`, y **composición**, conservando referencias a sus componentes. Guarda **`List<AlienInterest> Visible`**, que se limpia al comenzar una observación y se llena durante el barrido. Usa un **`Dictionary<AlienInterest, float>`** para recordar hasta cuándo conviene evitar repetir un interés. En los recorridos entre habitaciones usa **`Queue<RoomModule>`**, **`Dictionary`** y **`HashSet<DoorSocket>`**: explora rutas por etapas, recuerda la primera puerta útil y evita repetir puertas intentadas. Recibe ajustes de actividad y ofrece eventos: **Observer**. Tiene propiedades controladas para sus componentes y estado, pero `Visible` es una lista pública `readonly`: no se puede reemplazar la referencia, aunque sí modificar su contenido. Por eso no debe presentarse como encapsulamiento absoluto. Justificación: coordinar tareas sin concentrar todas sus instrucciones en una única función.

- **`AlienState.cs` — `Assets/Scripts/Aliens`.** Contiene la clase base **abstracta** `AlienState` y clases como `AlienWanderState`, `AlienScanState`, `AlienInspectState` y `AlienTraverseState`. Usa **herencia, polimorfismo y State**: todas ofrecen `Execute`, pero cada una realiza su acción. La observación limpia `Visible` y reúne intereses durante el movimiento de la mirada. Justificación: poder cambiar de acción sin una enorme cadena de condiciones. Estas clases de estado no son GameObjects ni componentes `MonoBehaviour` independientes.

- **`AlienPerception.cs` — `Assets/Scripts/Aliens`.** Comprueba alcance, campo visual y obstáculos. Recibe intereses mediante **`IAlienInterest`**, para consultar distintos objetos con el mismo acuerdo. Usa **arrays reutilizados** de impactos y puntos del jugador, y **listas** de categorías y candidatos para elegir qué revisar. Sus valores de visión son privados ajustables y tienen propiedades de consulta: **encapsulamiento**. Justificación: separar «puedo verlo» de «qué decido hacer». Al aumentar la actividad, los pesos favorecen puertas y escondites; no siempre tienen idéntica probabilidad.

- **`AlienHeadScanner.cs` — `Assets/Scripts/Aliens`.** Mueve la cabeza hacia un objetivo o barre el entorno. Usa referencias al controlador y al cuerpo: **composición y encapsulamiento** de sus ajustes. No mantiene otra lista de objetos ni otra máquina de decisiones. Justificación: que mirar sea una tarea separada de decidir qué inspeccionar.

- **`AlienInterest.cs` — `Assets/Scripts/Aliens`.** Define **`IAlienInterest`** y la clase base abstracta `AlienInterest`. Mantiene una **`List<AlienInterest>`** de intereses activos: se agregan al activarse y se retiran al desactivarse. La consulta `Active` es de solo lectura: **encapsulamiento**. Usa **herencia y polimorfismo**, dejando que cada derivado defina categoría, punto de mirada y acercamiento. Justificación: consultar un registro existente en vez de buscar todos los objetos de la escena a cada momento. Este registro no es un Singleton de GameObject.

- **`DoorInterest.cs`.** **Hereda de `AlienInterest`** y adapta una puerta: ofrece sus puntos y comprueba si está disponible para el recorrido normal del alien. Justificación: reutilizar el acuerdo común sin mezclar las reglas de puertas con las de camas.

- **`StairInterest.cs`.** **Hereda de `AlienInterest`** y representa el interés por la escalera. Justificación: incorporarla a la misma selección de intereses con sus propios datos de acercamiento.

- **`HideSpotInterest.cs`.** **Hereda de `AlienInterest`** y conecta la observación con un `HideSpot`. Usa **composición** al referenciar el escondite. Justificación: el escondite conserva su mecánica de ocupación; este componente lo presenta al sistema de intereses.

- **`InspectableObject.cs`.** **Hereda de `AlienInterest` e implementa `IInspectable`**. Permite presentar otros objetos como intereses revisables. Justificación: reutilizar la inspección sin obligar a que todos los objetos sean escondites. No implementa todavía tomar, llevar ni arrojar objetos.

Los cuatro scripts anteriores están en `Assets/Scripts/Aliens`. Sus datos particulares se consultan mediante propiedades y referencias a componentes; no requieren otra colección central de visibles.

## 5. Ver al jugador, perseguir y buscar su último rastro

El jugador **no se agrega a `Visible`**. Se comprueba por separado para darle prioridad inmediata. El alien no ve a través de muebles ni detecta al niño a sus espaldas. Si lo pierde de vista, utiliza lo que recuerda, no una consulta secreta de su posición actual.

```text
Comprobar al jugador por separado
                ↓
¿Está en el campo visual y sin obstáculos?
     ├─ No → Continuar la tarea o búsqueda actual
     └─ Sí → Guardar observación y dar prioridad
                         ↓
              Alerta de 0,9 a 1,15 segundos
              Seguirlo con la mirada y llamar
                         ↓
                       Perseguir
                         ↓
               ¿Puede alcanzar su cuerpo?
               ├─ Sí → Capturar → Derrota
               └─ No → ¿Sigue visible?
                        ├─ Sí → Actualizar persecución
                        └─ No → Ir al último rastro
                                      ↓
                          Buscar según dirección recordada
                                      ↓
                     Revisar lugares y una salida coherente
                                      ↓
                     ¿Lo encuentra? Sí → Perseguir
                                    No → Volver a patrullar

Si vio entrar al niño en un escondite
       → Recordar ese escondite → Revisarlo prioritariamente
```

### Scripts y decisiones utilizadas

- **`AlienPerception.cs`.** En esta mecánica comprueba varios puntos del cuerpo, la orientación del alien y los obstáculos. Sus **arrays** reducen la creación repetida de resultados de física. Consulta `PlayerVisibility`; ambos tienen tareas separadas: **composición**. Justificación: poder detectar al niño sobre una mesada o agachado si realmente está expuesto, sin detectar automáticamente lo que está detrás.

- **`AlienMemory.cs` — `Assets/Scripts/Aliens`.** Guarda la última habitación, posición, dirección y escondite observados. Usa **encapsulamiento**: conserva datos privados y ofrece propiedades como `LastKnownPosition` y `KnownHidingSpot`, junto con métodos de actualización. Guarda posición y dirección relativas a la habitación, para que acompañen sus movimientos. No necesita una lista ilimitada de todo el recorrido. Justificación: una memoria pequeña permite buscar de manera creíble sin saber siempre dónde está el jugador.

- **`AlienState.cs`.** Aquí intervienen `AlienAlertState`, `AlienChaseState`, `AlienSearchState`, `AlienInvestigateKnownHideSpotState` y `AlienCatchPlayerState`: **State, herencia y polimorfismo**. `AlienSearchState` usa una **`List<AlienSearchPoint>`**; retira cada candidato después de intentarlo y reúne otros si pasa a la habitación siguiente. Utiliza **`ISearchStrategy`** para elegir el siguiente punto: **Strategy**. Justificación: evitar revisar indefinidamente el mismo lugar y separar la búsqueda de la persecución.

- **`AlienSearchPoint.cs` — `Assets/Scripts/Aliens`.** Representa un sitio que se puede revisar y sus asociaciones con puerta o escondite. Permite marcar que fue visitado: **encapsulamiento**, porque la visita se registra mediante una operación definida. Usa **composición** con los objetos asociados. Justificación: darle a la búsqueda puntos con sentido, no solo coordenadas al azar.

- **`ISearchStrategy.cs` — `Assets/Scripts/Aliens`.** Define la **interfaz `ISearchStrategy`** e incluye `DirectionalSearchStrategy`. Usa **Strategy y polimorfismo** para favorecer puntos relacionados con el rumbo recordado y las visitas anteriores. Recibe una colección de solo lectura para elegir. Justificación: ajustar la lógica de elección sin reescribir las acciones de caminar o inspeccionar.

- **`AlienMotor.cs` — `Assets/Scripts/Aliens`.** Mueve al alien con **NavMeshAgent**, comprueba caminos y abandona intentos que no avanzan. Para un niño elevado intenta acercarse por suelo alcanzable alrededor del mueble. Usa **encapsulamiento** de la navegación y **composición** con el agente. También conserva datos locales para suspender y restaurar al alien cuando su habitación se traslada. Justificación: que las decisiones no tengan que resolver cada detalle del movimiento ni quedar esperando para siempre.

**Relación:** percepción observa → memoria recuerda → controlador cambia de estado → estrategia elige puntos → motor se acerca → escondite o sesión resuelve la revisión o captura. Un cruce que requiere seguridad puede terminar antes de cambiar de acción.

## 6. Actividad de los aliens según el tiempo

El tiempo de actividad cuenta desde que comienza la partida. Es distinto del contador visible de supervivencia, que comienza al revisar el celular.

```text
Consultar tiempo total de partida y objetivo
                     ↓
¿Ya encontró el celular o pasaron cinco minutos?
     ├─ Sí → Fase intensa
     └─ No → ¿Pasaron dos minutos?
              ├─ Sí → Fase activa
              └─ No → Fase cautelosa
                     ↓
              ¿Cambió la fase?
              ├─ No → Mantener ajustes
              └─ Sí → Avisar a los aliens
                               ↓
                  Aplicar ritmo a sus siguientes acciones
```

### Scripts y decisiones utilizadas

- **`AlienActivityDirector.cs` — `Assets/Scripts/Aliens`.** Consulta el reloj y el objetivo y decide la fase. Usa **Singleton de escena**, con `Instance` y protección contra duplicados, para tener un director compartido. Usa **Observer** mediante `ActivityChanged`: avisa al cambiar de fase, no obliga a cada alien a calcularla por separado. `Current` tiene modificación privada: **encapsulamiento**. Justificación: ambos aliens reciben criterios comunes, aunque tomen decisiones individuales.

- **`AlienActivityTuning`, definido en ese archivo.** Es una **estructura de datos `readonly struct`**, no un componente ni una lista. Agrupa pausas, multiplicadores de velocidad, pesos de intereses y espera para repetirlos. Sus propiedades de solo lectura muestran **encapsulamiento**. Justificación: enviar un paquete coherente de ajustes en lugar de muchos valores sueltos.

- **`AlienController.cs`.** Escucha el director: **Observer**. Guarda los ajustes y los utiliza en propiedades calculadas, por ejemplo velocidad base × multiplicador de actividad: **encapsulamiento**. No hace falta una lista de configuraciones antiguas. Justificación: cambiar el ritmo sin reemplazar la máquina de estados ni reiniciar continuamente las acciones que ya comenzaron.

**Importante:** State decide **qué hace** el alien; los ajustes de actividad deciden **con qué ritmo y preferencias lo hace**. La fase es un enum, no otra máquina State completa con una clase por fase.

## 7. Puertas y anomalía: cómo cambia la casa

La casa reutiliza habitaciones que ya existen. Al abrir una puerta, el sistema puede colocar detrás otra habitación y alinear sus marcos. Después de activar la anomalía, puede elegir habitaciones de planta alta o baja. Las escaleras y ciertos accesos siguen protegidos.

```text
Jugador o alien pide abrir
             ↓
¿Ya existe una conexión que puede utilizar?
     ├─ Sí → Usarla y abrir cuando sea seguro
     └─ No → Revisar pasos anteriores y solicitudes
                          ↓
                 ¿Está permitida la anomalía?
                 ├─ No → Probar destino normal
                 └─ Sí → Sortear cualquier destino seguro
                          ↓
               Reunir candidatos de ambas plantas
                          ↓
          Comprobar espacio, ocupantes y cruces en uso
                          ↓
               ¿Existe un destino seguro?
               ├─ No → Reintentar dentro del plazo
               └─ Sí → Elegir y colocar habitación
                                     ↓
                          Recolocar navegación y aliens
                                     ↓
                              Conectar marcos y abrir
                                     ↓
                              Personaje atraviesa
                                     ↓
                  Alien pide cerrar detrás de sí
                                     ↓
                  Cerrar y liberar cuando sea seguro
```

El primer intento de salir sigue llevando al cuarto del niño por la historia. Después, cada nueva conexión elegible sortea un destino seguro de ambas plantas, incluido el destino habitual. Cada partida genera una semilla nueva, que inicia una secuencia de elecciones distinta. Las solicitudes y habitaciones disponibles también influyen. Una conexión que todavía está en uso conserva su destino hasta que pueda liberarse con seguridad. En el Inspector se puede desactivar `Randomize Seed` para pruebas con semilla fija; `Randomize Every Eligible Door` habilita el sorteo en cada nueva conexión elegible y está activado por defecto.

Cerrar la hoja y liberar una habitación son dos decisiones distintas. El jugador puede cerrar desde el marco sin tener que salir de toda la zona del trigger; la animación ignora al jugador que pidió el cierre, pero respeta otros ocupantes y las reservas de cruce. Aunque la puerta termine cerrada, las habitaciones y sus suelos se conservan hasta que todos salgan del umbral. El alien solicita el cierre inmediatamente después de liberar su reserva al terminar el cruce; si otro personaje sigue atravesando, la orden permanece pendiente.

### Scripts y decisiones utilizadas

- **`HouseFlowController.cs` — `Assets/Scripts/Anomaly`.** Autoriza aperturas, cierres y movimientos de habitaciones. Usa **Singleton de escena** para coordinar una única casa. Implementa el **pool de habitaciones** reutilizando módulos, y **Observer** mediante avisos de conexiones y cambios de habitación. Tiene **listas** de habitaciones, ocupantes, conexiones y solicitudes; **diccionarios** de habitaciones/accesos por código, destinos normales y posiciones de estacionamiento; un **`HashSet`** de conexiones pendientes de cierre; y un **diccionario** temporal que relaciona aliens con los datos necesarios para moverlos junto a su habitación. Usa **encapsulamiento**: ofrece métodos como `RequestOpen` y consultas de solo lectura para no permitir que cada puerta reorganice la casa por su cuenta. Justificación: concentrar la autorización física en un lugar y evitar conexiones contradictorias.

  La llamada «cola de puertas» es una **`List<DoorRequest>`**, no `Queue<T>`: permite revisar prioridades, cancelar peticiones concretas y controlar vencimientos. No utiliza `Stack`. El pool es propio; no usa `UnityEngine.Pool.ObjectPool<T>`.

- **`AnomalyDirector.cs` — `Assets/Scripts/Anomaly`.** Pide elegir entre destinos que ya fueron comprobados. Usa **Strategy** mediante una referencia privada a **`IRoomSelectionStrategy`**. Mantiene una **`Queue<string>`** con los últimos dos destinos; al agregar otro retira el más antiguo. Ofrece el historial para consulta: **encapsulamiento**. Justificación: reducir repeticiones inmediatas sin decidir cómo se mueve físicamente una habitación. No es un Singleton.

- **`IRoomSelectionStrategy.cs`.** Define una **interfaz** para elegir un candidato. Recibe los candidatos como colección de solo lectura. Justificación: separar la selección de la colocación segura.

- **`RandomRoomSelectionStrategy.cs`.** Implementa **`IRoomSelectionStrategy`**: **Strategy y polimorfismo**. Usa **listas** para elegir una habitación y luego uno de sus accesos. Todas las habitaciones candidatas pueden salir; las menos recientes tienen peso dos y las recientes peso uno, sin excluirlas. Justificación: variar la casa sin dar más oportunidades a una habitación solo por tener más puertas. Los dos scripts están en `Assets/Scripts/Anomaly`.

- **`RoomModule.cs` — `Assets/Scripts/Rooms`.** Representa una habitación, con código, volumen y accesos. Usa **`List<DoorSocket>`** para esos accesos y la expone como colección de solo lectura: **encapsulamiento**. Forma parte del **pool**, pero no sortea destinos. Justificación: mover y reutilizar la habitación como unidad, conservando sus muebles y escondites.

- **`DoorSocket.cs` — `Assets/Scripts/Rooms`.** Indica posición y orientación de un acceso y con qué otro está conectado. Usa **encapsulamiento**, con conexión de cambio privado y operaciones para conectar/desconectar. La planta sigue siendo un dato de origen; ya no exige que ambos accesos sean de la misma planta para la anomalía. No es la bisagra. Justificación: alinear marcos, también en altura, sin depender del nombre visible de la puerta.

- **`RoomConnection.cs` — `Assets/Scripts/Rooms`.** Guarda los dos lados de un paso. Sus referencias principales se fijan al construirlo: **encapsulamiento**. Usa **`Dictionary<GameObject, float>`** para reservas de cruce y persecución: personaje → vencimiento. Algunos datos son `internal`, accesibles al código de la misma asamblea; no son privados exclusivos de la clase. También define `RoomConnectionCandidate`, un **`readonly struct`** que agrupa habitación y acceso candidato. Justificación: identificar qué conexión está en uso y liberar reservas antiguas sin acumularlas para siempre.

- **`RoomTopology.cs` — `Assets/Scripts/Rooms`.** Define **enums** para planta y tipo de acceso y una clase con pares de códigos de conexiones normales. No implementa por sí solo un patrón de comportamiento. Justificación: describir el orden original de la casa independientemente de la anomalía.

- **`RoomOccupant.cs` — `Assets/Scripts/Rooms`.** Identifica la habitación de un personaje y su uso de recorridos normales. Utiliza referencias y operaciones de registro: **composición y encapsulamiento**. Justificación: saber qué habitación está ocupada antes de moverla o devolverla al pool.

- **`DoorThresholdSensor.cs` — `Assets/Scripts/Rooms`.** Recibe **`OnTriggerEnter` y `OnTriggerExit`** y registra colliders en un **`HashSet<Collider>`**. El trigger detecta entrada/salida de la zona; no abre por sí solo la puerta ni reemplaza apuntar y pulsar E. Usa **encapsulamiento**, ofreciendo consultas de ocupación, y comprueba registros para retirar ocupantes que ya no corresponden. Justificación: no cerrar o mover el paso mientras alguien está cruzándolo y evitar registros repetidos por varios eventos.

- **`DoorInteractable.cs` — `Assets/Scripts/Interaction`.** Recibe E y gira la hoja sobre su pivote, pidiendo autorización a la casa. Implementa **`IInteractable` e `IOpenable`**. Emite eventos de apertura/cierre: **Observer**, aprovechado por el sonido y la coordinación de conexiones. Usa una **`List<Collider>`** para restaurar colisiones ignoradas temporalmente. Se apoya en `DoorEntity`: **composición y encapsulamiento** de las reglas de estado. Justificación: separar el movimiento visible de la puerta de las reglas que permiten abrirla.

- **`DoorFrameInteractable.cs` — `Assets/Scripts/Interaction`.** Implementa **`IInteractable`** y pasa la interacción del marco a la puerta referenciada: **composición y polimorfismo**. Justificación: facilitar apuntar sin duplicar las reglas de apertura.

- **`GameEntity.cs` — `Assets/Scripts/Core`.** Contiene la cadena **`GameEntity → InteractiveEntity → DoorEntity`**: un caso concreto de **herencia**. La base aporta identidad; la siguiente define operaciones de interacción; `DoorEntity` implementa las reglas de puerta. Usa **polimorfismo** mediante métodos `abstract` y `override`. `State` tiene `private set`: **encapsulamiento**. Los estados de puerta usan un enum; no son el patrón State por clases de los aliens. Justificación: impedir cambios incoherentes y mantener las reglas separadas de la animación.

- **`RoomNavigation.cs` — `Assets/Scripts/Aliens`.** Construye una vez la navegación de cada habitación a partir de sus colliders y la registra de nuevo cuando se traslada. Usa **listas** temporales de geometría e indicaciones de construcción. Guarda internamente el NavMesh y su registro: **encapsulamiento**. Justificación: que un cuarto reubicado siga teniendo suelo navegable sin reconstruir todo cada vez.

- **`RoomModulePoolLayout.cs` — `Assets/Scripts/Rooms`.** Organiza cómo se presenta el pool en el editor. No es quien decide las conexiones durante la partida. Está relacionado con la preparación del **pool**, no con Strategy de destinos. Justificación: separar el acomodo para trabajar en la escena del cambio de habitaciones durante el juego.

**Relación:** puerta solicita → casa comprueba → director y estrategia eligen → módulo y acceso se colocan → navegación y población actualizan el paso. Los personajes comparten una misma casa real; no hay habitaciones duplicadas para jugador y aliens.

## 8. Sonidos, preguntas, respuestas y llamados

Hay dos cosas diferentes: **el audio que escucha la persona que juega** y **el aviso de ruido que recibe la inteligencia del alien**. Reproducir un clip no obliga a los aliens a investigarlo.

```text
Jugador camina → Reproducir pasos → Sin aviso sospechoso
Alien camina  → Reproducir pasos → Sin aviso sospechoso

Jugador corre o alguien abre/cierra una puerta
                       ↓
             Reproducir audio y emitir aviso
                       ↓
             Alien que puede oírlo investiga
                       ↓
                  Girar y preguntar
                       ↓
             ¿El compañero produjo ese ruido?
             ├─ Sí → Responder «fui yo» → Retomar patrulla
             └─ No → Llamar e ir a investigar
                                    ↓
                       Compañero cercano puede acudir
                                    ↓
                     ¿Encuentran al jugador?
                     ├─ Sí → Perseguir
                     └─ No → Terminar búsqueda y patrullar

Alien ve al jugador → También llama al compañero cercano
```

### Scripts y decisiones utilizadas

- **`PlayerFootstepAudio.cs` — `Assets/Scripts/Player`.** Reproduce pasos según desplazamiento y distingue caminar de correr. Consulta el movimiento: **composición**. Sus clips y tiempos son campos protegidos ajustables: **encapsulamiento**. Al correr emite avisos a `SoundSignalSystem`; caminar no los emite. Participa como emisor del sistema **Observer**. Justificación: permitir ajustar el sonido sin tocar el movimiento ni hacer detectable la caminata. Los intervalos elegidos manualmente se conservan.

- **`DoorSoundEmitter.cs` — `Assets/Scripts/Interaction`.** Escucha los eventos de la puerta y reproduce apertura/cierre: **Observer y composición**. También emite el aviso sospechoso con quien produjo el ruido. Justificación: añadir audio y reacción sin meter esas instrucciones dentro de la animación de cada puerta.

- **`AlienAmbientAudio.cs` — `Assets/Scripts/Aliens`.** Reproduce pasos y vocalizaciones de deambular, pregunta, respuesta, llamada y persecución. Usa componentes de audio y del alien: **composición**, con clips y tiempos privados: **encapsulamiento**. No crea señales sospechosas por los pasos del alien. Justificación: separar expresividad sonora de la decisión de investigar.

- **`AlienHearing.cs` — `Assets/Scripts/Aliens`.** Escucha los avisos de ruido, comunicación y jugador visto: **Observer**. Se suscribe al activarse y se retira al desactivarse. Usa **composición** con controlador, voz y responsable del sonido. Comprueba habitación o alcance según el tipo de aviso y solicita la reacción; no necesita guardar otra lista de todos los ruidos. Justificación: escuchar señales concretas en vez de intentar interpretar el audio reproducido.

- **`SoundResponsibility.cs` — `Assets/Scripts/Aliens`.** Recuerda por un plazo los ruidos que produjo ese alien. Usa **`Dictionary<int, float>`**: identificador de sonido → vencimiento; y una **`List<int>`** auxiliar para retirar recuerdos vencidos. Ofrece operaciones como `Claim` e `IsResponsibleFor`: **encapsulamiento**. Justificación: responder por el ruido correcto y no quedar permanentemente como responsable de cualquier sonido.

- **`SoundSignalSystem.cs` — `Assets/Scripts/Audio`.** Distribuye avisos mediante eventos: **Observer**. Es una **clase estática**, no un Singleton de GameObject. Usa un **`Dictionary<int, AlienController>`** para relacionar cada ruido con quien lleva la investigación, con métodos para reclamar y liberar ese papel: **encapsulamiento**. Incluye clases de datos como `SoundStimulus`, `AlienCommunicationSignal` y `PlayerSpottedSignal`, que transportan el origen y posición relevante del aviso. Justificación: que todos entiendan a qué ruido se refiere la pregunta, sin enlazar directamente cada emisor con cada oyente.

- **`AlienController.cs`.** Aquí coordina la espera de respuesta, investigación y asistencia. Usa sus componentes especializados: **composición**. Los pasos nuevos pueden actualizar la atención sin reiniciar una pregunta a cada paso. Justificación: una reacción consistente, con prioridad de la visión del jugador sobre una sospecha sonora.

- **`ProceduralGreyboxAudio.cs` — `Assets/Scripts/Audio`.** Genera los clips provisionales con **`AudioClip.Create`** y **arrays de muestras de sonido**. Es una clase de utilidades estática, no un Singleton ni un Observer. Justificación: probar el audio sin descargar assets; después se pueden asignar clips reales sin cambiar el sistema de avisos.

Una pista sonora no garantiza que el niño siga allí. Los ruidos normales se filtran por las habitaciones correspondientes; los llamados tienen sus propias reglas de cercanía.

## 9. Historia, celular, contador y final de partida

Los objetivos son pasos narrativos de esta partida; no hay todavía un sistema separado de achievements persistentes entre partidas. Los mensajes expresan lo que piensa el niño y las puertas de la ruta reciben material rojo mientras corresponde señalarlas.

La derrota muestra «nunca nadie lo volvió a ver». En el menú principal, Q permite «Salir al escritorio»: en el juego compilado cierra la aplicación; dentro de Unity detiene Play sin cerrar el editor. La tecla se lee con el nuevo Input System desde `GameSessionManager`, y `PrototypeHUD` agrega la opción al texto existente del menú sin reemplazar los controles editados a mano.

```text
Menú → Comenzar partida
                ↓
     Mensajes iniciales: buscar a mamá y papá
                ↓
      Entrar en habitación de los padres
                ↓
      Nuevo objetivo: intentar salir de la casa
                ↓
      Puerta de salida lleva al cuarto del niño
                ↓
      Activar anomalía y buscar el celular
                ↓
      Acercarse al sofá y pulsar E para revisar
                ↓
      «No hay señal» → Comenzar tres minutos
                ↓
      Mostrar contador y adelantar fase intensa
                ↓
      ¿Es atrapado antes de terminar?
      ├─ Sí → Derrota → Ofrecer reinicio
      └─ No → Llegar a cero → Victoria
                «Llegó el amanecer, sobreviviste»
```

### Scripts y decisiones utilizadas

- **`StoryProgression.cs` — `Assets/Scripts/Core`.** Ordena objetivos, pensamientos y señalización. Usa una **`Queue<string>`** para mostrar pensamientos en orden; al cambiar de objetivo retira mensajes que quedaron viejos. Para buscar la próxima puerta útil usa **`Queue<RoomModule>`** y **`Dictionary<RoomModule, DoorSocket>`**: explora rutas y recuerda por dónde empezar. Usa **arrays de materiales** para destacar la puerta y restaurar su apariencia. Emite `ObjectiveChanged` y escucha sesión/cambios de habitación: **Observer**. Su objetivo tiene cambio controlado: **encapsulamiento**. Tiene acceso global `Instance`, **similar a Singleton**, pero no la misma protección contra duplicados que los otros coordinadores. Justificación: que mensajes, anomalía, celular y guía consulten un único objetivo actual. Los objetivos son un enum, no State por clases.

- **`StoryPhone.cs` — `Assets/Scripts/Interaction`.** Representa el celular fijo. Implementa **`IInteractable`**, comprueba objetivo, jugador y partida, y solicita avanzar a `StoryProgression`: **polimorfismo y composición**. Justificación: el teléfono no necesita mantener otro reloj ni otra copia de los objetivos.

- **`StoryPhoneInteractionArea.cs` — `Assets/Scripts/Interaction`.** Implementa **`IInteractable`** y deriva la acción al teléfono: **composición y polimorfismo**. Justificación: usar un área más cómoda que el pequeño collider del celular sin duplicar la historia.

- **`StoryPhoneProximitySensor.cs` — `Assets/Scripts/Interaction`.** Detecta cercanía con **triggers** y usa **`HashSet<Collider>`** para registrar contactos sin repetidos. Guarda los contactos de manera privada y llama a métodos de `PlayerInteractor` para habilitar o retirar la opción: **encapsulamiento y composición**. Justificación: permitir ofrecer E al estar junto al sofá sin apuntar exactamente al celular ni activar por error el escondite bajo el escritorio. El trigger habilita la posibilidad; no revisa automáticamente el teléfono.

- **`GameSessionManager.cs` — `Assets/Scripts/Core`.** Controla menú, partida, victoria y derrota. Usa **Singleton de escena**, evitando coordinadores duplicados, y **Observer** con `StateChanged`. Guarda dos tiempos privados: total de partida y supervivencia. Propiedades y métodos como `BeginGame`, `StartSurvival` y `TriggerDefeat` muestran **encapsulamiento**: otras mecánicas solicitan cambios, no asignan libremente el estado. Usa un **enum** para las situaciones de sesión; no una clase State por situación. Justificación: tener un único árbitro del resultado. También contempla derrota si el personaje cae por debajo del límite de seguridad.

- **`PrototypeHUD.cs` — `Assets/Scripts/Core`.** Presenta habitación, interacción, tiempo y paneles de menú/final. Usa **Observer** al escuchar cambios de sesión, habitación y texto de interacción; consulta el tiempo actual para mostrar la cuenta. Usa referencias a los sistemas: **composición**. Justificación: mostrar lo que ocurre sin decidir por su cuenta si hubo victoria o qué habitación corresponde. Los pensamientos del niño se presentan desde `StoryProgression`.

**Relación:** historia completa el objetivo del teléfono → sesión empieza el reloj → director de actividad aplica fase intensa → HUD muestra tiempo → captura o reloj solicitan un único resultado de partida.

## 10. Resumen para explicarlo en una consulta

Las decisiones centrales se pueden contar así:

1. **Interfaces:** «Distintos objetos responden a las mismas órdenes». E usa `IInteractable`; abrir/cerrar usa `IOpenable`; revisar usa `IInspectable`. Las estrategias también tienen interfaces para cambiar una solución sin cambiar a quien la solicita.
2. **State:** «El alien tiene una acción principal por vez». Las clases de estado separan patrullar, observar, perseguir y buscar.
3. **Strategy:** «Separamos la tarea de la forma de resolverla». Se usa para elegir habitaciones, elegir puntos de búsqueda y calcular transiciones de escondites.
4. **Pool:** «La casa cambia reutilizando habitaciones existentes». No se crean habitaciones nuevas en cada apertura.
5. **Observer:** «Un sistema avisa y los interesados reaccionan». Se usa en sonidos, puertas, actividad, objetivos, sesión e interfaz.
6. **Singleton:** «Hay un coordinador único para la casa, la sesión y la actividad». No todos los scripts con métodos estáticos son Singletons.
7. **Encapsulamiento:** «Cada sistema cuida sus datos». Se consulta si un escondite está ocupado o se solicita una derrota, en lugar de cambiar esos datos arbitrariamente desde afuera.
8. **Herencia y polimorfismo:** «Compartimos una base y cada tipo completa su comportamiento». Los ejemplos más claros son los estados del alien, los intereses y la cadena de entidades de puerta.
9. **Composición:** «El personaje funciona con piezas especializadas». Movimiento, memoria, visión y audio colaboran mediante componentes.

### Qué no conviene afirmar como implementado

- **`Stack<T>` no se utiliza explícitamente en las mecánicas actuales.** Una pila atiende primero lo último agregado; no era lo necesario para las solicitudes de puertas ni los mensajes.
- La fila de solicitudes de puertas es una **List con prioridades**, no una Queue simple.
- `readonly` en una referencia a List impide reemplazar la lista, pero **no impide modificar sus elementos**.
- Las interfaces no son tags ni componentes agregables por sí solos.
- Tener un enum llamado «estado» no significa utilizar automáticamente el patrón State por clases.
- Observer no elimina todas las comprobaciones de `Update`; visión, movimiento y relojes siguen necesitando actualizaciones.
- La anomalía **ya puede combinar ambas plantas**, pero conserva accesos protegidos y comprobaciones de colocación.
- No se implementó todavía tomar, llevar y arrojar objetos, ni un sistema de logros guardados entre partidas.

## 11. Herramientas de Unity y pruebas

El proyecto utiliza **nuevo Input System**, **CharacterController** para el niño, **NavMeshAgent/NavMesh** para los aliens, **colliders y triggers** para colisiones y zonas, **AudioSource/AudioClip** para sonido y **ProBuilder** para el boceto de la casa. Los gizmos ayudan a ver pivotes y puntos de conexión en el editor; no son los que calculan el giro de la puerta durante el juego.

Las herramientas `HideSpotEditor`, `AlienControllerEditor` y `HingeGizmoEditor` ayudan a editar o visualizar datos. `SpanishHierarchySetup` ayuda con nombres del editor. `ProjectValidation` ofrece pruebas voluntarias desde el menú. No son mecánicas que el jugador deba ejecutar.

Los scripts de `Assets/Tests` comprueban historia/puertas, visión/sonidos/captura y patrulla/navegación. Su código está condicionado al editor, y no se activa como parte de una partida normal. Tener estas pruebas disponibles no significa que se hayan ejecutado durante la elaboración de este informe.
