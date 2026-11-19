# Reglas del juego: Fresh Kingdom Showdown

Juego por turnos para **2, 3 o 4 jugadores** en una **arena circular**. Cada jugador tiene un **castillo**, una **catapulta**, un **color** y su **sección** del mapa. En tu turno compras objetos, los colocas en la arena y lanzas una pelota para **reventar burbujas**: cada burbuja da monedas, y los objetos que toque la pelota pueden sumar, restar o duplicar lo que ganes.

**Gana quien llegue primero a la meta de monedas**, que se elige al empezar cada partida: de **50 a 5000** (la primera vez aparece 100).

> Este documento es la referencia de diseño del grupo. Si una regla cambia, se cambia aquí primero (con un pull request) y después en el código. Todos los números están en `Assets/Resources/GameBalance.asset`.

---

## Resumen rápido

| Tema | Regla |
|---|---|
| Jugadores | 2, 3 o 4, elegidos en *Nueva partida*. El turno avanza hacia la derecha |
| Arena | Circular, con pared curva. Se divide en una sección por jugador |
| Objetivo de cada tiro | Reventar burbujas con la pelota |
| Burbujas | Aparecen al azar, en igual cantidad por sección. Más chica o más cerca del centro = más monedas |
| Burbuja de castillo | Flota sobre cada castillo. Si la revienta un rival, le roba 3 monedas al dueño |
| Turno | Compra → Colocación → Lanzamiento → Resolución → Fin |
| Tienda | 3 ofertas al azar. Actualizar cuesta 1 moneda, **una vez por turno** |
| Objetos | 7 tipos. Seis vienen en **goma**, **turbo** o **bumper** (cambian el rebote) o en positivo y negativo (cambian monedas); el aro multiplicador es especial |
| Materiales | Goma: rebota más fuerte · Turbo: acelera la pelota · Bumper: la empuja hacia afuera como un pinball |
| Positivo | +2 monedas si la pelota lo toca y revientas al menos una burbuja en ese tiro |
| Negativo | −2 monedas en las mismas condiciones |
| Aro multiplicador | Cada pasada por un aro duplica las monedas de burbujas, y se apilan: ×2, ×4, ×8… |
| Combo | Cada burbuja después de la primera en el mismo tiro suma **+1** moneda |
| Retirar objetos | Cualquier objeto, pagando su costo de retirada. No vuelve al inventario |
| Persistencia | Los objetos colocados se quedan en la arena hasta que alguien los retire |
| Victoria | Llegar a la meta de monedas elegida al empezar (50 a 5000) |

---

## 1. Preparación

- Antes de jugar se elige cuántos participan: **2, 3 o 4**. La cantidad queda fija; para cambiarla hay que volver al menú y empezar otra partida.
- Los jugadores se reparten alrededor de la arena (a 180°, 120° o 90°). Cada uno tiene su color: **Coral, Sol, Violeta y Magenta**. Son colores distintos de los de la interfaz, que usa azules, verdes y vidrio.
- La arena se divide en una **sección por jugador**, frente a su castillo. Cada sección tiene la **misma cantidad de burbujas** (16 por defecto).
- Cada jugador empieza con **5 monedas**, para que la tienda sirva desde el primer turno.

### Tu Aero

- Después de elegir jugadores y meta, cada jugador **personaliza su Aero**: un ícono 2D al estilo de los muñequitos de Messenger (cabeza y hombros brillantes con borde oscuro), sin cara.
- Se elige **nombre**, color de la cabeza y del cuerpo, **alto y ancho**, **sombrero** (gorro, gorra, copa, corona, fiesta, aro burbuja), **bufanda** (lisa, a rayas, moño, corbata, collar), **gafas** (redondas, de sol, visor aero, 3D, corazón, monóculo) y **extras** (audífonos, flor, pin SvS, varita de burbujas, alas, capa; se pueden poner varios). **Sorpréndeme** arma uno al azar (sin borrar el nombre ni la cara dibujada).
- **Dibujar:** se hace zoom a la cabeza y se le pinta la cara a mano alzada (10 colores, 4 grosores, borrador, deshacer y borrar todo). Solo se pinta dentro del círculo; mientras se dibuja no se ven el sombrero ni las gafas.
- El Aero es solo de apariencia: no cambia el juego. Aparece en el marcador (en lugar del orbe de color; salta al empezar su turno y al ganar monedas) y en la pantalla de victoria. Su nombre sale en el marcador, el cartel del turno y la victoria; si se deja vacío, se llama "Jugador N".
- El color del jugador (castillo, catapulta, sección) sigue siendo Coral, Sol, Violeta o Magenta, sin importar los colores del Aero.
- Los Aeros (con su cara dibujada) se guardan en la computadora para la próxima partida.

