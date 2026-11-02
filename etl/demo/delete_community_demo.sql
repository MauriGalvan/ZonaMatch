-- Deletes demo data for zone community features (etl/demo/community_demo.sql). Deleting demo users cascades to their
-- reviews, votes, questions, answers, contributions, and validations.

DELETE FROM app.usuarios WHERE email LIKE '%@demo.zonamatch.ar';
