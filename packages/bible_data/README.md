# Base de Données Biblique EECC

Module dédié au schéma et aux données textuelles de la Bible (Ancien et Nouveau Testament).

## Sources et Références
- Référence de départ : [scrollmapper/bible_databases](https://github.com/scrollmapper/bible_databases)
- Version de référence principale : **Louis Segond (1910)** en français.

## Structure
- Les tables correspondantes dans Supabase sont `bible_books` et `bible_verses` (définies dans `supabase/migrations/20261111000000_initial_schema.sql`).
- Pour les applications mobiles (EECC TV), une base SQLite locale ou des fichiers JSON optimisés peuvent être embarqués pour permettre la lecture hors ligne sans solliciter le réseau.
