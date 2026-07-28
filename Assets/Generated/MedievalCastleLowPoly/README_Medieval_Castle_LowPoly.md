# Medieval Castle Complete Low Poly

Asset procedural para Unity: castillo europeo del siglo XIII con exterior e interiores explorables.

## Archivos

- `Medieval_Castle_Complete_LowPoly.obj`: modelo completo con grupos nombrados por zona.
- `Medieval_Castle_Complete_LowPoly.mtl`: materiales simples para piedra, madera, hierro, paja, fuego y agua.
- `generate_medieval_castle.py`: generador reproducible del asset.

## Contenido incluido

- Murallas exteriores con adarves, almenas, torres octogonales y gatehouse.
- Foso, puente levadizo, rastrillo, camino de acceso y patio central.
- Torre del homenaje con gran salon, comedor, cocina, capilla, sala del trono, dormitorios y biblioteca.
- Sotano con mazmorras, bodegas, almacen y pasadizo secreto.
- Armeria, herreria, establos, cuartel de soldados y alojamiento de sirvientes.
- Escaleras, balcon, antorchas, chimeneas, mesas, bancos, literas, barriles, estanterias, altar, trono y rejas.

## Importacion en Unity

1. Unity detectara automaticamente el `.obj` dentro de `Assets/Generated/MedievalCastleLowPoly`.
2. Arrastra `Medieval_Castle_Complete_LowPoly.obj` a la escena.
3. Si quieres colision, agrega `MeshCollider` al objeto raiz o colliders por grupo.
4. Para luces reales, coloca Point Lights sobre los objetos `Antorcha_*` y `*_Fuego`.

## Datos tecnicos

- Estilo: low poly, materiales planos, escala compatible con Unity.
- Vertices aproximados: 4314
- Caras aproximadas: 3167
- Unidades: 1 unidad Unity = 1 metro sugerido.

