# Playa nocturna con Unity Terrain

En Unity 6000.3.23f1, espera a que terminen las importaciones y selecciona:
**Turtle Game > Crear playa nocturna con Terrain**.

El generador crea `Assets/Scenes/MoonBeach.unity` y sus recursos en
`Assets/Environment/MoonBeach/`. Conserva la escena prototipo y los Build Profiles.
Si ya existen los destinos, se detiene para evitar sobrescribir trabajo manual.

## Editar el ambiente

- Selecciona `Beach - sculpt and paint here` en la jerarquia.
- Usa Raise/Lower Terrain y Smooth Height para cambiar las dunas.
- Usa Paint Texture para mezclar DrySand y WetSand.
- El recorrido empieza cerca de Z=5 y avanza hacia la carretera en Z=124.
- Las dunas laterales dejan un corredor sinuoso; los tres obstaculos principales
  estan cerca de Z=28, Z=59 y Z=85. Ajustalos tras probar con WASD.
- Las luces de carretera estan en Z=130. Comprueba desde la camara baja que las
  dunas ocultan los postes durante el inicio; ajusta alturas y posiciones.
- El mar es un bloque visual provisional, sin shader de olas ni sonido.
- El prefab de tortuga y su controlador se reutilizan sin modificarlos.

## Alcance

Base de ambiente editable, no juego terminado. No incluye secuencia final,
atropello, audio, limites de nivel ni eventos narrativos. El recorrido tiene
unos 120 metros: con la velocidad actual dura menos que los 5-8 minutos previstos.
Antes de alargarlo, valida orientacion, escala y sensacion de movimiento.

## Comprobacion pendiente

Unity se cerro con codigo 1 antes de importar el proyecto durante los intentos
batch de esta sesion, tambien con -nographics. No se pudo validar compilacion,
generar la escena ni revisar el resultado visual. Ejecuta el menu en el editor
para generar los assets y realizar la primera revision.
