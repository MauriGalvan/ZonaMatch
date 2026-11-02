"""
Generates etl/demo/community_demo.sql: demo data for zone community features (reviews, helpfulness votes,
questions with answers, and map contributions pending validation).

All data is synthetic. Demo users have @demo.zonamatch.ar email addresses, so they can be identified and deleted
(etl/demo/delete_community_demo.sql). They can sign in with the password defined by DEMO_PASSWORD.

Each zone has a profile (1 to 5 per aspect) that guides scores and text, so a review of a quiet neighborhood does
not describe it as unsafe. Dates are stored as "N hours ago" and resolved with now() when loaded, so activity
always appears recent.

The output is deterministic (fixed seed). Usage:
    python etl/demo/generate_community_demo.py
"""

import base64
import hashlib
import random
import struct
import uuid
from pathlib import Path

RANDOM_SEED = 2026
DEMO_PASSWORD = "ZonaMatchDemo2026"
DEMO_DOMAIN = "demo.zonamatch.ar"
ID_NAMESPACE = uuid.UUID("6f0c2b52-6c53-4a8e-9a43-0f2e7f6d2a10")
OUTPUT_FILE = Path(__file__).with_name("community_demo.sql")

ASPECTS = ["seguridad", "transporte", "conectividad", "comercios", "espacios-verdes"]

