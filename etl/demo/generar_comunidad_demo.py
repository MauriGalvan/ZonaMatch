"""
Genera etl/demo/comunidad_demo.sql: datos de DEMO para la comunidad de las zonas (resenas, votos de "util",
preguntas con respuestas y aportes al mapa pendientes de validar).

Todo es inventado. Los autores son usuarios de demo con email @demo.zonamatch.ar: asi se reconocen y se borran
(etl/demo/borrar_comunidad_demo.sql). Pueden iniciar sesion con la contrasena CONTRASENA_DEMO.

Cada zona tiene un perfil (1 a 5 por aspecto) que guia los puntajes y los textos, para que una resena de un barrio
tranquilo no diga que es inseguro. Las fechas se guardan como "hace N horas" y se resuelven con now() al cargar,
asi las novedades siempre se ven recientes.

El resultado es siempre el mismo (semilla fija). Uso:
    python etl/demo/generar_comunidad_demo.py
"""

import base64
import hashlib
import random
import struct
import uuid
from pathlib import Path

SEMILLA = 2026
CONTRASENA_DEMO = "ZonaMatchDemo2026"
DOMINIO = "demo.zonamatch.ar"
ESPACIO_IDS = uuid.UUID("6f0c2b52-6c53-4a8e-9a43-0f2e7f6d2a10")
SALIDA = Path(__file__).with_name("comunidad_demo.sql")

ASPECTOS = ["seguridad", "transporte", "conectividad", "comercios", "espacios-verdes"]

