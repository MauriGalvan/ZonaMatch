-- Borra los datos de demo de la comunidad (etl/demo/comunidad_demo.sql). Borrar los usuarios de demo borra en
-- cascada sus resenas, votos, preguntas, respuestas, aportes y validaciones.
-- Uso: docker compose exec -T db psql -U zonamatch -d zonamatch_docker < etl\demo\borrar_comunidad_demo.sql

DELETE FROM app.usuarios WHERE email LIKE '%@demo.zonamatch.ar';
