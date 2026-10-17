# Reglas del juego: Catapultas y Castillos

Juego por turnos sobre una cuadrícula. Cada jugador tiene un **castillo** y una **catapulta**. En tu turno lanzas una roca: si cae en un **hueco** ganas monedas, y si cae en el hueco del castillo de un rival le **robas** monedas. Antes de tirar puedes colocar **obstáculos** que compraste en la **tienda** para ayudarte o complicarle la vida a los demás.

**Gana quien llegue primero a 100 monedas** (la meta se puede cambiar).

> Este documento es la referencia de diseño del grupo. Si una regla cambia, se cambia aquí primero (con un pull request) y después en el código.

---

## Resumen rápido

| Tema | Regla |
|---|---|
| Jugadores | 2 al inicio, 4 más adelante. El turno avanza hacia la derecha |
| Catapulta | Se mueve un poco a los lados, ajusta el ángulo, carga potencia y dispara. **Un tiro por turno** |
| Huecos | Dan monedas: más pequeño = más monedas; más lejos de las esquinas = más monedas |
| Castillos | No reciben daño |
| Hueco del castillo | Si un rival encesta ahí, le quita monedas al dueño y se las queda. El dueño nunca baja de 0 |
| Obstáculos | Buenos, malos o neutros. Multiplican lo que gana la roca y afectan los tiros de **todos** |
| Activación | Caja: la roca debe **entrar**. Aro: debe **pasar por dentro**. Plataforma: debe tocar la **cara de arriba** |
| Quitar obstáculo | Cuesta un porcentaje de su precio original |
| Tienda | 3 ítems al azar (pueden repetirse). Refrescar cuesta 1 moneda. Se abre para todos al inicio de cada ronda |
| Persistencia | Los obstáculos no se guardan: al terminar la ronda se descartan |
| Rocas | Desaparecen 2 s después de sumar o restar monedas, o de tocar el suelo sin entrar a un hueco |
| Victoria | Llegar a la meta de monedas (100 por defecto) |

---

## 1. Preparación

- Cada jugador empieza con **0 monedas**.
- Los castillos están en lados opuestos del mapa (con 4 jugadores, uno en cada lado) y cada catapulta queda frente a su castillo, mirando al centro.
- El mapa tiene entre **6 y 10 huecos** de distintos tamaños y distancias. En el prototipo hay 9, colocados en espejo para que ningún jugador tenga ventaja.
- El valor de cada hueco se calcula al empezar la partida y **no cambia** durante el juego. Se muestra encima del hueco (`+2`, `+5`, `+8`…).

## 2. Flujo de una ronda

1. **Inicio de ronda:** se abre la tienda para todos los jugadores.
2. **Turno de cada jugador**, en orden hacia la derecha:
   1. **Colocación:** coloca los obstáculos que compró. También puede quitar uno pagando.
   2. **Tiro:** mueve la catapulta, apunta, carga potencia y dispara.
   3. Cuando la roca desaparece, pasa el turno al siguiente jugador.
3. **Fin de ronda:** cuando todos tiraron, se descartan los obstáculos (los colocados y los que no se usaron) y empieza la siguiente ronda con una tienda nueva.
4. **En cuanto alguien llega a la meta, la partida termina**, aunque la ronda no haya acabado.

Estados del juego, en orden: `Tienda → Colocación → Apuntando → EsperandoRoca → SiguienteTurno → FinDeRonda`.

## 3. La catapulta

| Acción | Tecla | Detalle |
|---|---|---|
| Moverse | `A` / `D` | Solo un poco: hasta 6 unidades a cada lado de su posición inicial |
| Girar | `Q` / `E` | Hasta 35° a cada lado (agregado en el prototipo, ver pendientes) |
| Ángulo | `W` / `S` | Entre 15° y 75° (empieza en 45°) |
| Cargar | Mantener `Espacio` | La barra de potencia sube y baja sola mientras lo mantienes |
| Disparar | Soltar `Espacio` | La roca sale con la potencia que tenía la barra en ese momento |

- **Un solo tiro por turno.** Después de disparar, la catapulta queda bloqueada hasta tu siguiente turno.
- Una guía muestra la primera mitad del arco para ayudar a apuntar.
- Otros controles: `C` cambia a la cámara general y `Esc` pausa.

## 4. Las rocas

- Una roca **solo puede sumar o restar monedas una vez**.
- Para contar, el **centro** de la roca tiene que quedar dentro del hueco. Rozar el borde no cuenta.
- Al tocar el suelo empieza una cuenta de **2 segundos**: si en ese tiempo rueda hasta un hueco, todavía cuenta. Al terminar la cuenta, la roca desaparece y pasa el turno.
- Al caer en un hueco, la roca se mete en él y desaparece 2 segundos después.
- Reglas de seguridad: si la roca se atasca, desaparece a los **15 segundos**, y si cae fuera del mapa también desaparece.
- Los obstáculos **no** cuentan como suelo: rebotar en uno no inicia la cuenta.

## 5. Huecos y monedas

Cada hueco da más monedas cuanto **más pequeño** es y cuanto **más lejos de las esquinas** está:

```
factorTamaño    = radioDeReferencia / radioDelHueco               (limitado entre 0.5 y 3)
factorDistancia = distanciaALaEsquinaMásCercana / distanciaMáxima (entre 0 y 1)
monedas         = máx(1, redondear(base × factorTamaño × (1 + factorDistancia)))
```