# Perfil de cada zona: seguridad, transporte, conectividad, comercios, espacios verdes (1 a 5),
# y referencias del lugar para los textos (avenida y espacio verde; None si no hay una clara).
ZONAS = {
    # CABA
    "palermo": ("Palermo", (3, 5, 5, 5, 5), "Santa Fe", "los Bosques de Palermo"),
    "belgrano": ("Belgrano", (4, 5, 5, 5, 4), "Cabildo", "las Barrancas de Belgrano"),
    "recoleta": ("Recoleta", (4, 5, 5, 5, 4), "Las Heras", "Plaza Francia"),
    "caballito": ("Caballito", (3, 5, 5, 5, 4), "Rivadavia", "el Parque Centenario"),
    "villa-crespo": ("Villa Crespo", (3, 4, 5, 4, 3), "Corrientes", None),
    "almagro": ("Almagro", (3, 5, 5, 4, 2), "Rivadavia", None),
    "villa-urquiza": ("Villa Urquiza", (4, 4, 5, 4, 4), "Triunvirato", None),
    "nunez": ("Núñez", (4, 4, 5, 4, 4), "Cabildo", "el Parque de la Memoria"),
    "saavedra": ("Saavedra", (4, 3, 4, 3, 5), "García del Río", "el Parque Sarmiento"),
    "colegiales": ("Colegiales", (4, 4, 5, 4, 3), "Federico Lacroze", None),
    "villa-devoto": ("Villa Devoto", (4, 3, 4, 4, 5), "Francisco Beiró", "la Plaza Arenales"),
    "villa-del-parque": ("Villa del Parque", (4, 4, 4, 4, 4), "Cuenca", None),
    "flores": ("Flores", (2, 5, 4, 5, 3), "Rivadavia", "la Plaza Flores"),
    "floresta": ("Floresta", (3, 4, 4, 4, 2), "Avellaneda", None),
    "villa-luro": ("Villa Luro", (3, 4, 4, 3, 3), "Rivadavia", None),
    "monte-castro": ("Monte Castro", (4, 3, 4, 3, 3), "Álvarez Jonte", None),
    "velez-sarsfield": ("Vélez Sarsfield", (3, 4, 4, 3, 2), "Juan B. Justo", None),
    "liniers": ("Liniers", (2, 5, 4, 4, 2), "Rivadavia", None),
    "mataderos": ("Mataderos", (2, 3, 3, 3, 3), "Directorio", None),
    "parque-avellaneda": ("Parque Avellaneda", (3, 3, 4, 3, 5), "Directorio", "el Parque Avellaneda"),
    "parque-chacabuco": ("Parque Chacabuco", (3, 4, 4, 4, 4), "Asamblea", "el Parque Chacabuco"),
    "boedo": ("Boedo", (3, 4, 4, 4, 2), "Boedo", None),
    "san-telmo": ("San Telmo", (2, 4, 4, 5, 3), "Paseo Colón", "el Parque Lezama"),
    "la-boca": ("La Boca", (1, 3, 3, 3, 2), "Almirante Brown", None),
    "barracas": ("Barracas", (2, 3, 4, 3, 3), "Montes de Oca", None),
    "villa-lugano": ("Villa Lugano", (2, 3, 3, 3, 4), "Roca", "el Parque Indoamericano"),
    "balvanera": ("Balvanera", (2, 5, 5, 5, 1), "Corrientes", None),
    "chacarita": ("Chacarita", (3, 5, 5, 4, 3), "Corrientes", None),
    "villa-ortuzar": ("Villa Ortúzar", (4, 4, 4, 3, 3), "Álvarez Thomas", None),
    "agronomia": ("Agronomía", (4, 3, 4, 2, 5), "San Martín", "el predio de la Facultad de Agronomía"),
    # Conurbano
    "ramos-mejia": ("Ramos Mejía", (3, 4, 4, 5, 3), "Rivadavia", None),
    "haedo": ("Haedo", (3, 4, 4, 4, 4), "Rivadavia", None),
    "moron": ("Morón", (3, 4, 4, 5, 4), "Rivadavia", "la Plaza San Martín de Morón"),
    "castelar": ("Castelar", (4, 4, 4, 4, 4), "Santa Rosa", None),
    "ciudadela": ("Ciudadela", (2, 4, 3, 4, 2), "Rivadavia", None),
    "san-justo": ("San Justo", (2, 4, 3, 5, 2), "Arieta", None),
    "villa-luzuriaga": ("Villa Luzuriaga", (2, 3, 3, 3, 2), "Don Bosco", None),
    "lomas-del-mirador": ("Lomas del Mirador", (2, 3, 3, 3, 2), "Juan Manuel de Rosas", None),
    "olivos": ("Olivos", (4, 4, 5, 4, 5), "Maipú", "la costa del río"),
    "martinez": ("Martínez", (4, 4, 5, 5, 4), "Santa Fe", None),
    "san-isidro": ("San Isidro", (4, 3, 5, 4, 5), "Centenario", "el bajo de San Isidro"),
    "florida": ("Florida", (4, 4, 4, 4, 3), "San Martín", None),
    "quilmes": ("Quilmes", (2, 4, 4, 5, 4), "Rivadavia", "la costanera"),
    "bernal-oeste": ("Bernal Oeste", (3, 4, 4, 4, 4), "San Martín", None),
    "banfield": ("Banfield", (3, 4, 4, 4, 4), "Alsina", None),
    "lomas-de-zamora": ("Lomas de Zamora", (2, 4, 4, 5, 3), "Meeks", None),
    "lanus-oeste": ("Lanús Oeste", (2, 4, 4, 5, 2), "Hipólito Yrigoyen", None),
    "adrogue": ("Adrogué", (4, 3, 4, 4, 5), "Espora", None),
    "temperley": ("Temperley", (3, 4, 4, 4, 4), "Meeks", None),
}

