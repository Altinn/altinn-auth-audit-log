-- Table: authentication.eventlog
CREATE TABLE IF NOT EXISTS authentication.eventlog
(
	identifier bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created timestamp with time zone NOT NULL,
	modified timestamp with time zone,
	authenticationeventjson jsonb
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog_admin;
