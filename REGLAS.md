# Reglas del juego: Catapultas y burbujas

Juego por turnos para **2, 3 o 4 jugadores** en una **arena circular**. Cada jugador tiene un **castillo**, una **catapulta**, un **color** y su **sección** del mapa. En tu turno compras objetos, los colocas en la arena y lanzas una pelota para **reventar burbujas**: cada burbuja da monedas, y los objetos que toque la pelota pueden sumar, restar o duplicar lo que ganes.

**Gana quien llegue primero a 100 monedas** (la meta se puede cambiar en *Opciones*).

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
| Objetos | 7 tipos. Seis tienen variante normal, positiva y negativa; el aro multiplicador es especial |
| Positivo | +2 monedas si la pelota lo toca y revientas al menos una burbuja en ese tiro |
| Negativo | −2 monedas en las mismas condiciones |
| Aro multiplicador | Si la pelota pasa por dentro y revientas burbujas, las monedas de burbujas se duplican |
| Retirar objetos | Cualquier objeto, pagando su costo de retirada. No vuelve al inventario |
| Persistencia | Los objetos colocados se quedan en la arena hasta que alguien los retire |
| Victoria | Llegar a la meta de monedas (100 por defecto) |

---

## 1. Preparación

- Antes de jugar se elige cuántos participan: **2, 3 o 4**. La cantidad queda fija; para cambiarla hay que volver al menú y empezar otra partida.
- Los jugadores se reparten alrededor de la arena (a 180°, 120° o 90°). Cada uno tiene su color: **Coral, Sol, Violeta y Magenta**. Son colores distintos de los de la interfaz, que usa azules, verdes y vidrio.
- La arena se divide en una **sección por jugador**, frente a su castillo. Cada sección tiene la **misma cantidad de burbujas** (4 por defecto).
- Cada jugador empieza con **5 monedas**, para que la tienda sirva desde el primer turno.

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
| Girar | `Q` / `E` | Hasta 35° a cada lado |
| Ángulo | `W` / `S` | Entre 15° y 75° |
| Cargar y disparar | Mantener y soltar `Espacio` | La potencia sube y baja mientras lo mantienes |
| Otros | `C` cámara general · `Esc` pausa | |

## 4. La pelota

- Revienta las burbujas que toca y **sigue de largo**, así que un tiro puede reventar varias.
- Rebota en la pared, los castillos, las catapultas y los objetos colocados.
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
- Las seis formas geométricas vienen en tres variantes, que se distinguen a la vista:
  - **Normal** (perla brillante): solo desvía la pelota, no da ni quita monedas.
  - **Positivo** (verde esmeralda, con un "+" flotando): **+2 monedas** si la pelota lo toca **y** revientas al menos una burbuja en ese tiro.
  - **Negativo** (obsidiana oscura, con un "−" flotando): **−2 monedas** con las mismas condiciones.
- Golpear varias veces el mismo objeto cuenta **una sola vez** por tiro. Golpear objetos distintos suma el efecto de cada uno.
- Los objetos afectan los tiros de **todos** los jugadores, no solo los de quien los colocó.

### Aro multiplicador

- Si la pelota **pasa por dentro** del aro **y** revientas al menos una burbuja en ese tiro, las monedas de **burbujas** se multiplican **×2**.
- Tiene que pasar con la pelota entera dentro del agujero. **Tocar el borde no cuenta.**
- Pasar varias veces por el mismo aro, o por varios aros, aplica el ×2 **una sola vez**.
- El ×2 **no duplica** las bonificaciones ni penalizaciones de positivos y negativos.

## 8. Monedas de un tiro

Todo se calcula en un solo lugar (`RewardCalculator`), en la fase de Resolución:

```
B = suma de los valores de las burbujas reventadas
P = positivos distintos tocados        N = negativos distintos tocados
E = 2·P − 2·N                          (solo si B > 0)
R = B + E          sin aro
R = 2·B + E        con aro              (solo si B > 0)
monedas del tiro = máx(0, R)
```

- Un negativo reduce lo que ganas **en ese tiro**, pero **nunca te quita monedas que ya tenías**.
- Ejemplo: revientas burbujas de 3 y 4 (B = 7), pasas por un aro y tocas un positivo: 2·7 + 2 = **16 monedas**.

## 9. Tienda