NOMBRES = [
    "Ana", "Lucía", "Martina", "Sofía", "Valentina", "Camila", "Florencia", "Julieta", "Carolina", "Paula",
    "Agustina", "Micaela", "Romina", "Natalia", "Gabriela", "Laura", "Silvina", "Mariana", "Belén", "Rocío",
    "Juan", "Martín", "Lucas", "Matías", "Nicolás", "Santiago", "Facundo", "Diego", "Pablo", "Federico",
    "Gonzalo", "Tomás", "Ezequiel", "Leandro", "Sebastián", "Hernán", "Damián", "Franco", "Ignacio", "Rodrigo",
]
APELLIDOS = [
    "Pérez", "González", "Rodríguez", "Fernández", "López", "Martínez", "García", "Sánchez", "Romero", "Díaz",
    "Álvarez", "Torres", "Ruiz", "Ramírez", "Flores", "Acosta", "Benítez", "Medina", "Herrera", "Suárez",
    "Aguirre", "Giménez", "Molina", "Castro", "Ortiz", "Silva", "Núñez", "Luna", "Ríos", "Cabrera",
]
CANTIDAD_USUARIOS = 64

APERTURAS = [
    "Vivo en {zona} hace {anios} años.",
    "Hace {anios} años que alquilo en {zona}.",
    "Me mudé a {zona} hace {anios} años con mi pareja.",
    "Soy de {zona} de toda la vida.",
    "Llegué a {zona} por laburo y me terminé quedando.",
    "Estoy en {zona} desde hace un par de años.",
    "Nos vinimos a {zona} con los chicos hace {anios} años.",
    "",
]

# Frases por aspecto y nivel: bajo (1-2), medio (3) o alto (4-5)
FRASES = {
    "seguridad": {
        "alto": [
            "Es un barrio tranquilo, en todos estos años nunca tuve un problema.",
            "De noche se puede caminar sin drama por las calles principales.",
            "Hay movimiento de gente hasta tarde y eso da seguridad.",
            "Los vecinos se conocen y se cuidan entre todos.",
        ],
        "medio": [
            "La seguridad es la normal de cualquier barrio: de día bien, de noche hay que estar atento.",
            "Algunas cuadras alejadas de {avenida} se ponen solitarias a la noche.",
            "Hubo algún robo en la cuadra, pero nada fuera de lo común.",
        ],
        "bajo": [
            "La seguridad es el punto flojo: este año hubo varios robos en la cuadra.",
            "De noche no vuelvo caminando desde la parada, prefiero pedir un auto.",
            "Hay que andar con cuidado, sobre todo cerca de la estación.",
            "Me robaron el celular dos veces en el último año, así que ando con cuidado.",
        ],
    },
    "transporte": {
        "alto": [
            "Por {avenida} pasan muchísimos colectivos, nunca esperás más de cinco minutos.",
            "Tenés colectivos para todos lados y el tren o el subte a pocas cuadras.",
            "Llegar al centro es fácil y rápido, incluso en hora pico.",
        ],
        "medio": [
            "Los colectivos andan bien de día, pero a la noche la frecuencia baja bastante.",
            "Para ir al centro tengo que combinar, pero se hace.",
            "El tren te deja cerca, aunque en hora pico viaja muy lleno.",
        ],
        "bajo": [
            "El transporte es lo peor: dependés de una o dos líneas y a la noche casi no pasan.",
            "Sin auto se complica, la estación queda lejos.",
            "Para llegar al trabajo tardo más de una hora y combino dos colectivos.",
        ],
    },
    "conectividad": {
        "alto": [
            "Hay fibra óptica de varias empresas, trabajo desde casa sin problemas.",
            "Internet y señal de celular, impecables.",
        ],
        "medio": [
            "Internet anda bien, aunque a veces se corta los días de lluvia.",
            "La señal del celular adentro de casa es medio irregular.",
        ],
        "bajo": [
            "Conseguir fibra fue una odisea, solo llega una empresa.",
            "En algunas cuadras la señal de celular es malísima.",
        ],
    },
    "comercios": {
        "alto": [
            "Tenés de todo a mano: súper, verdulería y farmacia a dos cuadras.",
            "Sobre {avenida} hay comercios de todo tipo y bares para salir.",
            "No hace falta moverse del barrio para nada, hay de todo.",
        ],
        "medio": [
            "Lo básico lo tenés cerca; para algo más específico hay que ir hasta {avenida}.",
            "Hay súper y algunos negocios de barrio, nada del otro mundo.",
        ],
        "bajo": [
            "Faltan comercios: para una compra grande tengo que ir en auto.",
            "Hay pocos negocios y cierran temprano.",
        ],
    },
    "espacios-verdes": {
        "alto": [
            "Tengo {verde} cerca y los fines de semana es un programa.",
            "Muchas plazas y calles arboladas, ideal si tenés chicos o perro.",
            "Para salir a correr o pasear al perro hay lugares lindos a pocas cuadras.",
        ],
        "medio": [
            "Hay una plaza linda a unas cuadras, aunque a veces está descuidada.",
            "Algunas calles con árboles y una plaza chica, poco más.",
        ],
        "bajo": [
            "Espacios verdes casi no hay, es todo cemento.",
            "La plaza más cercana queda lejos y está bastante abandonada.",
        ],
    },
}