## 2. El turno

| Fase | Qué se puede hacer |
|---|---|
| **1. Compra** | La tienda se abre sola. Comprar objetos y actualizar las ofertas. *Continuar* (o Enter) pasa a la siguiente fase |
| **2. Colocación** | Colocar objetos del inventario y retirar objetos de la arena. *¡A lanzar!* (o Enter) pasa a la siguiente fase |
| **3. Lanzamiento** | Mover, apuntar y disparar la catapulta. Un solo tiro |
| **4. Resolución** | Se calculan las monedas del tiro, una sola vez, y se muestra el resumen |
| **5. Fin del turno** | Las burbujas reventadas reaparecen y empieza el turno del siguiente jugador |

- Comprar, actualizar, colocar y retirar **solo se puede en su fase y en tu propio turno**.
- Lo que pasó en un tiro (golpes, aros, burbujas) **no pasa al siguiente**: cada tiro empieza de cero.

## 3. La catapulta

| Acción | Tecla | Detalle |
|---|---|---|
| Moverse | `A` / `D` | Hasta 6 unidades a cada lado |
| Girar | `Q` / `E` | Hasta 70° a cada lado: alcanza los castillos de los lados |
| Ángulo | `W` / `S` | Entre 15° y 75° |
| Cargar y disparar | Mantener y soltar `Espacio` | Cada turno empieza en 0; la potencia sube y baja mientras lo mantienes |
| Otros | `C` cámara general · `Esc` pausa | En Compra y Colocación la cámara es libre (ver sección 10) |

## 4. La pelota

- Revienta las burbujas que toca y **sigue de largo**, así que un tiro puede reventar varias.
- Vuela **rápido** (la catapulta compensa: llega a las mismas distancias que antes, pero en menos tiempo).
- Rebota en la pared, los castillos, las catapultas y los objetos colocados.
- Cómo sale de un objeto depende de su **material** (ver sección 7). Los positivos y negativos son lisos: rebote fuerte y algo **caótico**, sale un poco más rápida y desviada al azar.
- Al chocar un material la pelota echa chispas de su color, suena distinto y su **estela** pasa a ser de ese color.
- No puede salir de la arena: la pared tiene una barrera invisible alta.
- Desaparece **2 segundos después de tocar el suelo**, o si se queda quieta encima de algo, se atasca (15 s) o cae fuera del mapa. Entonces se pasa a la Resolución.

## 5. Burbujas

- Aparecen en **posiciones aleatorias** dentro de su sección y a distintas alturas, nunca:
  - fuera del círculo ni pegadas a la pared,
  - enterradas en el suelo,
  - encima de otra burbuja (hay una separación mínima),
  - cerca de castillos o catapultas,
  - tocando un objeto colocado.
- Cada partida tiene una distribución distinta. Se puede fijar una semilla en `GameBalance` para repetir un escenario en pruebas.
- Las reventadas **reaparecen al final del turno** en un lugar libre de **su misma sección**. Las que siguen enteras no se mueven.
- Si una burbuja no encuentra un lugar libre (la arena está muy llena), **no aparece** y se vuelve a intentar al final del siguiente turno. Nunca se enciman.
- Su valor sale de la fórmula de los antiguos huecos, adaptada al círculo:

```
tamaño   = radioDeReferencia / radioDeLaBurbuja        (entre 0.5 y 3)
cercanía = 1 − (distancia al centro / radio útil)      (entre 0 y 1)
monedas  = máx(1, redondear(2 × tamaño × (1 + cercanía)))
```

  Dan entre **1 y 6 monedas**. El valor se muestra dentro de cada burbuja.

