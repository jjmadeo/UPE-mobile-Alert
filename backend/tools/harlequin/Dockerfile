# IDE de SQL por terminal (https://harlequin.sh) para poder mirar/tocar la
# Postgres de backend/ a mano mientras se desarrolla, sin instalar Python ni
# nada en el host — se levanta con `docker compose run --rm harlequin` desde
# backend/ (ver el servicio `harlequin` en docker-compose.yml).
FROM python:3.12-slim

RUN pip install --no-cache-dir harlequin harlequin-postgres

ENTRYPOINT ["harlequin"]