CIERRES = {
    "alto": [
        "En general lo recomiendo.",
        "Estamos muy bien en el barrio, no nos mudaríamos.",
        "Si podés pagarlo, es una gran opción.",
        "Lo recomiendo para vivir en familia.",
    ],
    "medio": [
        "Tiene sus cosas, pero en general estamos bien.",
        "Hay cosas para mejorar, pero no me iría.",
        "Relación precio-calidad, está bien.",
        "Los alquileres subieron bastante últimamente.",
    ],
    "bajo": [
        "Si tuviera la opción, me mudaría.",
        "Lo elegimos por precio, pero hay que saber a lo que vas.",
        "Para una familia con chicos lo pensaría dos veces.",
    ],
}

# Preguntas: tema, texto y respuestas por nivel del aspecto en la zona (las de "otros" no dependen del perfil)
PREGUNTAS = [
    ("seguridad", "¿Cómo es volver caminando de noche? Llegaría tarde del trabajo varios días a la semana.", {
        "alto": ["Tranquilo, yo vuelvo a las 11 de la noche y nunca tuve problemas.", "Por las calles principales, sin drama. Hay gente hasta tarde."],
        "medio": ["Depende de la cuadra. Por {avenida} bien, las calles internas se ponen oscuras.", "Yo trato de no ir por las cuadras más solitarias, pero en general bien."],
        "bajo": ["Te diría que no. Yo me tomo un auto desde la estación.", "Con cuidado. A una vecina le robaron hace un mes a dos cuadras de {avenida}."],
    }),
    ("transporte", "¿Qué tal se llega al centro en hora pico?", {
        "alto": ["Muy bien, en 30 o 40 minutos estás.", "Tenés varias opciones, se llega rápido."],
        "medio": ["Se llega, pero en hora pico el colectivo viene lleno. Calculá una hora.", "Combinando, unos 50 minutos."],
        "bajo": ["Es largo. Más de una hora y con combinación.", "Complicado sin auto, la verdad."],
    }),
    ("conectividad", "¿Qué empresas de fibra llegan? Necesito buena conexión para trabajar desde casa.", {
        "alto": ["Llegan varias, yo tengo fibra hace años y nunca tuve drama.", "Tenés para elegir, en mi edificio hay tres empresas."],
        "medio": ["Depende de la cuadra, preguntá por tu dirección exacta. Yo tengo una y anda bien.", "En mi cuadra llega una sola, pero anda bien."],
        "bajo": ["Es un tema. En mi casa solo llega una y se corta seguido.", "Yo terminé usando datos del celular, la fibra no llega."],
    }),
    ("comercios", "¿Hay un supermercado grande cerca o conviene pedir online?", {
        "alto": ["Hay varios, sobre {avenida} tenés de todo.", "Tenés súper grande y un montón de chinos y verdulerías."],
        "medio": ["Hay uno mediano y después negocios chicos. Para la compra grande pido online.", "Uno a unas diez cuadras, el resto son almacenes."],
        "bajo": ["Hay que ir en auto. Yo pido online la compra grande.", "No mucho, lo más cerca son almacenes de barrio."],
    }),
    ("espacios-verdes", "¿Hay plazas con juegos para chicos cerca?", {
        "alto": ["Sí, y muy lindas. {verde_mayuscula} es lo mejor que tiene el barrio.", "Varias, los fines de semana están llenas de familias."],
        "medio": ["Hay una a unas cuadras, los juegos están medio gastados pero sirve.", "Una plaza chica, para un rato está bien."],
        "bajo": ["Poco y nada, tenemos que ir en auto a otro barrio.", "La más cerca está bastante descuidada."],
    }),
    ("otros", "¿Cómo están los alquileres? Busco un dos ambientes.", {
        "todos": ["Subieron bastante este año. Conviene buscar directo con dueño.", "Se consigue, pero hay que moverse rápido porque vuelan.", "Hay oferta, aunque muchos piden garantía propietaria."],
    }),
    ("otros", "¿Hay mucho ruido los fines de semana?", {
        "todos": ["Cerca de la avenida sí, adentro es tranquilo.", "Depende de la cuadra, hay algún bar que hace ruido hasta tarde.", "Para nada, es muy tranquilo."],
    }),
    ("otros", "¿Alguna recomendación de escuela primaria pública por la zona?", {
        "todos": ["Hay varias buenas, pero las vacantes son un tema. Anotate temprano.", "La de la vuelta de mi casa es muy buena, preguntá en la cooperadora."],
    }),
]