## 6. Burbuja de castillo (robo)

- Sobre cada castillo flota una burbuja más grande, del color de su dueño.
- Si la revienta un **rival**, le quita **3 monedas** al dueño y se las queda. El tope es de 10 por tiro.
- Se puede robar a **cualquier rival**, no solo al de enfrente: girando la catapulta se llega a los castillos de los lados (con 4 jugadores quedan a unos 55°).
- El dueño **nunca baja de 0**: se roba como mucho lo que tiene.
- La pelota del **dueño** pasa a través de su propia burbuja sin reventarla.
- El robo **no cuenta como burbuja** para los positivos, negativos y aros, y el aro no lo duplica.
- Vuelve a aparecer al final del turno.

## 7. Objetos

| Objeto | Forma |
|---|---|
| Cubo | Bloque sólido |
| Pared | Rectángulo largo |
| Rampa | Cuña inclinada: la pelota sube por ella y sale volando |
| Cilindro | Columna larga |
| Esfera | Bola |
| Pirámide | Base cuadrada y caras inclinadas |
| Aro multiplicador | Anillo dorado (especial) |

- Todos tienen **colisión física real**: desvían la pelota según su forma.
- Las seis formas geométricas vienen así:
  - **De material** (los "normales"): no dan ni quitan monedas, pero cambian cómo sale la pelota. Hay tres materiales (abajo).
  - **Positivo** (verde esmeralda, con un "+" flotando, liso): **+2 monedas** si la pelota lo toca **y** revientas al menos una burbuja en ese tiro.
  - **Negativo** (obsidiana oscura, con un "−" flotando, liso): **−2 monedas** con las mismas condiciones.

### Materiales

| Material | Cómo se ve | Qué le hace a la pelota |
|---|---|---|
| **Goma** | Gelatina rosa translúcida, con una pelotita que salta encima | Rebota con mucha más fuerza: sale a **1,4×** la velocidad con la que llegó, casi sin desviarse |
| **Turbo** | Rayas naranjas y oscuras, con una doble flecha » encima | La **acelera**: sale a **1,8×** (mínimo 26 m/s) en la dirección del rebote, un poco hacia arriba |
| **Bumper** | Cromo brillante, con un aro celeste encima | La **empuja hacia afuera** siempre a la misma velocidad (24 m/s), algo hacia arriba, como en el pinball. Se infla al golpear |

- Ninguna pelota pasa de **40 m/s** tras un rebote.
- Los materiales se combinan con la forma: una **rampa turbo** la lanza lejísimos, una **pared de goma** devuelve los tiros con fuerza, una **esfera bumper** en el centro es puro caos.
- Golpear varias veces el mismo objeto cuenta **una sola vez** por tiro. Golpear objetos distintos suma el efecto de cada uno.
- Los objetos afectan los tiros de **todos** los jugadores, no solo los de quien los colocó.

### Aro multiplicador

- Cada vez que la pelota **pasa por dentro** de un aro, las monedas de **burbujas** de ese tiro se multiplican **×2**, siempre que revientes al menos una burbuja.
- **Se apilan:** 1 pasada ×2, 2 pasadas ×4, 3 pasadas ×8, y así (×2 elevado a las pasadas). Puede ser por aros distintos o **por el mismo aro** si la pelota vuelve a cruzarlo.
- Tiene que pasar con la pelota entera dentro del agujero y salir del otro lado. **Tocar el borde no cuenta**, ni quedarse temblando en el agujero.
- El multiplicador **no duplica** las bonificaciones ni penalizaciones de positivos y negativos.

## 8. Monedas de un tiro

Todo se calcula en un solo lugar (`RewardCalculator`), en la fase de Resolución:

```
B = suma de los valores de las burbujas reventadas + combo
combo = (burbujas reventadas − 1) · 1   (+1 por cada burbuja después de la primera)
P = positivos distintos tocados        N = negativos distintos tocados
k = pasadas por aros                   (solo si B > 0)
E = 2·P − 2·N                          (solo si B > 0)
R = 2^k · B + E                        (sin aros, 2^0 = 1)
monedas del tiro = máx(0, R)
```