# Each zone's profile: safety, transit, connectivity, shops, and green spaces (1 to 5),
# plus local references used in the sample text (avenue and green space; None if there is no clear reference).
ZONES = {
    # City of Buenos Aires.
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
    # Greater Buenos Aires.
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

FIRST_NAMES = [
    "Ana", "Lucía", "Martina", "Sofía", "Valentina", "Camila", "Florencia", "Julieta", "Carolina", "Paula",
    "Agustina", "Micaela", "Romina", "Natalia", "Gabriela", "Laura", "Silvina", "Mariana", "Belén", "Rocío",
    "Juan", "Martín", "Lucas", "Matías", "Nicolás", "Santiago", "Facundo", "Diego", "Pablo", "Federico",
    "Gonzalo", "Tomás", "Ezequiel", "Leandro", "Sebastián", "Hernán", "Damián", "Franco", "Ignacio", "Rodrigo",
]
LAST_NAMES = [
    "Pérez", "González", "Rodríguez", "Fernández", "López", "Martínez", "García", "Sánchez", "Romero", "Díaz",
    "Álvarez", "Torres", "Ruiz", "Ramírez", "Flores", "Acosta", "Benítez", "Medina", "Herrera", "Suárez",
    "Aguirre", "Giménez", "Molina", "Castro", "Ortiz", "Silva", "Núñez", "Luna", "Ríos", "Cabrera",
]
USER_COUNT = 64

REVIEW_OPENINGS = [
    "Vivo en {zona} hace {anios} años.",
    "Hace {anios} años que alquilo en {zona}.",
    "Me mudé a {zona} hace {anios} años con mi pareja.",
    "Soy de {zona} de toda la vida.",
    "Llegué a {zona} por laburo y me terminé quedando.",
    "Estoy en {zona} desde hace un par de años.",
    "Nos vinimos a {zona} con los chicos hace {anios} años.",
    "",
]

# Review text by aspect and level: low (1-2), medium (3), or high (4-5).
REVIEW_PHRASES = {
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

REVIEW_CLOSINGS = {
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

# Questions: topic, prompt, and answers by zone aspect level ("other" questions do not depend on the profile).
SAMPLE_QUESTIONS = [
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

# Places residents suggest adding to the map (pending validation).
SUGGESTED_PLACES = [
    ("comercios", "Verdulería La Esquina", "Lunes a sábados de 8 a 20"),
    ("comercios", "Almacén de Don Pepe", "Todos los días de 9 a 22"),
    ("comercios", "Feria de productores", "Sábados de 9 a 14"),
    ("espacios-verdes", "Plaza de bolsillo", None),
    ("salud", "Centro de salud barrial", "Lunes a viernes de 8 a 16"),
    ("deporte", "Club social y deportivo del barrio", "Lunes a sábados de 9 a 22"),
    ("cultura", "Biblioteca popular", "Martes a sábados de 14 a 20"),
]


def score_level(score):
    return "bajo" if score <= 2 else "medio" if score == 3 else "alto"


def sql_literal(value):
    if value is None:
        return "NULL"
    return "'" + str(value).replace("'", "''") + "'"


def hash_identity_password(password, rng):
    # ASP.NET Core Identity v3 format: 0x01 | PRF (2 = HMAC-SHA512) | iterations | salt length | salt | key.
    iterations = 210_000
    salt = bytes(rng.getrandbits(8) for _ in range(16))
    key = hashlib.pbkdf2_hmac("sha512", password.encode(), salt, iterations, 32)
    return base64.b64encode(b"\x01" + struct.pack(">III", 2, iterations, len(salt)) + salt + key).decode()


def demo_id(*parts):
    return str(uuid.uuid5(ID_NAMESPACE, "/".join(str(part) for part in parts)))


def normalize_name(text):
    return text.lower().translate(str.maketrans("áéíóúüñ", "aeiouun"))


def random_age_hours(rng, max_days=540):
    # Recent activity is more common: exponential distribution with a mean of about four months.
    return max(2, min(max_days * 24, int(rng.expovariate(1 / (120 * 24)))))


def fill_template(text, zone, avenue, green_space, years):
    green_space = green_space or "una plaza"
    return (text.replace("{zona}", zone).replace("{avenida}", avenue).replace("{anios}", str(years))
            .replace("{verde_mayuscula}", green_space[0].upper() + green_space[1:]).replace("{verde}", green_space))


def main():
    rng = random.Random(RANDOM_SEED)

    users = []
    used_emails = set()
    while len(users) < USER_COUNT:
        first_name, last_name = rng.choice(FIRST_NAMES), rng.choice(LAST_NAMES)
        email = f"{normalize_name(first_name)}.{normalize_name(last_name)}@{DEMO_DOMAIN}"
        if email in used_emails:
            continue
        used_emails.add(email)
        birth_date = f"{rng.randint(1960, 2002)}-{rng.randint(1, 12):02d}-{rng.randint(1, 28):02d}"
        users.append((demo_id("usuario", email), email, hash_identity_password(DEMO_PASSWORD, rng), birth_date))

    reviews, votes, questions, answers, contributions, validations = [], [], [], [], [], []

    for zone_slug, (zone_name, zone_profile, avenue, green_space) in ZONES.items():
        review_authors = rng.sample(users, rng.randint(4, 9))
        for review_author in review_authors:
            scores = [max(1, min(5, base + rng.choice([-1, 0, 0, 0, 1]))) for base in zone_profile]
            years_in_zone = rng.choice([1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25])
            topics = sorted(rng.sample(range(5), rng.choice([2, 2, 3])))
            opening = rng.choice(REVIEW_OPENINGS)
            review_parts = [fill_template(opening, zone_name, avenue, green_space, years_in_zone)]
            review_parts += [fill_template(rng.choice(REVIEW_PHRASES[ASPECTS[index]][score_level(scores[index])]), zone_name, avenue, green_space, years_in_zone) for index in topics]
            average_score = sum(scores) / 5
            review_parts.append(rng.choice(REVIEW_CLOSINGS["alto" if average_score >= 3.8 else "medio" if average_score >= 2.8 else "bajo"]))
            review_text = " ".join(part for part in review_parts if part)
            # Reported years come from the opening when stated; "de toda la vida" means many years; otherwise it is optional.
            declared_years = years_in_zone if "{anios}" in opening else 30 if "toda la vida" in opening else rng.choice([None, years_in_zone])
            age_hours = random_age_hours(rng)
            review_id = demo_id("resena", zone_slug, review_author[1])
            reviews.append((review_id, zone_slug, review_author[0], *scores, review_text, declared_years, [ASPECTS[index] for index in topics], age_hours))

            for voter in rng.sample(users, rng.choice([0, 0, 1, 2, 3, 5, 8])):
                if voter[0] != review_author[0]:
                    votes.append((review_id, voter[0], max(1, age_hours - rng.randint(1, max(1, age_hours - 1)))))

        for topic, question_text, answers_by_level in rng.sample(SAMPLE_QUESTIONS, rng.randint(2, 4)):
            question_author = rng.choice(users)
            age_hours = random_age_hours(rng, 300)
            question_id = demo_id("pregunta", zone_slug, question_text)
            questions.append((question_id, zone_slug, question_author[0], question_text, age_hours))

            answer_options = answers_by_level["todos"] if topic == "otros" else answers_by_level[score_level(zone_profile[ASPECTS.index(topic)])]
            # Some questions remain unanswered to encourage users to respond.
            for answer_index, answer_text in enumerate(rng.sample(answer_options, min(len(answer_options), rng.choice([0, 1, 1, 2, 2, 3])))):
                respondent = rng.choice([user for user in users if user[0] != question_author[0]])
                answers.append((demo_id("respuesta", question_id, answer_index), question_id, respondent[0],
                                fill_template(answer_text, zone_name, avenue, green_space, 0), max(1, age_hours - rng.randint(1, max(1, age_hours - 1)))))

        for category, place_name, opening_hours in rng.sample(SUGGESTED_PLACES, rng.choice([0, 1, 1, 2])):
            contributor = rng.choice(users)
            age_hours = random_age_hours(rng, 60)
            contribution_id = demo_id("aporte", zone_slug, place_name)
            contributions.append((contribution_id, zone_slug, contributor[0], category, place_name, opening_hours, f"Cerca de {avenue}", age_hours, rng.randint(1, 10_000)))
            # These confirmations are not enough to approve a contribution (three more confirmations than rejections are required).
            for voter in rng.sample([user for user in users if user[0] != contributor[0]], rng.choice([0, 1, 2])):
                validations.append((contribution_id, voter[0], max(1, age_hours - 1)))

    write_demo_sql(users, reviews, votes, questions, answers, contributions, validations)


def write_demo_sql(users, reviews, votes, questions, answers, contributions, validations):
    age_expression = lambda hours: f"now() - interval '{hours} hours'"
    sql_rows = lambda values: ",\n".join(values)
    existing_zones_query = "SELECT slug FROM geo.zonas WHERE elegida"

    sql = [f"""-- Demo data for zone community features. Generated by etl/demo/generate_community_demo.py; do not edit manually.
-- All content is synthetic. Authors are users @{DEMO_DOMAIN} (password: {DEMO_PASSWORD}).
-- Delete previous demo data and reload it; zones missing from geo.zonas are skipped.
-- Run with Docker (works in PowerShell, which does not support "<" redirection):
--   docker cp etl/demo/community_demo.sql zonamatch-db-1:/tmp/community_demo.sql
--   docker exec zonamatch-db-1 psql -U zonamatch -d zonamatch_docker -f /tmp/community_demo.sql
-- Run with psql against any database: psql "host=... port=... dbname=... user=..." -f etl/demo/community_demo.sql

\\set ON_ERROR_STOP on
-- The file is UTF-8; without this setting, psql on Windows uses the system encoding and corrupts accented text.
SET client_encoding = 'UTF8';
BEGIN;

-- Deleting a user cascades to their reviews, votes, questions, answers, contributions, and validations.
DELETE FROM app.usuarios WHERE email LIKE '%@{DEMO_DOMAIN}';

INSERT INTO app.usuarios (id, email, password_hash, fecha_nacimiento, fecha_creacion) VALUES
{sql_rows(f"({sql_literal(user_id)}, {sql_literal(email)}, {sql_literal(password_hash)}, {sql_literal(birth_date)}, now() - interval '600 days')" for user_id, email, password_hash, birth_date in users)};

INSERT INTO app.resenas (id, zona_slug, usuario_id, puntaje_seguridad, puntaje_transporte, puntaje_conectividad,
                         puntaje_comercios, puntaje_espacios_verdes, texto, anios_en_zona, temas, verificada,
                         fecha_creacion, fecha_actualizacion)
SELECT v.id::uuid, v.slug, v.usuario::uuid, v.s, v.t, v.c, v.co, v.ev, v.texto, v.anios, v.temas, false, v.fecha, v.fecha
FROM (VALUES
{sql_rows(f"({sql_literal(review[0])}, {sql_literal(review[1])}, {sql_literal(review[2])}, {review[3]}, {review[4]}, {review[5]}, {review[6]}, {review[7]}, {sql_literal(review[8])}, {review[9] if review[9] is not None else 'NULL::int'}, ARRAY[{', '.join(sql_literal(topic) for topic in review[10])}]::text[], {age_expression(review[11])})" for review in reviews)}
) AS v(id, slug, usuario, s, t, c, co, ev, texto, anios, temas, fecha)
WHERE v.slug IN ({existing_zones_query});

INSERT INTO app.resenas_votos_util (resena_id, usuario_id, fecha)
SELECT v.resena::uuid, v.usuario::uuid, v.fecha
FROM (VALUES
{sql_rows(f"({sql_literal(review_id)}, {sql_literal(user_id)}, {age_expression(hours)})" for review_id, user_id, hours in votes)}
) AS v(resena, usuario, fecha)
WHERE v.resena::uuid IN (SELECT id FROM app.resenas);

INSERT INTO app.preguntas (id, zona_slug, usuario_id, texto, fecha_creacion)
SELECT v.id::uuid, v.slug, v.usuario::uuid, v.texto, v.fecha
FROM (VALUES
{sql_rows(f"({sql_literal(question[0])}, {sql_literal(question[1])}, {sql_literal(question[2])}, {sql_literal(question[3])}, {age_expression(question[4])})" for question in questions)}
) AS v(id, slug, usuario, texto, fecha)
WHERE v.slug IN ({existing_zones_query});

INSERT INTO app.respuestas (id, pregunta_id, usuario_id, texto, fecha_creacion)
SELECT v.id::uuid, v.pregunta::uuid, v.usuario::uuid, v.texto, v.fecha
FROM (VALUES
{sql_rows(f"({sql_literal(answer[0])}, {sql_literal(answer[1])}, {sql_literal(answer[2])}, {sql_literal(answer[3])}, {age_expression(answer[4])})" for answer in answers)}
) AS v(id, pregunta, usuario, texto, fecha)
WHERE v.pregunta::uuid IN (SELECT id FROM app.preguntas);

-- New points pending validation, placed at a deterministic random location inside the zone.
INSERT INTO app.aportes (id, zona_slug, usuario_id, tipo, estado, nombre, categoria, ubicacion, horario, direccion,
                         vive_o_trabaja_en_zona, fecha_creacion)
SELECT v.id::uuid, v.slug, v.usuario::uuid, 'PuntoNuevo', 'Pendiente', v.nombre, v.categoria,
       (SELECT ST_GeometryN(ST_GeneratePoints(z.geom, 1, v.semilla), 1) FROM geo.zonas z WHERE z.elegida AND z.slug = v.slug LIMIT 1),
       v.horario, v.direccion, true, v.fecha
FROM (VALUES
{sql_rows(f"({sql_literal(contribution[0])}, {sql_literal(contribution[1])}, {sql_literal(contribution[2])}, {sql_literal(contribution[3])}, {sql_literal(contribution[4])}, {sql_literal(contribution[5])}, {sql_literal(contribution[6])}, {age_expression(contribution[7])}, {contribution[8]})" for contribution in contributions)}
) AS v(id, slug, usuario, categoria, nombre, horario, direccion, fecha, semilla)
WHERE v.slug IN ({existing_zones_query});

INSERT INTO app.aportes_validaciones (aporte_id, usuario_id, confirma, fecha)
SELECT v.aporte::uuid, v.usuario::uuid, true, v.fecha
FROM (VALUES
{sql_rows(f"({sql_literal(contribution_id)}, {sql_literal(user_id)}, {age_expression(hours)})" for contribution_id, user_id, hours in validations)}
) AS v(aporte, usuario, fecha)
WHERE v.aporte::uuid IN (SELECT id FROM app.aportes);

COMMIT;

SELECT (SELECT count(*) FROM app.usuarios WHERE email LIKE '%@{DEMO_DOMAIN}') AS usuarios_demo,
       (SELECT count(*) FROM app.resenas r JOIN app.usuarios u ON u.id = r.usuario_id WHERE u.email LIKE '%@{DEMO_DOMAIN}') AS resenas,
       (SELECT count(*) FROM app.preguntas p JOIN app.usuarios u ON u.id = p.usuario_id WHERE u.email LIKE '%@{DEMO_DOMAIN}') AS preguntas,
       (SELECT count(*) FROM app.respuestas r JOIN app.usuarios u ON u.id = r.usuario_id WHERE u.email LIKE '%@{DEMO_DOMAIN}') AS respuestas,
       (SELECT count(*) FROM app.aportes a JOIN app.usuarios u ON u.id = a.usuario_id WHERE u.email LIKE '%@{DEMO_DOMAIN}') AS aportes;
"""]
    OUTPUT_FILE.write_text("".join(sql), encoding="utf-8", newline="\n")
    print(f"{OUTPUT_FILE.name}: {len(users)} users, {len(reviews)} reviews, {len(votes)} votes, "
          f"{len(questions)} questions, {len(answers)} answers, {len(contributions)} contributions")


if __name__ == "__main__":
    main()
