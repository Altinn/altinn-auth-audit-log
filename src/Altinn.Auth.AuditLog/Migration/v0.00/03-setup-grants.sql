GRANT USAGE ON SCHEMA authentication TO auth_auditlog;
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,TRIGGER ON ALL TABLES IN SCHEMA authentication TO auth_auditlog;
GRANT ALL ON ALL SEQUENCES IN SCHEMA authentication TO auth_auditlog;