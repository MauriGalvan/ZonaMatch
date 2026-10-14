-- Borra los datos de demo de la comunidad (etl/demo/comunidad_demo.sql). Borrar los usuarios de demo borra en
-- cascada sus resenas, votos, preguntas, respuestas, aportes y validaciones.

DELETE FROM app.usuarios WHERE email LIKE '%@demo.zonamatch.ar';