# Lugares que los vecinos sugieren agregar al mapa (pendientes de validar)
APORTES = [
    ("comercios", "Verdulería La Esquina", "Lunes a sábados de 8 a 20"),
    ("comercios", "Almacén de Don Pepe", "Todos los días de 9 a 22"),
    ("comercios", "Feria de productores", "Sábados de 9 a 14"),
    ("espacios-verdes", "Plaza de bolsillo", None),
    ("salud", "Centro de salud barrial", "Lunes a viernes de 8 a 16"),
    ("deporte", "Club social y deportivo del barrio", "Lunes a sábados de 9 a 22"),
    ("cultura", "Biblioteca popular", "Martes a sábados de 14 a 20"),
]


def nivel(puntaje):
    return "bajo" if puntaje <= 2 else "medio" if puntaje == 3 else "alto"


def sql_texto(valor):
    if valor is None:
        return "NULL"
    return "'" + str(valor).replace("'", "''") + "'"


def hash_identity(contrasena, rng):
    # Formato v3 de ASP.NET Core Identity: 0x01 | PRF (2 = HMAC-SHA512) | iteraciones | largo de sal | sal | clave
    iteraciones = 210_000
    sal = bytes(rng.getrandbits(8) for _ in range(16))
    clave = hashlib.pbkdf2_hmac("sha512", contrasena.encode(), sal, iteraciones, 32)
    return base64.b64encode(b"\x01" + struct.pack(">III", 2, iteraciones, len(sal)) + sal + clave).decode()


def id_demo(*partes):
    return str(uuid.uuid5(ESPACIO_IDS, "/".join(str(p) for p in partes)))


def sin_acentos(texto):
    return texto.lower().translate(str.maketrans("áéíóúüñ", "aeiouun"))


def hace_horas(rng, maximo_dias=540):
    # Mas actividad reciente que vieja: exponencial con media de unos 4 meses
    return max(2, min(maximo_dias * 24, int(rng.expovariate(1 / (120 * 24)))))


def completar(texto, zona, avenida, verde, anios):
    verde = verde or "una plaza"
    return (texto.replace("{zona}", zona).replace("{avenida}", avenida).replace("{anios}", str(anios))
            .replace("{verde_mayuscula}", verde[0].upper() + verde[1:]).replace("{verde}", verde))