- Aparece al empezar tu fase de Compra (también se abre con el botón **Tienda** o con `T`).
- Muestra **3 ofertas al azar**, distintas entre sí. Cada tarjeta muestra el nombre, el ícono, la variante, la descripción y el precio. Si no te alcanza, sale deshabilitada con "Te faltan N".
- **Comprar:** se descuenta el precio y el objeto va a tu inventario. Si algo falla, no se cobra nada.
- **Actualizar ofertas:** cuesta **1 moneda** y se puede **una vez por turno**. Cerrar y abrir la tienda no lo reinicia. En tu siguiente turno vuelve a estar disponible.
- Cada jugador tiene su propia tienda. Las ofertas se renuevan al empezar cada uno de tus turnos.
- Si algún día la pool tuviera menos de 3 objetos distintos, los lugares que sobran se muestran como "Sin oferta".

### Precios iniciales

| Objeto | Normal | Positivo | Negativo | Retirar (N / P / Neg) |
|---|---|---|---|---|
| Cubo, Rampa, Cilindro, Esfera, Pirámide | 3 | 8 | 6 | 8 / 12 / 10 |
| Pared | 4 | 9 | 7 | 9 / 13 / 11 |
| Aro multiplicador | — | — | — | Precio **15**, retirar **18** |

- Los normales son baratos. Las variantes valen más porque cambian monedas. El aro es lo más caro.
- Retirar cuesta bastante más que comprar, para que colocar sea una decisión estratégica.

## 10. Inventario, colocación y retirada

- Lo que compras queda en tu **inventario** (barra de abajo en la fase de Colocación) hasta que lo colocas.
- **Colocar:**
  1. Elige un objeto y aparece una vista previa de vidrio que sigue al mouse. Es **celeste** si el lugar sirve y **rosa** si no.
  2. Gíralo y colócalo como quieras: `Q`/`E` girar, `R`/`F` inclinar, `Z`/`X` ladear, ruedita (o `RePág`/`AvPág`) para subir o bajar.
  3. **Clic** para confirmar. **Clic derecho** o `Esc` cancela, y el objeto sigue en tu inventario.
- No se puede colocar:
  - fuera del círculo ni atravesando la pared,
  - atravesando el suelo,
  - encima de una burbuja (ni pegado a una),
  - encimado con otro objeto, un castillo o una catapulta.
- Los objetos colocados **se quedan en la arena** entre turnos y recuerdan **quién los puso**.
- **Retirar:**
  1. En tu fase de Colocación, haz clic sobre **cualquier** objeto de la arena, tuyo o de un rival.
  2. Se muestra qué es, quién lo puso y cuánto cuesta. Hay que **confirmar** para pagar.
  3. Si no te alcanzan las monedas, no se retira. Al confirmar, el objeto desaparece y **no vuelve al inventario**.

## 11. Victoria

- Gana el primero que llega a la **meta de monedas** (100 por defecto; de 20 a 200 en *Opciones*), sea por burbujas o por robo.
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

- [ ] **Ritmo de la partida:** en pruebas automáticas, un tiro bien apuntado revienta 1 burbuja y da unas **3 monedas** (promedio de 3,0 a 3,4 en 48 tiros). Con meta 100 son unos 30 turnos por jugador, y una persona apunta peor que la prueba. Propuesta: bajar la meta a **50** o subir el valor base de las burbujas de 2 a **3** (`bubbleBaseValue`).
- [ ] **Números:** probar partidas completas y ajustar precios, costos de retirada y valores de burbujas en `GameBalance.asset`. Los precios iniciales se pensaron con esas ~3 monedas por turno: un normal cuesta 1 turno, un positivo unos 3, el aro unos 5 y retirar de 3 a 6.
- [ ] **Cantidad de burbujas por sección** (hoy 4): ¿más con 2 jugadores?
- [ ] **Límite de objetos en la arena:** hoy no hay; con 4 jugadores se puede llenar.
- [ ] **Girar con Q/E:** se agregó porque sin girar no se llega a los lados. ¿Se queda?

## Estado en el prototipo de Unity

| Parte | Estado |
|---|---|
| Arena circular, secciones y burbujas aleatorias | ✅ Hecho |
| 2, 3 y 4 jugadores (menú *Nueva partida*) | ✅ Hecho |
| Fases del turno, tienda, inventario y colocación | ✅ Hecho |
| Variantes, aro multiplicador y retirada | ✅ Hecho |
| Equilibrio fino de la economía | ⏳ Pendiente (partidas de prueba del grupo) |
