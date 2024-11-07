-- grant on tables
ALTER DEFAULT PRIVILEGES FOR USER "${YUNIQL-USER}" IN SCHEMA authentication GRANT
SELECT
, INSERT, UPDATE, REFERENCES, DELETE, TRUNCATE, TRIGGER ON TABLES TO "${APP-USER}";

ALTER DEFAULT PRIVILEGES FOR USER "${YUNIQL-USER}" IN SCHEMA authz GRANT
SELECT
, INSERT, UPDATE, REFERENCES, DELETE, TRUNCATE, TRIGGER ON TABLES TO "${APP-USER}";

-- grant on sequences
ALTER DEFAULT PRIVILEGES FOR USER "${YUNIQL-USER}" IN SCHEMA authentication GRANT ALL ON SEQUENCES TO "${APP-USER}";

ALTER DEFAULT PRIVILEGES FOR USER "${YUNIQL-USER}" IN SCHEMA authz GRANT ALL ON SEQUENCES TO "${APP-USER}";