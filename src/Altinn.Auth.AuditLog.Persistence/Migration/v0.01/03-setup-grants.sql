GRANT USAGE ON SCHEMA authz TO "${APP-USER}";
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,TRIGGER ON ALL TABLES IN SCHEMA authz TO "${APP-USER}";
GRANT ALL ON ALL SEQUENCES IN SCHEMA authz TO "${APP-USER}";