def main():
    rng = random.Random(SEMILLA)

    usuarios = []
    usados = set()
    while len(usuarios) < CANTIDAD_USUARIOS:
        nombre, apellido = rng.choice(NOMBRES), rng.choice(APELLIDOS)
        email = f"{sin_acentos(nombre)}.{sin_acentos(apellido)}@{DOMINIO}"
        if email in usados:
            continue
        usados.add(email)
        nacimiento = f"{rng.randint(1960, 2002)}-{rng.randint(1, 12):02d}-{rng.randint(1, 28):02d}"
        usuarios.append((id_demo("usuario", email), email, hash_identity(CONTRASENA_DEMO, rng), nacimiento))

    resenas, votos, preguntas, respuestas, aportes, validaciones = [], [], [], [], [], []

    for slug, (nombre_zona, perfil, avenida, verde) in ZONAS.items():
        autores = rng.sample(usuarios, rng.randint(4, 9))
        for autor in autores:
            puntajes = [max(1, min(5, base + rng.choice([-1, 0, 0, 0, 1]))) for base in perfil]
            anios = rng.choice([1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25])
            temas = sorted(rng.sample(range(5), rng.choice([2, 2, 3])))
            apertura = rng.choice(APERTURAS)
            partes = [completar(apertura, nombre_zona, avenida, verde, anios)]
            partes += [completar(rng.choice(FRASES[ASPECTOS[i]][nivel(puntajes[i])]), nombre_zona, avenida, verde, anios) for i in temas]
            promedio = sum(puntajes) / 5
            partes.append(rng.choice(CIERRES["alto" if promedio >= 3.8 else "medio" if promedio >= 2.8 else "bajo"]))
            texto = " ".join(p for p in partes if p)
            # Los anios que declara: los de la apertura si los dice; si es "de toda la vida", muchos; si no, a veces
            declara_anios = anios if "{anios}" in apertura else 30 if "toda la vida" in apertura else rng.choice([None, anios])
            horas = hace_horas(rng)
            id_resena = id_demo("resena", slug, autor[1])
            resenas.append((id_resena, slug, autor[0], *puntajes, texto, declara_anios, [ASPECTOS[i] for i in temas], horas))

            for votante in rng.sample(usuarios, rng.choice([0, 0, 1, 2, 3, 5, 8])):
                if votante[0] != autor[0]:
                    votos.append((id_resena, votante[0], max(1, horas - rng.randint(1, max(1, horas - 1)))))

        for tema, texto_pregunta, por_nivel in rng.sample(PREGUNTAS, rng.randint(2, 4)):
            autor = rng.choice(usuarios)
            horas = hace_horas(rng, 300)
            id_pregunta = id_demo("pregunta", slug, texto_pregunta)
            preguntas.append((id_pregunta, slug, autor[0], texto_pregunta, horas))

            opciones = por_nivel["todos"] if tema == "otros" else por_nivel[nivel(perfil[ASPECTOS.index(tema)])]
            # Algunas quedan sin responder, para que se vea la invitacion a responder
            for k, texto_respuesta in enumerate(rng.sample(opciones, min(len(opciones), rng.choice([0, 1, 1, 2, 2, 3])))):
                quien = rng.choice([u for u in usuarios if u[0] != autor[0]])
                respuestas.append((id_demo("respuesta", id_pregunta, k), id_pregunta, quien[0],
                                   completar(texto_respuesta, nombre_zona, avenida, verde, 0), max(1, horas - rng.randint(1, max(1, horas - 1)))))

        for k, (categoria, nombre_lugar, horario) in enumerate(rng.sample(APORTES, rng.choice([0, 1, 1, 2]))):
            autor = rng.choice(usuarios)
            horas = hace_horas(rng, 60)
            id_aporte = id_demo("aporte", slug, nombre_lugar)
            aportes.append((id_aporte, slug, autor[0], categoria, nombre_lugar, horario, f"Cerca de {avenida}", horas, rng.randint(1, 10_000)))
            # Confirmaciones que todavia no alcanzan para aprobarlo (hacen falta 3 mas que rechazos)
            for votante in rng.sample([u for u in usuarios if u[0] != autor[0]], rng.choice([0, 1, 2])):
                validaciones.append((id_aporte, votante[0], max(1, horas - 1)))

    escribir(usuarios, resenas, votos, preguntas, respuestas, aportes, validaciones)