- Con los valores actuales (base 2, radio de referencia 1.5) los huecos dan entre **2 y 8 monedas**.
- Si la roca pasó por obstáculos: `monedas = máx(1, redondear(monedasDelHueco × multiplicadorDeLaRoca))`.
- **Regla de balance:** ningún hueco debe dar tantas monedas que se pueda ganar en 2 o 3 tiros.

## 6. Castillos y robo de monedas

- Los castillos **no reciben daño**: las rocas solo rebotan en sus muros.
- Dentro de cada castillo (en el patio) hay un **hueco de castigo**.
- Si un **rival** encesta en él, **le quita monedas al dueño y se las suma**:
  - Robo base: **3 monedas** (× multiplicador de la roca), con un **tope de 10 por tiro**.
  - Se roba como mucho lo que el dueño tiene: `robo real = mín(cantidad, monedas del dueño)`. **El dueño nunca baja de 0** y el ladrón solo recibe lo que realmente se quitó; no aparecen monedas de la nada.
- Si el **dueño** encesta en su propio castillo, **no pasa nada** (la roca igual desaparece).
- Robar también puede hacerte ganar la partida.

## 7. Obstáculos

### Tipos

| Tipo | Multiplicador | Efecto en la roca |
|---|---|---|
| Bueno | mayor que 1 | La roca gana más monedas o roba más |
| Malo | menor que 1 | La roca gana menos |
| Neutro | igual a 1 | Solo cambia la trayectoria |

- Los obstáculos afectan los tiros de **todos** los jugadores, no solo los de quien los colocó.
- La roca empieza con multiplicador **1** y cada obstáculo que activa lo multiplica por su valor.
- Una roca **no puede activar dos veces el mismo obstáculo**.

### Cómo se activan

| Forma | Se activa si… | No se activa si… |
|---|---|---|
| **Caja** | La roca entra al hueco interior | Solo toca las paredes |
| **Aro** | La roca pasa por dentro | Choca con el borde del aro |
| **Plataforma** | La roca toca la cara plana de arriba | Toca los costados o la parte de abajo |

### Colocar

- Solo durante tu **fase de colocación**.
- Se ve una versión translúcida (fantasma) antes de confirmar. `R` rota y **clic izquierdo** confirma.
- No se puede colocar encima de huecos, castillos ni otros obstáculos.
- Cada obstáculo colocado recuerda **quién lo puso** y **cuánto costó**.

### Quitar

- **Clic derecho** sobre un obstáculo colocado: cuesta `redondear(precio original × porcentaje de quitar)` (por ejemplo, 50%).
- Si no te alcanzan las monedas, aparece un aviso y el obstáculo se queda.

## 8. Tienda

- Se abre para todos al **inicio de cada ronda**.
- Muestra **3 ítems al azar**, que pueden repetirse.
- **Comprar** descuenta el precio y guarda el ítem en tu inventario. Sin monedas suficientes no se puede comprar.
- **Refrescar** cuesta **1 moneda** y vuelve a sortear los 3 ítems.
- Cada jugador tiene su propia tienda: refrescar solo cambia la tuya (pendiente de confirmar, ver abajo).
- Lo que no uses en la ronda **se pierde** al terminarla.

## 9. Victoria

- Gana el primer jugador que llega a la **meta de monedas**: **100** por defecto. Se puede cambiar en *Opciones* (de 20 a 200).
- Al llegar a la meta se bloquean los turnos y aparece la pantalla de victoria, con las opciones de jugar otra vez o volver al menú.

## 10. Casos límite

| Situación | Qué pasa |
|---|---|
| Un rival encesta en tu castillo cuando tienes 0 monedas | No te quitan nada y él no gana nada |
| Comprar sin monedas suficientes | No se permite |
| Ítems comprados y no usados | Se descartan al terminar la ronda |
| La roca se atasca | Desaparece a los 15 s y pasa el turno |
| Colocar un obstáculo sobre un hueco | No se permite |
| Multiplicadores muy altos | Ninguna combinación debe permitir ganar de un solo tiro |

---

## Pendiente de decidir (grupo)

- [ ] **Orden de colocación:** ¿cada quien coloca en su turno (los obstáculos afectan solo a los tiros que vienen después en esa ronda) o todos colocan antes del primer tiro? Cambiarlo es solo reordenar las fases.
- [ ] **Tienda:** ¿individual (propuesta) o los mismos 3 ítems para todos en la ronda?
- [ ] **Números:** precios de la tienda, porcentaje para quitar obstáculos y valores de los multiplicadores.
- [ ] **Tope en los huecos normales:** el robo tiene un tope de 10 por tiro; ¿ponemos uno también a las monedas de los huecos cuando hay multiplicadores?
- [ ] **Girar con Q/E:** no estaba en el diseño original; se agregó porque sin girar no se llega a los huecos de los lados. ¿Se queda?
- [ ] **4 jugadores:** en qué lado va cada uno y en qué sentido avanza el turno.

## Estado en el prototipo de Unity

| Parte | Estado |
|---|---|
| Turnos de 2 jugadores, catapulta y rocas | ✅ Hecho |
| Huecos con la fórmula de monedas | ✅ Hecho |
| Hueco del castillo (robo) | ✅ Hecho |
| Victoria, menú principal y opciones (volumen, música, meta) | ✅ Hecho |
| Obstáculos | ⏳ Pendiente (paso 4) |
| Tienda y rondas | ⏳ Pendiente (paso 5) |
| 4 jugadores | ⏳ Pendiente (paso 7) |
