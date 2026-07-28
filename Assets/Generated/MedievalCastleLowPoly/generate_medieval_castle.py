import math
from pathlib import Path


OUT_DIR = Path(__file__).resolve().parent
OBJ_PATH = OUT_DIR / "Medieval_Castle_Complete_LowPoly.obj"
MTL_PATH = OUT_DIR / "Medieval_Castle_Complete_LowPoly.mtl"
README_PATH = OUT_DIR / "README_Medieval_Castle_LowPoly.md"


class ObjBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.current_object = None
        self.current_material = None

    def object(self, name):
        safe = name.replace(" ", "_")
        self.current_object = safe
        self.faces.append(("o", safe))

    def material(self, name):
        self.current_material = name
        self.faces.append(("usemtl", name))

    def v(self, x, y, z):
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def face(self, indices):
        self.faces.append(("f", indices))

    def box(self, name, cx, cy, cz, sx, sy, sz, mat, top=True, bottom=True):
        self.object(name)
        self.material(mat)
        x0, x1 = cx - sx / 2, cx + sx / 2
        y0, y1 = cy - sy / 2, cy + sy / 2
        z0, z1 = cz - sz / 2, cz + sz / 2
        pts = [
            self.v(x0, y0, z0), self.v(x1, y0, z0), self.v(x1, y1, z0), self.v(x0, y1, z0),
            self.v(x0, y0, z1), self.v(x1, y0, z1), self.v(x1, y1, z1), self.v(x0, y1, z1),
        ]
        self.face([pts[0], pts[1], pts[2], pts[3]])
        self.face([pts[5], pts[4], pts[7], pts[6]])
        self.face([pts[4], pts[0], pts[3], pts[7]])
        self.face([pts[1], pts[5], pts[6], pts[2]])
        if top:
            self.face([pts[3], pts[2], pts[6], pts[7]])
        if bottom:
            self.face([pts[4], pts[5], pts[1], pts[0]])

    def prism(self, name, cx, cy, cz, radius, height, sides, mat, top=True, bottom=True):
        self.object(name)
        self.material(mat)
        bottom_ring = []
        top_ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides + math.pi / sides
            x = cx + math.cos(a) * radius
            z = cz + math.sin(a) * radius
            bottom_ring.append(self.v(x, cy - height / 2, z))
            top_ring.append(self.v(x, cy + height / 2, z))
        for i in range(sides):
            j = (i + 1) % sides
            self.face([bottom_ring[i], bottom_ring[j], top_ring[j], top_ring[i]])
        if top:
            self.face(list(reversed(top_ring)))
        if bottom:
            self.face(bottom_ring)

    def roof(self, name, cx, cy, cz, sx, sy, sz, mat, axis="x"):
        self.object(name)
        self.material(mat)
        x0, x1 = cx - sx / 2, cx + sx / 2
        z0, z1 = cz - sz / 2, cz + sz / 2
        y0 = cy - sy / 2
        y1 = cy + sy / 2
        if axis == "x":
            v = [
                self.v(x0, y0, z0), self.v(x1, y0, z0), self.v(x1, y0, z1), self.v(x0, y0, z1),
                self.v(x0, y1, cz), self.v(x1, y1, cz),
            ]
            self.face([v[0], v[1], v[5], v[4]])
            self.face([v[3], v[4], v[5], v[2]])
            self.face([v[0], v[4], v[3]])
            self.face([v[1], v[2], v[5]])
        else:
            v = [
                self.v(x0, y0, z0), self.v(x1, y0, z0), self.v(x1, y0, z1), self.v(x0, y0, z1),
                self.v(cx, y1, z0), self.v(cx, y1, z1),
            ]
            self.face([v[0], v[1], v[4]])
            self.face([v[3], v[5], v[2]])
            self.face([v[0], v[4], v[5], v[3]])
            self.face([v[1], v[2], v[5], v[4]])

    def write(self):
        with OBJ_PATH.open("w", encoding="utf-8") as f:
            f.write("mtllib Medieval_Castle_Complete_LowPoly.mtl\n")
            f.write("# Complete low poly 13th-century European castle with exterior and explorable interiors.\n")
            for x, y, z in self.vertices:
                f.write(f"v {x:.4f} {y:.4f} {z:.4f}\n")
            for item in self.faces:
                if item[0] == "o":
                    f.write(f"\no {item[1]}\n")
                elif item[0] == "usemtl":
                    f.write(f"usemtl {item[1]}\n")
                elif item[0] == "f":
                    f.write("f " + " ".join(str(i) for i in item[1]) + "\n")