- Un negativo reduce lo que ganas **en ese tiro**, pero **nunca te quita monedas que ya tenías**.
- Ejemplo: revientas burbujas de 3 y 4 (B = 7), pasas por un aro y tocas un positivo: 2·7 + 2 = **16 monedas**.
- Ejemplo apilado: la misma B = 7 pasando 3 veces por aros: 8·7 = **56 monedas**.
- Ejemplo con combo: revientas 4 burbujas que valen 2, 3, 3 y 4 (12) → combo +3, B = 15; con un aro: **30 monedas**.
- Al reventar varias seguidas sale **"¡Combo ×N!"** y el sonido sube de tono. En el combo ×4, ×7 y ×10 hay un instante de **cámara lenta**, igual que al pasar por un aro o robar en un castillo.

## 9. Tienda

- Aparece al empezar tu fase de Compra (también se abre con el botón **Tienda** o con `T`).
- Muestra **3 ofertas al azar**, distintas entre sí. Cada tarjeta muestra el nombre, el ícono, la variante, la descripción y el precio. Si no te alcanza, sale deshabilitada con "Te faltan N".
- **Comprar:** se descuenta el precio y el objeto va a tu inventario. Si algo falla, no se cobra nada.
- **Actualizar ofertas:** cuesta **1 moneda** y se puede **una vez por turno**. Cerrar y abrir la tienda no lo reinicia. En tu siguiente turno vuelve a estar disponible.
- Cada jugador tiene su propia tienda. Las ofertas se renuevan al empezar cada uno de tus turnos.
- Si algún día la pool tuviera menos de 3 objetos distintos, los lugares que sobran se muestran como "Sin oferta".

### Precios iniciales

| Objeto | Goma | Turbo / Bumper | Positivo | Negativo | Retirar (Goma / Turbo y Bumper / P / Neg) |
|---|---|---|---|---|---|
| Cubo, Rampa, Cilindro, Esfera, Pirámide | 4 | 5 | 8 | 6 | 9 / 10 / 12 / 10 |
| Pared | 5 | 6 | 9 | 7 | 10 / 11 / 13 / 11 |
| Aro multiplicador | — | — | — | — | Precio **15**, retirar **18** |

- Los de material son baratos. Positivos y negativos valen más porque cambian monedas. El aro es lo más caro.
- Cada forma sale en 3 materiales, así que cada uno tiene la mitad de probabilidad de aparecer en la tienda (`shopWeight` 0,5): entre los tres salen un poco más que los normales de antes.
- Retirar cuesta bastante más que comprar, para que colocar sea una decisión estratégica.

## 10. Inventario, colocación y retirada

- Lo que compras queda en tu **inventario** (barra de abajo en la fase de Colocación) hasta que lo colocas.
- **Colocar:**
  1. Elige un objeto y aparece una vista previa de vidrio que sigue al mouse. Es **celeste** si el lugar sirve y **rosa** si no.
  2. La vista previa **cae** hasta apoyarse en el suelo o **encima de otro objeto**: así se arman torres, puentes y rampas sobre paredes que se van complicando con la partida.
  3. Gíralo y colócalo como quieras: `Q`/`E` girar, `R`/`F` inclinar, `Z`/`X` ladear, `Shift` + ruedita (o `RePág`/`AvPág`) para subirlo y dejarlo flotando.
  4. **Clic** para confirmar. **Clic derecho** (sin arrastrar) o `Esc` cancela, y el objeto sigue en tu inventario.
- **Vista libre** mientras compras y colocas: clic derecho + arrastrar gira la cámara, `WASD` (o arrastrar con la ruedita apretada) la mueve, la ruedita acerca o aleja y `C` vuelve a la vista de arriba. Cada turno empieza desde arriba.
- No se puede colocar:
  - fuera del círculo ni atravesando la pared,
  - atravesando el suelo,
  - encima de una burbuja (ni pegado a una),
  - atravesando otro objeto (tocarlo sí se puede: apoyado encima o pegado al lado),
  - encimado con un castillo o una catapulta,
  - más alto que **16 m** contando todo lo apilado debajo (`stackMaxHeight`).
