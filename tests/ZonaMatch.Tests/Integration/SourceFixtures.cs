namespace ZonaMatch.Tests.Integration
{
    // tablas de origen con el mismo esquema que la base compartida (zm.*) y que osm2pgsql (planet_osm_*).
    // la configuración real de appsettings.json se ejecuta contra estos datos
    public static class SourceFixtures
    {
        public const string Sql = """
            CREATE SCHEMA zm;

            CREATE TABLE zm.comunas (key text PRIMARY KEY, caba_id text, nombre text NOT NULL, provincia text, censo_codigo text,
                geom geometry(MultiPolygon,4326) NOT NULL);
            CREATE TABLE zm.partidos (key text PRIMARY KEY, municipio_id integer, nombre text NOT NULL, provincia text, censo_codigo text,
                geom geometry(MultiPolygon,4326) NOT NULL);
            CREATE TABLE zm.radios (id text PRIMARY KEY, origen text NOT NULL, depto text, comuna text, fraccion text, radio text,
                geom geometry(MultiPolygon,4326) NOT NULL);
            CREATE TABLE zm.escuelas (clave_natural text PRIMARY KEY, nombre text NOT NULL, nivel text, sector text, modalidad text,
                direccion text, localidad text, cue text, matricula integer, partido_key text, geom geometry(Point,4326) NOT NULL);

            CREATE FUNCTION pg_temp.box(x1 float8, y1 float8, x2 float8, y2 float8) RETURNS geometry AS
                $$ SELECT ST_Multi(ST_MakeEnvelope(x1, y1, x2, y2, 4326)) $$ LANGUAGE sql;
            CREATE FUNCTION pg_temp.merc(g geometry) RETURNS geometry AS
                $$ SELECT ST_Transform(g, 3857) $$ LANGUAGE sql;

            INSERT INTO zm.comunas VALUES ('COMUNA 10', '506', 'COMUNA 10', 'CABA', 'COMUNA 10', pg_temp.box(-58.53, -34.65, -58.49, -34.61));

            INSERT INTO zm.partidos VALUES
                ('LA MATANZA', 6427, 'La Matanza', 'BUENOS AIRES', '427', pg_temp.box(-58.60, -34.70, -58.53, -34.62)),
                ('MORON', 6568, 'Morón', 'BUENOS AIRES', '568', pg_temp.box(-58.66, -34.68, -58.60, -34.62)),
                ('PILAR', 6638, 'Pilar', 'BUENOS AIRES', '638', pg_temp.box(-58.95, -34.50, -58.85, -34.40)),
                ('TIGRE', 6805, 'Tigre', 'BUENOS AIRES', '805', pg_temp.box(-58.60, -34.45, -58.55, -34.42)),
                ('ISLAS TIGRE', 6805, 'Islas Tigre', 'BUENOS AIRES', '805', pg_temp.box(-58.60, -34.42, -58.40, -34.30));

            INSERT INTO zm.radios VALUES
                ('020100101', 'caba', NULL, 'COMUNA 10', '01', '01', pg_temp.box(-58.52, -34.64, -58.50, -34.62)),
                ('020100102', 'caba', NULL, 'COMUNA 10', '01', '02', 'SRID=4326;MULTIPOLYGON EMPTY'),
                ('064270101', 'pba', '427', NULL, '01', '01', pg_temp.box(-58.58, -34.66, -58.55, -34.64)),
                ('069990101', 'pba', '999', NULL, '01', '01', pg_temp.box(-58.58, -34.66, -58.55, -34.64));

            INSERT INTO zm.escuelas VALUES
                ('e1', 'ESCUELA DE EDUCACION PRIMARIA N°39 "EL PAMPERO"', 'Nivel Primario', 'Estatal', 'Educación Común',
                    'Av. de Mayo 700', 'RAMOS MEJIA', '0601', 300, 'LA MATANZA', ST_SetSRID(ST_MakePoint(-58.5652, -34.6501), 4326)),
                ('e2', 'JARDIN LOS PITUFOS', 'Nivel Inicial', 'Privado', 'Educación Común',
                    NULL, 'RAMOS MEJIA', '0602', 80, 'LA MATANZA', ST_SetSRID(ST_MakePoint(-58.5600, -34.6600), 4326)),
                ('e3', 'CENTRO DE FORMACION PROFESIONAL 401', 'Formación Profesional', 'Estatal', NULL,
                    NULL, 'RAMOS MEJIA', '0603', 50, 'LA MATANZA', ST_SetSRID(ST_MakePoint(-58.5610, -34.6610), 4326)),
                ('e4', 'ESCUELA SECUNDARIA 1', 'Nivel Secundario', 'Estatal', NULL,
                    NULL, 'PILAR', '0604', 400, 'PILAR', ST_SetSRID(ST_MakePoint(-58.90, -34.45), 4326));

            -- salida clásica de osm2pgsql con --hstore: EPSG:3857 y etiquetas como columnas
            CREATE TABLE public.planet_osm_polygon (osm_id bigint, name text, boundary text, admin_level text, amenity text, shop text,
                leisure text, tourism text, railway text, sport text, operator text, tags hstore, way geometry(Geometry,3857));
            CREATE TABLE public.planet_osm_point (osm_id bigint, name text, amenity text, shop text, leisure text, tourism text,
                railway text, highway text, sport text, operator text, tags hstore, way geometry(Point,3857));

            INSERT INTO public.planet_osm_polygon (osm_id, name, boundary, admin_level, way) VALUES
                -- relación partida en dos filas con el mismo id
                (-1001, 'Villa Luro', 'administrative', '10', pg_temp.merc(ST_MakeEnvelope(-58.53, -34.64, -58.51, -34.62, 4326))),
                (-1001, 'Villa Luro', 'administrative', '10', pg_temp.merc(ST_MakeEnvelope(-58.51, -34.64, -58.49, -34.62, 4326))),
                (-1002, 'Barrio fuera de CABA', 'administrative', '10', pg_temp.merc(ST_MakeEnvelope(-58.30, -34.60, -58.28, -34.58, 4326))),
                (-2001, 'Ramos Mejía', 'administrative', '8', pg_temp.merc(ST_MakeEnvelope(-58.60, -34.67, -58.53, -34.63, 4326))),
                (-2002, 'Haedo', 'administrative', '8', pg_temp.merc(ST_MakeEnvelope(-58.63, -34.67, -58.60, -34.63, 4326))),
                (-2003, 'San Justo', 'administrative', '8', pg_temp.merc(ST_MakeEnvelope(-58.59, -34.70, -58.57, -34.68, 4326))),
                (-2004, 'San Justo', 'administrative', '8', pg_temp.merc(ST_MakeEnvelope(-58.56, -34.70, -58.54, -34.68, 4326))),
                (-2005, 'Del Viso', 'administrative', '8', pg_temp.merc(ST_MakeEnvelope(-58.92, -34.48, -58.88, -34.44, 4326))),
                (-2006, 'Localidad sin partido', 'administrative', '8', pg_temp.merc(ST_MakeEnvelope(-57.60, -35.00, -57.50, -34.90, 4326)));

            INSERT INTO public.planet_osm_polygon (osm_id, name, leisure, tags, way) VALUES
                (4001, 'Plaza Villa Luro', 'park', 'operator:type=>public', pg_temp.merc(ST_MakeEnvelope(-58.512, -34.632, -58.510, -34.630, 4326)));

            INSERT INTO public.planet_osm_point (osm_id, name, amenity, railway, highway, tags, way) VALUES
                (5001, NULL, NULL, NULL, 'bus_stop', 'operator:type=>public, addr:street=>Rivadavia, addr:housenumber=>9800',
                    pg_temp.merc(ST_SetSRID(ST_MakePoint(-58.505, -34.630), 4326))),
                (5002, 'Estación Villa Luro', NULL, 'station', NULL, 'operator=>"Trenes Argentinos", name:es=>""',
                    pg_temp.merc(ST_SetSRID(ST_MakePoint(-58.508, -34.635), 4326))),
                (5003, 'Estación Plaza Miserere', NULL, 'station', NULL, 'station=>subway',
                    pg_temp.merc(ST_SetSRID(ST_MakePoint(-58.506, -34.633), 4326))),
                (5004, 'Escuela N° 39', 'school', NULL, NULL, NULL,
                    pg_temp.merc(ST_SetSRID(ST_MakePoint(-58.5650, -34.6500), 4326))),
                (5005, 'Estacionamiento', 'parking', NULL, NULL, NULL,
                    pg_temp.merc(ST_SetSRID(ST_MakePoint(-58.507, -34.631), 4326))),
                (5006, 'Hospital Posadas', 'hospital', NULL, NULL, 'operator:type=>government',
                    pg_temp.merc(ST_SetSRID(ST_MakePoint(-58.615, -34.645), 4326)));
            """;
    }
}