def add_crenellations(b, prefix, x0, x1, z0, z1, y, mat):
    step = 6
    idx = 0
    x = x0
    while x <= x1:
        b.box(f"{prefix}_Almena_Norte_{idx}", x, y, z1, 3.0, 2.2, 2.2, mat)
        b.box(f"{prefix}_Almena_Sur_{idx}", x, y, z0, 3.0, 2.2, 2.2, mat)
        x += step
        idx += 1
    z = z0
    while z <= z1:
        b.box(f"{prefix}_Almena_Oeste_{idx}", x0, y, z, 2.2, 2.2, 3.0, mat)
        b.box(f"{prefix}_Almena_Este_{idx}", x1, y, z, 2.2, 2.2, 3.0, mat)
        z += step
        idx += 1


def add_table_set(b, prefix, x, z, sx, sz):
    b.box(f"{prefix}_Mesa_Madera", x, 1.45, z, sx, 0.35, sz, "WoodDark")
    for dx in (-sx / 2 + 0.4, sx / 2 - 0.4):
        for dz in (-sz / 2 + 0.4, sz / 2 - 0.4):
            b.box(f"{prefix}_Pata_{dx}_{dz}", x + dx, 0.75, z + dz, 0.25, 1.4, 0.25, "WoodDark")
    for i, dz in enumerate((-sz / 2 - 0.8, sz / 2 + 0.8)):
        b.box(f"{prefix}_Banco_{i}", x, 0.85, z + dz, sx, 0.35, 0.55, "WoodDark")


def add_torch(b, name, x, y, z, rotation_axis="z"):
    b.box(f"{name}_Soporte_Hierro", x, y, z, 0.15, 0.7, 0.15, "Iron")
    b.prism(f"{name}_Llama", x, y + 0.55, z, 0.28, 0.75, 5, "Fire")


def add_stairs(b, prefix, x, z, width, depth, height, steps, direction="north"):
    for i in range(steps):
        sy = height / steps
        if direction == "north":
            cz = z + (i + 0.5) * depth / steps
            sx, sz = width, depth / steps
        else:
            cz = z - (i + 0.5) * depth / steps
            sx, sz = width, depth / steps
        cy = 0.2 + (i + 0.5) * sy
        b.box(f"{prefix}_Escalon_{i:02d}", x, cy, cz, sx, sy, sz, "StoneTrim")