- Los objetos colocados **se quedan en la arena** entre turnos y recuerdan **quién los puso**.
- **Retirar:**
  1. En tu fase de Colocación, haz clic sobre **cualquier** objeto de la arena, tuyo o de un rival.
  2. Se muestra qué es, quién lo puso y cuánto cuesta. Hay que **confirmar** para pagar.
  3. Si no te alcanzan las monedas, no se retira. Al confirmar, el objeto desaparece y **no vuelve al inventario**.
  4. Lo que estaba **apoyado encima cae** hasta lo que quede debajo (una torre baja entera). Lo que se dejó flotando a propósito se queda donde estaba.

## 11. Victoria

- Gana el primero que llega a la **meta de monedas**, sea por burbujas o por robo.
- La meta se elige en **Nueva partida**, junto con cuántos juegan: de **50 a 5000** (opciones en `winCoinChoices`). Se recuerda la última; la primera vez aparece 100.
- Al ganar se bloquean los turnos y aparece la pantalla de victoria, con *Jugar otra vez* o *Menú principal*.

## 12. Casos límite

| Situación | Qué pasa |
|---|---|
| Comprar o actualizar sin monedas | No se permite y no se cobra nada |
| Robo cuando el dueño tiene 0 | No se roba nada |
| Negativo más grande que lo ganado | El tiro da 0, no resta del saldo |
| Tocar objetos sin reventar burbujas | No dan ni quitan nada |
| La arena se llena de objetos | Las burbujas que no caben esperan al siguiente turno |
| La pelota queda quieta sobre un objeto | El tiro termina a los 1,5 s |

---

## Pendiente de decidir (grupo)

- [ ] **Ritmo de la partida:** en pruebas automáticas, un tiro bien apuntado revienta 1 burbuja y da unas **3 monedas** (promedio de 3,0 a 3,4 en 48 tiros). Con meta 100 son unos 30 turnos por jugador, y una persona apunta peor que la prueba. Ahora la meta se elige al empezar (50 a 5000) y los aros apilados pueden dar tiros enormes. Falta probar qué meta se siente bien con y sin aros, y si conviene subir el valor base de las burbujas de 2 a **3** (`bubbleBaseValue`).
- [ ] **Números:** probar partidas completas y ajustar precios, costos de retirada y valores de burbujas en `GameBalance.asset`. Los precios iniciales se pensaron con esas ~3 monedas por turno: un normal cuesta 1 turno, un positivo unos 3, el aro unos 5 y retirar de 3 a 6.
- [x] **Cantidad de burbujas por sección:** subió de 4 a **16**, porque jugando costaba mucho atinarles.
- [ ] **Límite de objetos en la arena:** hoy no hay; con 4 jugadores se puede llenar.
- [x] **Ritmo:** la pelota va más rápido (`projectileSpeed` 1,7) y hay materiales, combos, temblor y cámara lenta.
- [ ] **Más materiales:** por ahora goma, turbo y bumper. Ideas para después: hielo (resbala sin frenar), esponja (frena la pelota, defensivo), trampolín (lanza hacia arriba).
- [ ] **Fuerza de los materiales:** probar en partidas y ajustar en `GameBalance.asset` (sección Materiales).
- [x] **Girar con Q/E:** se queda, ahora hasta 70° para poder robar a los castillos de los lados.

## Estado en el prototipo de Unity

| Parte | Estado |
|---|---|
| Arena circular, secciones y burbujas aleatorias | ✅ Hecho |
| 2, 3 y 4 jugadores (menú *Nueva partida*) | ✅ Hecho |
| Fases del turno, tienda, inventario y colocación | ✅ Hecho |
| Variantes, aro multiplicador y retirada | ✅ Hecho |
| Materiales (goma, turbo, bumper), combos, temblor y cámara lenta | ✅ Hecho |
| Aeros personalizables en 2D con cara dibujada (menú, marcador y victoria) | ✅ Hecho |
| Equilibrio fino de la economía | ⏳ Pendiente (partidas de prueba del grupo) |
