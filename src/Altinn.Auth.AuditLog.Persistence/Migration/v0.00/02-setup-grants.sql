GRANT USAGE ON SCHEMA authentication TO "${APP-USER}";
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,TRIGGER ON ALL TABLES IN SCHEMA authentication TO "${APP-USER}";
GRANT ALL ON ALL SEQUENCES IN SCHEMA authentication TO "${APP-USER}";