def build_castle():
    b = ObjBuilder()

    # Terrain, moat, and approach.
    b.box("Base_Terreno_Bajo", 0, -0.12, 0, 150, 0.24, 130, "Grass", top=True)
    b.box("Foso_Agua_Norte", 0, 0.03, 56, 142, 0.08, 10, "Water")
    b.box("Foso_Agua_Sur", 0, 0.03, -56, 142, 0.08, 10, "Water")
    b.box("Foso_Agua_Oeste", -66, 0.03, 0, 10, 0.08, 112, "Water")
    b.box("Foso_Agua_Este", 66, 0.03, 0, 10, 0.08, 112, "Water")
    b.box("Camino_Entrada_Tierra", 0, 0.04, -75, 18, 0.1, 40, "Dirt")
    b.box("Puente_Levadizo_Bajado", 0, 0.55, -58, 14, 0.55, 20, "WoodDark")
    for i in range(5):
        b.box(f"Puente_Costilla_Hierro_{i}", -6 + i * 3, 0.9, -58, 0.22, 0.28, 20, "Iron")

    # Curtain walls with front gate gap.
    b.box("Muralla_Norte", 0, 6, 45, 116, 12, 4, "Stone")
    b.box("Muralla_Oeste", -58, 6, 0, 4, 12, 90, "Stone")
    b.box("Muralla_Este", 58, 6, 0, 4, 12, 90, "Stone")
    b.box("Muralla_Sur_Oeste", -37, 6, -45, 42, 12, 4, "Stone")
    b.box("Muralla_Sur_Este", 37, 6, -45, 42, 12, 4, "Stone")
    add_crenellations(b, "MurallaExterior", -58, 58, -45, 45, 13.1, "StoneTrim")

    # Wall walk planks and inner ramps.
    b.box("Adarve_Norte", 0, 12.4, 42.5, 112, 0.35, 3, "Wood")
    b.box("Adarve_Oeste", -55.5, 12.4, 0, 3, 0.35, 84, "Wood")
    b.box("Adarve_Este", 55.5, 12.4, 0, 3, 0.35, 84, "Wood")
    b.box("Adarve_Sur_Oeste", -37, 12.4, -42.5, 38, 0.35, 3, "Wood")
    b.box("Adarve_Sur_Este", 37, 12.4, -42.5, 38, 0.35, 3, "Wood")
    add_stairs(b, "Escalera_Adarve_Oeste", -49, -35, 4, 18, 12, 12, "north")
    add_stairs(b, "Escalera_Adarve_Este", 49, -17, 4, 18, 12, 12, "north")

    # Towers and gatehouse.
    tower_positions = [
        ("Suroeste", -58, -45), ("Sureste", 58, -45), ("Noroeste", -58, 45), ("Noreste", 58, 45),
        ("Oeste_Media", -58, 0), ("Este_Media", 58, 0), ("Norte_Central", 0, 45),
    ]
    for name, x, z in tower_positions:
        b.prism(f"Torre_Vigilancia_{name}", x, 8, z, 7.2, 16, 8, "Stone")
        b.prism(f"Torre_Vigilancia_{name}_Techo_Conico", x, 18.2, z, 6.6, 4.5, 8, "RoofSlate")
        for i in range(8):
            a = 2 * math.pi * i / 8
            b.box(f"Torre_Vigilancia_{name}_Almena_{i}", x + math.cos(a) * 5.5, 17.2, z + math.sin(a) * 5.5, 1.7, 2.2, 1.7, "StoneTrim")
        b.box(f"Torre_Vigilancia_{name}_Ventana_Saetera", x, 8, z - 7.25, 0.9, 3.0, 0.25, "Shadow")

    b.prism("Torre_Gatehouse_Oeste", -10, 9, -45, 6.5, 18, 8, "Stone")
    b.prism("Torre_Gatehouse_Este", 10, 9, -45, 6.5, 18, 8, "Stone")
    b.box("Gatehouse_Cuerpo_Superior", 0, 13.5, -45, 25, 9, 7, "Stone")
    b.box("Gatehouse_Rastrillo_Hierro", 0, 5.2, -48.9, 9, 8, 0.35, "Iron")
    for i in range(6):
        b.box(f"Gatehouse_Puas_Rastrillo_{i}", -4.5 + i * 1.8, 1.2, -49.1, 0.28, 2.4, 0.28, "Iron")
    b.box("Gatehouse_Puerta_Madera_Izquierda", -2.4, 4.2, -47.7, 4.5, 7.6, 0.45, "WoodDark")
    b.box("Gatehouse_Puerta_Madera_Derecha", 2.4, 4.2, -47.7, 4.5, 7.6, 0.45, "WoodDark")
    b.box("Gatehouse_Sala_Guardia_Interior", 0, 6, -38.5, 22, 10, 6, "Stone")
    add_table_set(b, "Gatehouse_Sala_Guardia", 0, -38.5, 7, 2.2)

    # Inner ward.
    b.box("Patio_Central_Empedrado", 0, 0.08, -5, 86, 0.16, 70, "Cobble")
    b.box("Pozo_Patio_Base", -16, 0.6, -4, 4, 1.2, 4, "StoneTrim")
    b.prism("Pozo_Patio_Boca", -16, 1.5, -4, 2.4, 1.2, 8, "Stone")
    b.box("Pozo_Patio_Travesano_Madera", -16, 3.6, -4, 5.5, 0.35, 0.35, "WoodDark")
    b.box("Fuente_Patio_Abrevadero", 20, 0.55, -8, 9, 1.1, 3, "StoneTrim")
    b.box("Fuente_Patio_Agua", 20, 1.13, -8, 8, 0.08, 2.2, "Water")

    # Keep exterior and internal floors.
    b.box("Torre_Del_Homenaje_Muro_Norte", 0, 9, 27, 44, 18, 3, "Stone")
    b.box("Torre_Del_Homenaje_Muro_Sur", 0, 9, -7, 44, 18, 3, "Stone")
    b.box("Torre_Del_Homenaje_Muro_Oeste", -22, 9, 10, 3, 18, 34, "Stone")
    b.box("Torre_Del_Homenaje_Muro_Este", 22, 9, 10, 3, 18, 34, "Stone")
    b.box("Torre_Del_Homenaje_Piso_Bajo", 0, 0.18, 10, 41, 0.36, 31, "StoneFloor")
    b.box("Torre_Del_Homenaje_Piso_Alto", 0, 8.6, 10, 41, 0.32, 31, "Wood")
    b.box("Torre_Del_Homenaje_Techo_Plano", 0, 18.25, 10, 45, 0.5, 35, "StoneTrim")
    b.roof("Torre_Del_Homenaje_Techo_Pizarra", 0, 22, 10, 48, 7, 38, "RoofSlate", axis="x")
    add_crenellations(b, "TorreDelHomenaje", -22, 22, -7, 27, 19.7, "StoneTrim")

    # Keep partitions, rooms, and corridors.
    b.box("Interior_Muro_Divisor_GranSalon_Cocina", -7, 4, 10, 1.2, 8, 28, "StoneInterior")
    b.box("Interior_Muro_Divisor_Comedor_Capilla", 9, 4, 10, 1.2, 8, 28, "StoneInterior")
    b.box("Interior_Muro_Transversal_Bajo", 1, 4, 4, 40, 8, 1.2, "StoneInterior")
    b.box("Gran_Salon_Floor_Marker", -15, 0.42, 16, 13, 0.08, 18, "Rushes")
    add_table_set(b, "Gran_Salon_Mesa_Alta", -15, 17, 10, 3)
    add_table_set(b, "Gran_Salon_Mesa_Larga_1", -15, 10, 11, 2.4)
    b.box("Gran_Salon_Chimenea_Piedra", -20.4, 3.1, 16, 1.2, 6, 7, "StoneTrim")
    b.box("Gran_Salon_Fuego_Chimenea", -19.7, 1.5, 16, 0.8, 2.2, 3.8, "Fire")

    add_table_set(b, "Comedor_Mesa_Principal", 15, 15, 11, 3)
    b.box("Comedor_Aparador", 20.1, 1.8, 10, 0.8, 3, 8, "WoodDark")
    b.box("Cocina_Hogar_Piedra", -16, 1.1, -2.5, 9, 2.2, 2.5, "StoneTrim")
    b.box("Cocina_Fuego", -16, 1.75, -2.2, 5, 1.4, 0.9, "Fire")
    b.box("Cocina_Mesa_Preparacion", -16, 1.1, 0.8, 10, 0.45, 2.2, "Wood")
    for i in range(4):
        b.prism(f"Cocina_Barril_{i}", -21 + i * 3.0, 0.9, -3.5, 0.8, 1.8, 8, "WoodDark")

    b.box("Capilla_Altar", 15, 1.0, -2.6, 7, 1.4, 1.6, "StoneTrim")
    b.box("Capilla_Cruz_Madera_Vertical", 15, 3.1, -3.25, 0.35, 3.5, 0.25, "WoodDark")
    b.box("Capilla_Cruz_Madera_Horizontal", 15, 3.5, -3.25, 2.2, 0.28, 0.25, "WoodDark")
    for i in range(4):
        b.box(f"Capilla_Banco_{i}", 10 + i * 3.2, 0.75, 1.4, 2.3, 0.35, 1.1, "WoodDark")

    # Upper floor rooms.
    b.box("Interior_Muro_Alto_Dormitorios_Biblioteca", -4, 12.5, 10, 1.0, 7.5, 28, "Wood")
    b.box("Interior_Muro_Alto_Trono_Dormitorios", 9, 12.5, 10, 1.0, 7.5, 28, "Wood")
    b.box("Interior_Muro_Transversal_Alto", 2, 12.5, 12, 39, 7.5, 1.0, "Wood")
    b.box("Sala_Trono_Plataforma", 16, 9.05, 20, 10, 0.8, 6, "StoneTrim")
    b.box("Sala_Trono_Asiento", 16, 10.0, 21, 3.2, 1.4, 2.4, "WoodDark")
    b.box("Sala_Trono_Respaldo", 16, 11.4, 22.0, 3.6, 4.2, 0.5, "WoodDark")
    b.box("Sala_Trono_Dosel", 16, 13.9, 21.3, 6, 0.3, 4, "ClothRed")
    for i in range(5):
        b.box(f"Biblioteca_Estanteria_{i}", -20.5, 10.7, 20 - i * 4.0, 0.9, 3.8, 3.0, "WoodDark")
        b.box(f"Biblioteca_Libros_{i}", -19.9, 11.2, 20 - i * 4.0, 0.25, 2.5, 2.4, "ClothRed")
    for i, x in enumerate((-16, -10, 3)):
        b.box(f"Dormitorio_Cama_{i}", x, 9.4, 4, 4.8, 0.8, 2.5, "WoodDark")
        b.box(f"Dormitorio_Colchon_{i}", x, 10.0, 4, 4.4, 0.45, 2.2, "ClothRed")
        b.box(f"Dormitorio_Arcon_{i}", x + 3.0, 9.6, 4, 1.6, 1.1, 1.1, "Wood")

    # Basement and dungeons below keep.
    b.box("Sotano_Boveda_Principal", 0, -3.1, 10, 39, 5.8, 29, "StoneInterior")
    b.box("Mazmorras_Pasillo_Central", 0, -5.9, 10, 32, 0.18, 4, "StoneFloor")
    for i, x in enumerate((-15, -9, -3, 3, 9, 15)):
        b.box(f"Mazmorra_Celda_{i}_Suelo", x, -5.8, 17, 4.5, 0.22, 7, "StoneFloor")
        b.box(f"Mazmorra_Celda_{i}_Rejas", x, -3.2, 13.4, 4.5, 4.8, 0.25, "Iron")
        for bar in range(4):
            b.box(f"Mazmorra_Celda_{i}_Barrote_{bar}", x - 1.7 + bar * 1.1, -3.1, 13.25, 0.15, 4.8, 0.15, "Iron")
    b.box("Bodega_Vino_Estanteria", -15, -3.4, 0.2, 10, 2.4, 1.2, "WoodDark")
    for i in range(8):
        b.prism(f"Bodega_Barril_{i}", -19 + i * 2.3, -4.5, -2.4, 0.75, 1.6, 8, "WoodDark")
    b.box("Almacen_Sacos", 13, -4.8, -1, 8, 1.6, 4, "ClothTan")
    b.box("Pasadizo_Secreto_Detras_Biblioteca", -27, -3.5, 10, 2.2, 4.5, 22, "Shadow")
    b.box("Pasadizo_Secreto_Salida_Muralla_Norte", -27, -2.8, 33, 2.2, 3.0, 18, "Shadow")

    # Outbuildings in the ward.
    b.box("Armeria_Muro_Norte", -37, 3, 23, 18, 6, 1.2, "Stone")
    b.box("Armeria_Muro_Sur", -37, 3, 11, 18, 6, 1.2, "Stone")
    b.box("Armeria_Muro_Oeste", -46, 3, 17, 1.2, 6, 12, "Stone")
    b.box("Armeria_Muro_Este", -28, 3, 17, 1.2, 6, 12, "Stone")
    b.roof("Armeria_Techo", -37, 7.5, 17, 20, 4, 14, "RoofSlate", axis="x")
    for i in range(6):
        b.box(f"Armeria_Estante_Armas_{i}", -44 + i * 3.0, 1.8, 21.8, 0.4, 3.2, 1.0, "WoodDark")
        b.box(f"Armeria_Lanza_{i}", -44 + i * 3.0, 2.4, 21.5, 0.12, 4.0, 0.12, "Iron")

    b.box("Herreria_Muro_Fondo", 38, 3, 20, 18, 6, 1.2, "Stone")
    b.box("Herreria_Muro_Lateral_Oeste", 29, 3, 14, 1.2, 6, 12, "Stone")
    b.box("Herreria_Muro_Lateral_Este", 47, 3, 14, 1.2, 6, 12, "Stone")
    b.roof("Herreria_Techo", 38, 7.2, 14, 20, 4, 14, "RoofSlate", axis="x")
    b.box("Herreria_Forja_Piedra", 37, 1.2, 19, 5, 2.4, 2.2, "StoneTrim")
    b.box("Herreria_Forja_Fuego", 37, 2.4, 19.1, 3.5, 1.3, 1.1, "Fire")
    b.box("Herreria_Yunque", 42, 1.1, 15, 2.0, 1.1, 1.0, "Iron")

    b.box("Establos_Muro_Fondo", 37, 3, -26, 25, 6, 1.2, "Wood")
    b.box("Establos_Muro_Oeste", 24.5, 3, -34, 1.2, 6, 16, "Wood")
    b.box("Establos_Muro_Este", 49.5, 3, -34, 1.2, 6, 16, "Wood")
    b.roof("Establos_Techo_Paja", 37, 7.0, -34, 27, 4.5, 18, "Thatch", axis="x")
    for i in range(5):
        b.box(f"Establo_Box_{i}_Separador", 27 + i * 5, 2, -34, 0.35, 4, 12, "WoodDark")
        b.box(f"Establo_Pesebre_{i}", 29 + i * 5, 0.9, -28, 3.2, 0.9, 1.2, "WoodDark")

    b.box("Cuartel_Soldados_Muro_Norte", -37, 3, -18, 25, 6, 1.2, "Stone")
    b.box("Cuartel_Soldados_Muro_Sur", -37, 3, -35, 25, 6, 1.2, "Stone")
    b.box("Cuartel_Soldados_Muro_Oeste", -49.5, 3, -26.5, 1.2, 6, 17, "Stone")
    b.box("Cuartel_Soldados_Muro_Este", -24.5, 3, -26.5, 1.2, 6, 17, "Stone")
    b.roof("Cuartel_Soldados_Techo", -37, 7.2, -26.5, 27, 4, 19, "RoofSlate", axis="x")
    for i in range(6):
        b.box(f"Cuartel_Litera_{i}", -46 + i * 4, 1.0, -31, 3.0, 1.0, 1.2, "WoodDark")
        b.box(f"Cuartel_Manta_{i}", -46 + i * 4, 1.65, -31, 2.7, 0.25, 1.0, "ClothTan")

    b.box("Alojamiento_Sirvientes_Muro_Norte", 0, 3, -25, 22, 6, 1.2, "Wood")
    b.box("Alojamiento_Sirvientes_Muro_Sur", 0, 3, -37, 22, 6, 1.2, "Wood")
    b.box("Alojamiento_Sirvientes_Muro_Oeste", -11, 3, -31, 1.2, 6, 12, "Wood")
    b.box("Alojamiento_Sirvientes_Muro_Este", 11, 3, -31, 1.2, 6, 12, "Wood")
    b.roof("Alojamiento_Sirvientes_Techo_Paja", 0, 7, -31, 24, 4, 14, "Thatch", axis="x")
    for i in range(4):
        b.box(f"Sirvientes_Catre_{i}", -8 + i * 5, 0.8, -33, 3.2, 0.7, 1.2, "WoodDark")

    # Balconies, stairs, and connection markers.
    b.box("Balcon_Madera_Sobre_Patio", 0, 9.8, -8.8, 24, 0.4, 3.2, "WoodDark")
    b.box("Balcon_Baranda", 0, 11.2, -10.4, 24, 1.4, 0.35, "WoodDark")
    add_stairs(b, "Escalera_Principal_Torre_Homenaje", 19, -4, 4, 25, 8.4, 14, "north")
    add_stairs(b, "Escalera_Sotano_Mazmorras", -19, 2, 3.5, 14, -5.5, 11, "north")

    # Torches throughout.
    torch_points = [
        (-52, 6, -35), (52, 6, -35), (-52, 6, 35), (52, 6, 35),
        (-20.5, 3.5, 8), (-20.5, 3.5, 23), (20.5, 3.5, 8), (20.5, 3.5, 23),
        (-6, 3.5, -5.7), (9, 3.5, -5.7), (-18, -3.2, 12), (18, -3.2, 12),
        (-37, 3.0, 11.5), (38, 3.0, 19.5), (0, 3.0, -25.5),
    ]
    for i, (x, y, z) in enumerate(torch_points):
        add_torch(b, f"Antorcha_{i:02d}", x, y, z)

    # Door lintels and visual openings.
    for i, (name, x, z, sx) in enumerate([
        ("Puerta_Torre_Homenaje", 0, -8.8, 8),
        ("Puerta_Armeria", -37, 10.2, 5),
        ("Puerta_Herreria", 38, 13.2, 5),
        ("Puerta_Establos", 37, -25.1, 7),
        ("Puerta_Cuartel", -37, -17.1, 6),
        ("Puerta_Sirvientes", 0, -24.1, 5),
    ]):
        b.box(f"{name}_Marco_Madera", x, 3.2, z, sx, 0.5, 0.5, "WoodDark")
        b.box(f"{name}_Umbral_Piedra", x, 0.25, z, sx + 1, 0.5, 1.0, "StoneTrim")

    b.write()
    return b