def escribir(usuarios, resenas, votos, preguntas, respuestas, aportes, validaciones):
    hace = lambda horas: f"now() - interval '{horas} hours'"
    filas = lambda valores: ",\n".join(valores)
    zonas_existentes = "SELECT slug FROM geo.zonas WHERE elegida"

    sql = [f"""-- DATOS DE DEMO de la comunidad de las zonas. Generado por etl/demo/generar_comunidad_demo.py: no editar a mano.
-- Todo es inventado. Autores: usuarios @{DOMINIO} (contrasena: {CONTRASENA_DEMO}).
-- Borra los datos de demo anteriores y vuelve a cargarlos; las zonas que no existen en geo.zonas se saltean.
-- Uso con Docker (sirve en PowerShell, que no tiene "<"):
--   docker cp etl/demo/comunidad_demo.sql zonamatch-db-1:/tmp/comunidad_demo.sql
--   docker exec zonamatch-db-1 psql -U zonamatch -d zonamatch_docker -f /tmp/comunidad_demo.sql
-- Uso con psql contra cualquier base: psql "host=... port=... dbname=... user=..." -f etl/demo/comunidad_demo.sql

\\set ON_ERROR_STOP on
-- El archivo es UTF-8: sin esto, psql en Windows lo lee con la codificacion del sistema y rompe las tildes
SET client_encoding = 'UTF8';
BEGIN;

-- Borrar un usuario borra en cascada sus resenas, votos, preguntas, respuestas, aportes y validaciones
DELETE FROM app.usuarios WHERE email LIKE '%@{DOMINIO}';

INSERT INTO app.usuarios (id, email, password_hash, fecha_nacimiento, fecha_creacion) VALUES
{filas(f"({sql_texto(i)}, {sql_texto(e)}, {sql_texto(h)}, {sql_texto(n)}, now() - interval '600 days')" for i, e, h, n in usuarios)};

INSERT INTO app.resenas (id, zona_slug, usuario_id, puntaje_seguridad, puntaje_transporte, puntaje_conectividad,
                         puntaje_comercios, puntaje_espacios_verdes, texto, anios_en_zona, temas, verificada,
                         fecha_creacion, fecha_actualizacion)
SELECT v.id::uuid, v.slug, v.usuario::uuid, v.s, v.t, v.c, v.co, v.ev, v.texto, v.anios, v.temas, false, v.fecha, v.fecha
FROM (VALUES
{filas(f"({sql_texto(r[0])}, {sql_texto(r[1])}, {sql_texto(r[2])}, {r[3]}, {r[4]}, {r[5]}, {r[6]}, {r[7]}, {sql_texto(r[8])}, {r[9] if r[9] is not None else 'NULL::int'}, ARRAY[{', '.join(sql_texto(t) for t in r[10])}]::text[], {hace(r[11])})" for r in resenas)}
) AS v(id, slug, usuario, s, t, c, co, ev, texto, anios, temas, fecha)
WHERE v.slug IN ({zonas_existentes});

INSERT INTO app.resenas_votos_util (resena_id, usuario_id, fecha)
SELECT v.resena::uuid, v.usuario::uuid, v.fecha
FROM (VALUES
{filas(f"({sql_texto(r)}, {sql_texto(u)}, {hace(h)})" for r, u, h in votos)}
) AS v(resena, usuario, fecha)
WHERE v.resena::uuid IN (SELECT id FROM app.resenas);

INSERT INTO app.preguntas (id, zona_slug, usuario_id, texto, fecha_creacion)
SELECT v.id::uuid, v.slug, v.usuario::uuid, v.texto, v.fecha
FROM (VALUES
{filas(f"({sql_texto(p[0])}, {sql_texto(p[1])}, {sql_texto(p[2])}, {sql_texto(p[3])}, {hace(p[4])})" for p in preguntas)}
) AS v(id, slug, usuario, texto, fecha)
WHERE v.slug IN ({zonas_existentes});

INSERT INTO app.respuestas (id, pregunta_id, usuario_id, texto, fecha_creacion)
SELECT v.id::uuid, v.pregunta::uuid, v.usuario::uuid, v.texto, v.fecha
FROM (VALUES
{filas(f"({sql_texto(r[0])}, {sql_texto(r[1])}, {sql_texto(r[2])}, {sql_texto(r[3])}, {hace(r[4])})" for r in respuestas)}
) AS v(id, pregunta, usuario, texto, fecha)
WHERE v.pregunta::uuid IN (SELECT id FROM app.preguntas);

-- Puntos nuevos pendientes de validar, en un lugar al azar (fijo por semilla) dentro de la zona
INSERT INTO app.aportes (id, zona_slug, usuario_id, tipo, estado, nombre, categoria, ubicacion, horario, direccion,
                         vive_o_trabaja_en_zona, fecha_creacion)
SELECT v.id::uuid, v.slug, v.usuario::uuid, 'PuntoNuevo', 'Pendiente', v.nombre, v.categoria,
       (SELECT ST_GeometryN(ST_GeneratePoints(z.geom, 1, v.semilla), 1) FROM geo.zonas z WHERE z.elegida AND z.slug = v.slug LIMIT 1),
       v.horario, v.direccion, true, v.fecha
FROM (VALUES
{filas(f"({sql_texto(a[0])}, {sql_texto(a[1])}, {sql_texto(a[2])}, {sql_texto(a[3])}, {sql_texto(a[4])}, {sql_texto(a[5])}, {sql_texto(a[6])}, {hace(a[7])}, {a[8]})" for a in aportes)}
) AS v(id, slug, usuario, categoria, nombre, horario, direccion, fecha, semilla)
WHERE v.slug IN ({zonas_existentes});

INSERT INTO app.aportes_validaciones (aporte_id, usuario_id, confirma, fecha)
SELECT v.aporte::uuid, v.usuario::uuid, true, v.fecha
FROM (VALUES
{filas(f"({sql_texto(a)}, {sql_texto(u)}, {hace(h)})" for a, u, h in validaciones)}
) AS v(aporte, usuario, fecha)
WHERE v.aporte::uuid IN (SELECT id FROM app.aportes);

COMMIT;

SELECT (SELECT count(*) FROM app.usuarios WHERE email LIKE '%@{DOMINIO}') AS usuarios_demo,
       (SELECT count(*) FROM app.resenas r JOIN app.usuarios u ON u.id = r.usuario_id WHERE u.email LIKE '%@{DOMINIO}') AS resenas,
       (SELECT count(*) FROM app.preguntas p JOIN app.usuarios u ON u.id = p.usuario_id WHERE u.email LIKE '%@{DOMINIO}') AS preguntas,
       (SELECT count(*) FROM app.respuestas r JOIN app.usuarios u ON u.id = r.usuario_id WHERE u.email LIKE '%@{DOMINIO}') AS respuestas,
       (SELECT count(*) FROM app.aportes a JOIN app.usuarios u ON u.id = a.usuario_id WHERE u.email LIKE '%@{DOMINIO}') AS aportes;
"""]
    SALIDA.write_text("".join(sql), encoding="utf-8", newline="\n")
    print(f"{SALIDA.name}: {len(usuarios)} usuarios, {len(resenas)} reseñas, {len(votos)} votos, "
          f"{len(preguntas)} preguntas, {len(respuestas)} respuestas, {len(aportes)} aportes")


if __name__ == "__main__":
    main()