def write_mtl():
    materials = {
        "Stone": (0.43, 0.43, 0.40),
        "StoneTrim": (0.56, 0.55, 0.50),
        "StoneInterior": (0.35, 0.35, 0.34),
        "StoneFloor": (0.38, 0.37, 0.34),
        "Cobble": (0.32, 0.33, 0.31),
        "Wood": (0.47, 0.30, 0.16),
        "WoodDark": (0.26, 0.14, 0.07),
        "Iron": (0.12, 0.12, 0.13),
        "RoofSlate": (0.18, 0.21, 0.25),
        "Thatch": (0.61, 0.48, 0.23),
        "Water": (0.10, 0.28, 0.45),
        "Grass": (0.20, 0.36, 0.18),
        "Dirt": (0.33, 0.23, 0.14),
        "Fire": (1.00, 0.36, 0.04),
        "Rushes": (0.48, 0.40, 0.20),
        "ClothRed": (0.48, 0.04, 0.04),
        "ClothTan": (0.58, 0.48, 0.34),
        "Shadow": (0.04, 0.04, 0.04),
    }
    with MTL_PATH.open("w", encoding="utf-8") as f:
        for name, (r, g, b) in materials.items():
            f.write(f"newmtl {name}\n")
            f.write(f"Kd {r:.3f} {g:.3f} {b:.3f}\n")
            f.write("Ka 0.050 0.050 0.050\n")
            f.write("Ks 0.050 0.050 0.050\n")
            f.write("Ns 16\n")
            if name == "Water":
                f.write("d 0.68\n")
            f.write("\n")


def write_readme(vertex_count, face_count):
    README_PATH.write_text(
        f"""# Medieval Castle Complete Low Poly

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
- Vertices aproximados: {vertex_count}
- Caras aproximadas: {face_count}
- Unidades: 1 unidad Unity = 1 metro sugerido.

""",
        encoding="utf-8",
    )


if __name__ == "__main__":
    builder = build_castle()
    write_mtl()
    face_count = sum(1 for item in builder.faces if item[0] == "f")
    write_readme(len(builder.vertices), face_count)
    print(f"Wrote {OBJ_PATH}")
    print(f"Vertices: {len(builder.vertices)} Faces: {face_count}")
